using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using JsonViewer.Models;

namespace JsonViewer.Services;

/// <summary>
/// 内存管理器
/// </summary>
public class MemoryManager
{
    private readonly ILogger<MemoryManager> _logger;
    private readonly Timer _monitoringTimer;
    private Timer? _timer;
    private readonly ConcurrentDictionary<string, WeakReference<JsonTreeNode>> _nodeCache;
    private readonly object _lockObject = new object();
    
    // 内存阈值配置
    private const long MEMORY_WARNING_THRESHOLD = 300L * 1024 * 1024; // 300MB (降低阈值)
    private const long MEMORY_CRITICAL_THRESHOLD = 500L * 1024 * 1024; // 500MB (降低阈值)
    private const int MONITORING_INTERVAL_MS = 2000; // 2秒 (更频繁监控)
    private const int CLEANUP_BATCH_SIZE = 50; // 减少批次大小
    private const int AGGRESSIVE_CLEANUP_THRESHOLD = 3; // 连续3次警告后执行积极清理
    
    // 统计信息
    private long _lastGcMemory;
    private DateTime _lastCleanupTime = DateTime.Now;
    private int _cleanupCount;
    private int _cacheHitCount;
    private int _cacheMissCount;
    private int _consecutiveWarnings; // 连续警告次数
    private DateTime _lastAggressiveCleanup = DateTime.MinValue; // 上次积极清理时间

    public event EventHandler<MemoryStatusEventArgs>? MemoryStatusChanged;

    public MemoryManager(ILogger<MemoryManager> logger)
    {
        _logger = logger;
        _nodeCache = new ConcurrentDictionary<string, WeakReference<JsonTreeNode>>();
        
        // 启动内存监控定时器
        _monitoringTimer = new Timer(MonitorMemoryUsage, null, 
            TimeSpan.FromMilliseconds(MONITORING_INTERVAL_MS),
            TimeSpan.FromMilliseconds(MONITORING_INTERVAL_MS));
            
        _logger.LogInformation("内存管理器已启动，监控间隔: {Interval}ms", MONITORING_INTERVAL_MS);
    }

    /// <summary>
    /// 监控内存使用情况
    /// </summary>
    private void MonitorMemoryUsage(object? state)
    {
        try
        {
            var currentMemory = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;
            
            var status = new MemoryStatus
            {
                GcMemory = currentMemory,
                WorkingSet = workingSet,
                MemoryDelta = currentMemory - _lastGcMemory,
                CacheSize = _nodeCache.Count,
                CacheHitRate = CalculateCacheHitRate()
            };
            
            // 检查内存状态
            if (currentMemory > MEMORY_CRITICAL_THRESHOLD)
            {
                status.Level = MemoryLevel.Critical;
                _logger.LogWarning("内存使用达到临界水平: {Memory:F1}MB", currentMemory / 1024.0 / 1024.0);
                
                _consecutiveWarnings++;
                // 执行紧急清理
                _ = Task.Run(() => PerformEmergencyCleanup());
            }
            else if (currentMemory > MEMORY_WARNING_THRESHOLD)
            {
                status.Level = MemoryLevel.Warning;
                _logger.LogInformation("内存使用达到警告水平: {Memory:F1}MB", currentMemory / 1024.0 / 1024.0);
                
                _consecutiveWarnings++;
                
                // 检查是否需要积极清理
                if (_consecutiveWarnings >= AGGRESSIVE_CLEANUP_THRESHOLD && 
                    DateTime.Now - _lastAggressiveCleanup > TimeSpan.FromMinutes(1))
                {
                    _logger.LogWarning("连续{Count}次内存警告，执行积极清理", _consecutiveWarnings);
                    _ = Task.Run(() => PerformAggressiveCleanup());
                    _lastAggressiveCleanup = DateTime.Now;
                }
                else
                {
                    // 执行常规清理
                    _ = Task.Run(() => PerformRegularCleanup());
                }
            }
            else
            {
                status.Level = MemoryLevel.Normal;
                _consecutiveWarnings = 0; // 重置连续警告计数
            }
            
            _lastGcMemory = currentMemory;
            
            // 触发内存状态变化事件
            MemoryStatusChanged?.Invoke(this, new MemoryStatusEventArgs(status));
            
            // 定期清理缓存
            if (DateTime.Now - _lastCleanupTime > TimeSpan.FromMinutes(2))
            {
                _ = Task.Run(() => CleanupNodeCache());
                _lastCleanupTime = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "内存监控过程中发生错误");
        }
    }

    /// <summary>
    /// 计算缓存命中率
    /// </summary>
    private double CalculateCacheHitRate()
    {
        var totalRequests = _cacheHitCount + _cacheMissCount;
        return totalRequests > 0 ? (double)_cacheHitCount / totalRequests : 0.0;
    }

    /// <summary>
    /// 添加节点到缓存
    /// </summary>
    public void CacheNode(JsonTreeNode node)
    {
        if (node == null)
            return;
            
        var nodeId = node.GetNodeId();
        _nodeCache.AddOrUpdate(nodeId, 
            new WeakReference<JsonTreeNode>(node),
            (key, oldValue) => new WeakReference<JsonTreeNode>(node));
    }

    /// <summary>
    /// 从缓存获取节点
    /// </summary>
    public JsonTreeNode? GetCachedNode(string nodeId)
    {
        if (_nodeCache.TryGetValue(nodeId, out var weakRef) && 
            weakRef.TryGetTarget(out var node))
        {
            Interlocked.Increment(ref _cacheHitCount);
            return node;
        }
        
        Interlocked.Increment(ref _cacheMissCount);
        return null;
    }

    /// <summary>
    /// 从缓存移除节点
    /// </summary>
    public void RemoveFromCache(string nodeId)
    {
        _nodeCache.TryRemove(nodeId, out _);
    }

    /// <summary>
    /// 清理节点缓存
    /// </summary>
    public async Task CleanupNodeCache()
    {
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _nodeCache)
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                    
                    // 批量处理，避免长时间锁定
                    if (keysToRemove.Count >= CLEANUP_BATCH_SIZE)
                    {
                        break;
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _nodeCache.TryRemove(key, out _);
                }
                
                if (keysToRemove.Count > 0)
                {
                    _cleanupCount += keysToRemove.Count;
                    _logger.LogDebug("清理了 {Count} 个无效的缓存节点", keysToRemove.Count);
                }
            }
        });
    }

    /// <summary>
    /// 执行常规清理
    /// </summary>
    public async Task PerformRegularCleanup()
    {
        _logger.LogInformation("开始执行常规内存清理");
        
        try
        {
            // 清理节点缓存
            await CleanupNodeCache();
            
            // 清理未使用的节点子项
            await CleanupUnusedChildren();
            
            // 执行垃圾回收
            GC.Collect(0, GCCollectionMode.Optimized);
            
            _logger.LogInformation("常规内存清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "常规内存清理过程中发生错误");
        }
    }

    /// <summary>
    /// 执行紧急清理
    /// </summary>
    public async Task PerformEmergencyCleanup()
    {
        _logger.LogWarning("开始执行紧急内存清理");
        
        try
        {
            // 清空所有缓存
            _nodeCache.Clear();
            
            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // 清理大对象堆
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            
            _logger.LogWarning("紧急内存清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "紧急内存清理过程中发生错误");
        }
    }

    /// <summary>
    /// 执行积极内存清理
    /// </summary>
    private async Task PerformAggressiveCleanup()
    {
        try
        {
            _logger.LogInformation("开始执行积极内存清理");
            
            // 清理过期的弱引用
            await CleanupExpiredWeakReferences();
            
            // 清理未使用的子节点
            await CleanupUnusedChildren();
            
            // 执行多次垃圾回收
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(i, GCCollectionMode.Optimized);
                await Task.Delay(50); // 短暂延迟
            }
            
            // 压缩大对象堆
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            
            _cleanupCount++;
            _logger.LogInformation("积极内存清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "积极内存清理过程中发生错误");
        }
    }

    /// <summary>
    /// 清理过期的弱引用
    /// </summary>
    private async Task CleanupExpiredWeakReferences()
    {
        var expiredKeys = new List<string>();
        
        await Task.Run(() =>
        {
            foreach (var kvp in _nodeCache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
        });
        
        foreach (var key in expiredKeys)
        {
            _nodeCache.TryRemove(key, out _);
        }
        
        _logger.LogDebug("清理了{Count}个过期的弱引用", expiredKeys.Count);
    }

    /// <summary>
    /// 清理未使用的子节点
    /// </summary>
    private async Task CleanupUnusedChildren()
    {
        await Task.Run(() =>
        {
            var cleanedNodes = 0;
            
            foreach (var kvp in _nodeCache.ToList())
            {
                if (kvp.Value.TryGetTarget(out var node))
                {
                    // 如果节点未展开且有子项，清理子项以释放内存
                    if (!node.IsExpanded && node.Children.Any())
                    {
                        node.ClearChildren();
                        cleanedNodes++;
                    }
                }
            }
            
            if (cleanedNodes > 0)
            {
                _logger.LogDebug("清理了 {Count} 个未使用节点的子项", cleanedNodes);
            }
        });
    }

    /// <summary>
    /// 优化节点树结构
    /// </summary>
    public async Task OptimizeNodeTree(JsonTreeNode rootNode)
    {
        if (rootNode == null)
            return;
            
        await Task.Run(() =>
        {
            OptimizeNodeRecursive(rootNode, 0);
        });
    }

    /// <summary>
    /// 递归优化节点
    /// </summary>
    private void OptimizeNodeRecursive(JsonTreeNode node, int depth)
    {
        // 限制递归深度，避免栈溢出
        if (depth > 50)
            return;
            
        // 如果节点未展开且深度较大，清理其子项
        if (!node.IsExpanded && depth > 3 && node.Children.Any())
        {
            node.ClearChildren();
            return;
        }
        
        // 递归处理子节点
        foreach (var child in node.Children.ToList())
        {
            OptimizeNodeRecursive(child, depth + 1);
        }
    }

    /// <summary>
    /// 获取内存统计信息
    /// </summary>
    public MemoryStatistics GetMemoryStatistics()
    {
        var gcMemory = GC.GetTotalMemory(false);
        var workingSet = Environment.WorkingSet;
        
        return new MemoryStatistics
        {
            GcMemory = gcMemory,
            WorkingSet = workingSet,
            CacheSize = _nodeCache.Count,
            CacheHitRate = CalculateCacheHitRate(),
            CleanupCount = _cleanupCount,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            MemoryPressure = CalculateMemoryPressure(gcMemory)
        };
    }

    /// <summary>
    /// 计算内存压力
    /// </summary>
    private MemoryPressure CalculateMemoryPressure(long currentMemory)
    {
        if (currentMemory > MEMORY_CRITICAL_THRESHOLD)
            return MemoryPressure.High;
        else if (currentMemory > MEMORY_WARNING_THRESHOLD)
            return MemoryPressure.Medium;
        else
            return MemoryPressure.Low;
    }

    /// <summary>
    /// 开始内存监控
    /// </summary>
    public void StartMonitoring()
    {
        _timer = new Timer(MonitorMemoryUsage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(MONITORING_INTERVAL_MS));
        _logger.LogInformation("内存监控已启动");
    }

    /// <summary>
    /// 停止内存监控
    /// </summary>
    public void StopMonitoring()
    {
        _timer?.Dispose();
        _timer = null;
        _logger.LogInformation("内存监控已停止");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _nodeCache.Clear();
        _logger.LogInformation("内存管理器已释放资源");
    }
}

/// <summary>
/// 内存状态
/// </summary>
public class MemoryStatus
{
    public long GcMemory { get; set; }
    public long WorkingSet { get; set; }
    public long MemoryDelta { get; set; }
    public int CacheSize { get; set; }
    public double CacheHitRate { get; set; }
    public MemoryLevel Level { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// 内存级别
/// </summary>
public enum MemoryLevel
{
    Normal,
    Warning,
    Critical
}

/// <summary>
/// 内存压力
/// </summary>
public enum MemoryPressure
{
    Low,
    Medium,
    High
}

/// <summary>
/// 内存统计信息
/// </summary>
public class MemoryStatistics
{
    public long GcMemory { get; set; }
    public long WorkingSet { get; set; }
    public int CacheSize { get; set; }
    public double CacheHitRate { get; set; }
    public int CleanupCount { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public MemoryPressure MemoryPressure { get; set; }
}

/// <summary>
/// 内存状态事件参数
/// </summary>
public class MemoryStatusEventArgs : EventArgs
{
    public MemoryStatus Status { get; }
    
    public MemoryStatusEventArgs(MemoryStatus status)
    {
        Status = status;
    }
}
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using JsonViewer.Models;

namespace JsonViewer.Services;

/// <summary>
/// 异步加载管理器
/// </summary>
public class AsyncLoadManager
{
    private readonly ILogger<AsyncLoadManager> _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _loadingTasks;
    private readonly SemaphoreSlim _loadingSemaphore;
    private readonly Timer _cleanupTimer;
    
    // 配置参数
    private const int MAX_CONCURRENT_LOADS = 3;
    private const int CLEANUP_INTERVAL_MS = 30000; // 30秒
    private const int LOAD_TIMEOUT_MS = 10000; // 10秒超时

    public AsyncLoadManager(ILogger<AsyncLoadManager> logger)
    {
        _logger = logger;
        _loadingTasks = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        _loadingSemaphore = new SemaphoreSlim(MAX_CONCURRENT_LOADS, MAX_CONCURRENT_LOADS);
        
        // 启动清理定时器
        _cleanupTimer = new Timer(CleanupCompletedTasks, null, 
            TimeSpan.FromMilliseconds(CLEANUP_INTERVAL_MS), 
            TimeSpan.FromMilliseconds(CLEANUP_INTERVAL_MS));
    }

    /// <summary>
    /// 异步加载节点子项
    /// </summary>
    public async Task<bool> LoadChildrenAsync(JsonTreeNode node, CancellationToken cancellationToken = default)
    {
        if (node == null || node.IsLoaded || node.IsLoading)
            return node?.IsLoaded ?? false;

        var nodeId = node.GetNodeId();
        
        // 检查是否已经在加载中
        if (_loadingTasks.TryGetValue(nodeId, out var existingTask))
        {
            try
            {
                return await existingTask.Task.WaitAsync(TimeSpan.FromMilliseconds(LOAD_TIMEOUT_MS), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("节点加载超时: {NodeId}", nodeId);
                return false;
            }
        }

        // 创建新的加载任务
        var taskCompletionSource = new TaskCompletionSource<bool>();
        if (!_loadingTasks.TryAdd(nodeId, taskCompletionSource))
        {
            // 如果添加失败，说明其他线程已经开始加载
            if (_loadingTasks.TryGetValue(nodeId, out var concurrentTask))
            {
                try
                {
                    return await concurrentTask.Task.WaitAsync(TimeSpan.FromMilliseconds(LOAD_TIMEOUT_MS), cancellationToken);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("节点加载超时: {NodeId}", nodeId);
                    return false;
                }
            }
            return false;
        }

        try
        {
            // 等待信号量
            await _loadingSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                node.IsLoading = true;
                _logger.LogDebug("开始加载节点子项: {NodeId}", nodeId);
                
                // 模拟异步加载过程
                await Task.Run(async () =>
                {
                    await Task.Delay(50, cancellationToken); // 模拟加载延迟
                    
                    // 实际的子项加载逻辑
                    await LoadChildrenInternal(node, cancellationToken);
                    
                }, cancellationToken);
                
                node.IsLoaded = true;
                node.IsLoading = false;
                
                _logger.LogDebug("节点子项加载完成: {NodeId}, 子项数量: {ChildCount}", nodeId, node.Children.Count);
                
                taskCompletionSource.SetResult(true);
                return true;
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            node.IsLoading = false;
            _logger.LogDebug("节点加载被取消: {NodeId}", nodeId);
            taskCompletionSource.SetCanceled(cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            node.IsLoading = false;
            _logger.LogError(ex, "节点加载失败: {NodeId}", nodeId);
            taskCompletionSource.SetException(ex);
            return false;
        }
        finally
        {
            // 清理任务
            _loadingTasks.TryRemove(nodeId, out _);
        }
    }

    /// <summary>
    /// 内部子项加载逻辑
    /// </summary>
    private async Task LoadChildrenInternal(JsonTreeNode node, CancellationToken cancellationToken)
    {
        // 如果节点已经有子项，直接返回
        if (node.Children.Any())
            return;

        // 根据节点类型加载子项
        switch (node.ValueType)
        {
            case JsonValueType.Object:
                await LoadObjectChildren(node, cancellationToken);
                break;
                
            case JsonValueType.Array:
                await LoadArrayChildren(node, cancellationToken);
                break;
                
            default:
                // 叶子节点，无需加载子项
                break;
        }
    }

    /// <summary>
    /// 加载对象类型节点的子项
    /// </summary>
    private async Task LoadObjectChildren(JsonTreeNode node, CancellationToken cancellationToken)
    {
        // 这里应该根据实际的JSON数据来加载子项
        // 由于我们在JsonTreeNode中已经预先加载了子项，这里主要是处理懒加载逻辑
        
        if (node.LazyChildren != null)
        {
            var children = new List<JsonTreeNode>();
            
            foreach (var lazyChild in node.LazyChildren)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 创建子节点
                var childNode = lazyChild();
                children.Add(childNode);
                
                // 每处理10个子项就让出一次控制权
                if (children.Count % 10 == 0)
                {
                    await Task.Yield();
                }
            }
            
            node.SetChildren(children);
            node.LazyChildren = null; // 清理懒加载函数
        }
    }

    /// <summary>
    /// 加载数组类型节点的子项
    /// </summary>
    private async Task LoadArrayChildren(JsonTreeNode node, CancellationToken cancellationToken)
    {
        // 类似于对象类型的处理
        if (node.LazyChildren != null)
        {
            var children = new List<JsonTreeNode>();
            
            foreach (var lazyChild in node.LazyChildren)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var childNode = lazyChild();
                children.Add(childNode);
                
                if (children.Count % 10 == 0)
                {
                    await Task.Yield();
                }
            }
            
            node.SetChildren(children);
            node.LazyChildren = null;
        }
    }

    /// <summary>
    /// 预加载节点的直接子项
    /// </summary>
    public async Task PreloadDirectChildrenAsync(JsonTreeNode node, CancellationToken cancellationToken = default)
    {
        if (node == null || node.IsLoaded)
            return;

        try
        {
            await LoadChildrenAsync(node, cancellationToken);
            
            // 预加载第一级子项
            var preloadTasks = node.Children
                .Take(5) // 只预加载前5个子项
                .Select(child => LoadChildrenAsync(child, cancellationToken));
                
            await Task.WhenAll(preloadTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预加载节点子项失败: {NodeId}", node.GetNodeId());
        }
    }

    /// <summary>
    /// 批量加载多个节点
    /// </summary>
    public async Task<Dictionary<string, bool>> LoadMultipleNodesAsync(
        IEnumerable<JsonTreeNode> nodes, 
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();
        var loadTasks = nodes.Select(async node =>
        {
            var nodeId = node.GetNodeId();
            try
            {
                var success = await LoadChildrenAsync(node, cancellationToken);
                return new { NodeId = nodeId, Success = success };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量加载节点失败: {NodeId}", nodeId);
                return new { NodeId = nodeId, Success = false };
            }
        });

        var loadResults = await Task.WhenAll(loadTasks);
        
        foreach (var result in loadResults)
        {
            results[result.NodeId] = result.Success;
        }

        return results;
    }

    /// <summary>
    /// 取消所有正在进行的加载任务
    /// </summary>
    public void CancelAllLoading()
    {
        _logger.LogInformation("取消所有正在进行的加载任务");
        
        foreach (var task in _loadingTasks.Values)
        {
            try
            {
                task.SetCanceled();
            }
            catch (InvalidOperationException)
            {
                // 任务可能已经完成，忽略异常
            }
        }
        
        _loadingTasks.Clear();
    }

    /// <summary>
    /// 获取当前加载状态
    /// </summary>
    public LoadingStatus GetLoadingStatus()
    {
        return new LoadingStatus
        {
            ActiveLoadingTasks = _loadingTasks.Count,
            AvailableSlots = _loadingSemaphore.CurrentCount,
            MaxConcurrentLoads = MAX_CONCURRENT_LOADS
        };
    }

    /// <summary>
    /// 清理已完成的任务
    /// </summary>
    private void CleanupCompletedTasks(object? state)
    {
        var completedTasks = _loadingTasks
            .Where(kvp => kvp.Value.Task.IsCompleted)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var taskId in completedTasks)
        {
            _loadingTasks.TryRemove(taskId, out _);
        }

        if (completedTasks.Count > 0)
        {
            _logger.LogDebug("清理了 {Count} 个已完成的加载任务", completedTasks.Count);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _loadingSemaphore?.Dispose();
        CancelAllLoading();
    }
}

/// <summary>
/// 加载状态信息
/// </summary>
public class LoadingStatus
{
    public int ActiveLoadingTasks { get; set; }
    public int AvailableSlots { get; set; }
    public int MaxConcurrentLoads { get; set; }
    
    public bool IsFullyLoaded => ActiveLoadingTasks == 0;
    public double LoadingProgress => MaxConcurrentLoads > 0 
        ? (double)(MaxConcurrentLoads - AvailableSlots) / MaxConcurrentLoads 
        : 0.0;
}
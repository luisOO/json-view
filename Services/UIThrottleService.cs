using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace JsonViewer.Services;

/// <summary>
/// UI更新节流服务，用于减少频繁的UI更新和重绘
/// </summary>
public class UIThrottleService
{
    private readonly ConcurrentDictionary<string, ThrottleInfo> _throttleInfos = new();
    private readonly DispatcherTimer _timer;
    private const int DefaultThrottleMs = 16; // 约60FPS

    public UIThrottleService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(DefaultThrottleMs)
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// 节流执行UI更新操作
    /// </summary>
    /// <param name="key">操作的唯一标识</param>
    /// <param name="action">要执行的UI操作</param>
    /// <param name="throttleMs">节流间隔（毫秒）</param>
    public void ThrottleUIUpdate(string key, Action action, int throttleMs = DefaultThrottleMs)
    {
        var throttleInfo = _throttleInfos.AddOrUpdate(key, 
            new ThrottleInfo { Action = action, LastExecuted = DateTime.MinValue, ThrottleMs = throttleMs },
            (k, existing) => 
            {
                existing.Action = action;
                existing.ThrottleMs = throttleMs;
                return existing;
            });

        var now = DateTime.UtcNow;
        if ((now - throttleInfo.LastExecuted).TotalMilliseconds >= throttleMs)
        {
            // 立即执行
            ExecuteAction(key, throttleInfo);
        }
        else
        {
            // 标记为待执行
            throttleInfo.IsPending = true;
            
            // 启动定时器（如果尚未启动）
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }
    }

    /// <summary>
    /// 异步节流执行UI更新操作
    /// </summary>
    /// <param name="key">操作的唯一标识</param>
    /// <param name="action">要执行的UI操作</param>
    /// <param name="throttleMs">节流间隔（毫秒）</param>
    public async Task ThrottleUIUpdateAsync(string key, Func<Task> action, int throttleMs = DefaultThrottleMs)
    {
        var throttleInfo = _throttleInfos.AddOrUpdate(key, 
            new ThrottleInfo { AsyncAction = action, LastExecuted = DateTime.MinValue, ThrottleMs = throttleMs },
            (k, existing) => 
            {
                existing.AsyncAction = action;
                existing.ThrottleMs = throttleMs;
                return existing;
            });

        var now = DateTime.UtcNow;
        if ((now - throttleInfo.LastExecuted).TotalMilliseconds >= throttleMs)
        {
            // 立即执行
            await ExecuteActionAsync(key, throttleInfo);
        }
        else
        {
            // 标记为待执行
            throttleInfo.IsPending = true;
            
            // 启动定时器（如果尚未启动）
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }
    }

    /// <summary>
    /// 立即执行所有待执行的操作
    /// </summary>
    public void FlushAll()
    {
        foreach (var kvp in _throttleInfos)
        {
            if (kvp.Value.IsPending)
            {
                if (kvp.Value.AsyncAction != null)
                {
                    _ = ExecuteActionAsync(kvp.Key, kvp.Value);
                }
                else
                {
                    ExecuteAction(kvp.Key, kvp.Value);
                }
            }
        }
    }

    /// <summary>
    /// 清理指定的节流操作
    /// </summary>
    /// <param name="key">操作的唯一标识</param>
    public void Clear(string key)
    {
        _throttleInfos.TryRemove(key, out _);
    }

    /// <summary>
    /// 清理所有节流操作
    /// </summary>
    public void ClearAll()
    {
        _throttleInfos.Clear();
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var hasExecuted = false;

        foreach (var kvp in _throttleInfos)
        {
            var throttleInfo = kvp.Value;
            if (throttleInfo.IsPending && 
                (now - throttleInfo.LastExecuted).TotalMilliseconds >= throttleInfo.ThrottleMs)
            {
                if (throttleInfo.AsyncAction != null)
                {
                    _ = ExecuteActionAsync(kvp.Key, throttleInfo);
                }
                else
                {
                    ExecuteAction(kvp.Key, throttleInfo);
                }
                hasExecuted = true;
            }
        }

        // 如果没有待执行的操作，停止定时器
        if (!hasExecuted || !_throttleInfos.Values.Any(info => info.IsPending))
        {
            _timer.Stop();
        }
    }

    private void ExecuteAction(string key, ThrottleInfo throttleInfo)
    {
        try
        {
            throttleInfo.Action?.Invoke();
            throttleInfo.LastExecuted = DateTime.UtcNow;
            throttleInfo.IsPending = false;
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出，避免影响其他操作
        }
    }

    private async Task ExecuteActionAsync(string key, ThrottleInfo throttleInfo)
    {
        try
        {
            if (throttleInfo.AsyncAction != null)
            {
                await throttleInfo.AsyncAction();
            }
            throttleInfo.LastExecuted = DateTime.UtcNow;
            throttleInfo.IsPending = false;
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出，避免影响其他操作
        }
    }

    private class ThrottleInfo
    {
        public Action? Action { get; set; }
        public Func<Task>? AsyncAction { get; set; }
        public DateTime LastExecuted { get; set; }
        public int ThrottleMs { get; set; }
        public bool IsPending { get; set; }
    }

    public void Dispose()
    {
        _timer?.Stop();
        ClearAll();
    }
}
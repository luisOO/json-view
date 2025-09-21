using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;

namespace JsonViewer.Models;

/// <summary>
/// JSON树节点模型，支持懒加载和虚拟化
/// </summary>
public class JsonTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _childrenLoaded;
    private ObservableCollection<JsonTreeNode>? _children;
    private string? _displayValue;
    private bool _suppressNotifications;
    private readonly HashSet<string> _pendingNotifications = new();

    public JsonTreeNode()
    {
        _children = new ObservableCollection<JsonTreeNode>();
        _children.CollectionChanged += OnChildrenCollectionChanged;
    }

    /// <summary>
    /// 节点键名
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 节点值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// JSON值类型
    /// </summary>
    public JsonValueKind ValueKind { get; set; }

    /// <summary>
    /// JSON值类型（自定义枚举）
    /// </summary>
    public JsonValueType ValueType { get; set; }

    /// <summary>
    /// 原始值（用于保存解析后的原始数据）
    /// </summary>
    public object? RawValue { get; set; }

    /// <summary>
    /// 在文件中的位置（用于懒加载）
    /// </summary>
    public long FilePosition { get; set; }

    /// <summary>
    /// 节点层级深度
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 父节点引用
    /// </summary>
    public JsonTreeNode? Parent { get; set; }

    /// <summary>
    /// 是否有子节点
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// 懒加载函数 - 新的懒加载机制
    /// </summary>
    public Func<Task>? LazyLoadFunction { get; set; }

    /// <summary>
    /// 懒加载子节点函数集合（保留兼容性）
    /// </summary>
    public List<Func<JsonTreeNode>>? LazyChildren { get; set; }

    /// <summary>
    /// 子节点集合
    /// </summary>
    public ObservableCollection<JsonTreeNode> Children
    {
        get => _children ??= new ObservableCollection<JsonTreeNode>();
        set
        {
            if (_children != null)
            {
                _children.CollectionChanged -= OnChildrenCollectionChanged;
            }
            _children = value;
            if (_children != null)
            {
                _children.CollectionChanged += OnChildrenCollectionChanged;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CountDisplay));
        }
    }

    /// <summary>
    /// 是否已加载子节点
    /// </summary>
    public bool ChildrenLoaded
    {
        get => _childrenLoaded;
        set
        {
            _childrenLoaded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否已加载（ChildrenLoaded的别名）
    /// </summary>
    public bool IsLoaded
    {
        get => _childrenLoaded;
        set
        {
            _childrenLoaded = value;
            OnPropertyChanged();
        }
    }

    private bool _isLoading;
    /// <summary>
    /// 是否正在加载
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否展开
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                
                // 展开时触发懒加载
                if (value && !_childrenLoaded && HasChildren)
                {
                    _ = LoadChildrenAsync();
                }
            }
        }
    }

    /// <summary>
    /// 是否选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 显示值（格式化后的值）
    /// </summary>
    public string DisplayValue
    {
        get
        {
            if (_displayValue != null)
                return _displayValue;

            _displayValue = ValueKind switch
            {
                JsonValueKind.String => $"\"{Value}\"",
                JsonValueKind.Number => Value?.ToString() ?? "0",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Object => HasChildren ? $"{{ {Children.Count} items }}" : "{}",
                JsonValueKind.Array => HasChildren ? $"[ {Children.Count} items ]" : "[]",
                _ => Value?.ToString() ?? string.Empty
            };

            return _displayValue;
        }
        set
        {
            _displayValue = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 异步加载子节点 - 优化版本
    /// </summary>
    public async Task LoadChildrenAsync()
    {
        if (_childrenLoaded || IsLoading) return;

        try
        {
            IsLoading = true;


            // 使用新的懒加载函数
            if (LazyLoadFunction != null)
            {
                await LazyLoadFunction();

                return;
            }

            // 兼容旧的懒加载机制
            if (LazyChildren != null && LazyChildren.Count > 0)
            {
                Children.Clear();
                
                // 分批加载以避免UI卡顿
                const int batchSize = 50;
                for (int i = 0; i < LazyChildren.Count; i += batchSize)
                {
                    var batch = LazyChildren.Skip(i).Take(batchSize);
                    foreach (var lazyChild in batch)
                    {
                        var child = lazyChild();
                        child.Parent = this;
                        Children.Add(child);
                    }
                    
                    // 让出控制权
                    await Task.Delay(1);
                }
                
                LazyChildren.Clear();
                _childrenLoaded = true;

            }
        }
        finally
        {
            IsLoading = false;
            
            // 强制通知UI更新
            OnPropertyChanged(nameof(Children));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CountDisplay));
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    /// <summary>
    /// 类型信息显示
    /// </summary>
    public string TypeInfo => ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        _ => "unknown"
    };

    /// <summary>
    /// 子节点数量显示
    /// </summary>
    public string CountDisplay
    {
        get
        {
            if (ValueKind == JsonValueKind.Object && HasChildren)
                return $"({Children.Count} items)";
            if (ValueKind == JsonValueKind.Array && HasChildren)
                return $"({Children.Count} items)";
            return string.Empty;
        }
    }

    /// <summary>
    /// 子节点数量
    /// </summary>
    public int Count => Children.Count;

    /// <summary>
    /// 节点路径
    /// </summary>
    public string Path => GetPath();

    /// <summary>
    /// 获取节点路径
    /// </summary>
    public string GetPath()
    {
        var path = new List<string>();
        var current = this;
        
        while (current?.Parent != null)
        {
            path.Insert(0, current.Key);
            current = current.Parent;
        }
        
        return string.Join(".", path);
    }



    /// <summary>
    /// 清理子节点（用于内存优化）
    /// </summary>
    public void ClearChildren()
    {
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.ClearChildren();
            }
            _children.Clear();
        }
        _childrenLoaded = false;
    }

    /// <summary>
    /// 搜索匹配
    /// </summary>
    public bool MatchesSearch(string searchTerm, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        return Key.Contains(searchTerm, comparison) || 
               DisplayValue.Contains(searchTerm, comparison);
    }

    /// <summary>
    /// 获取节点ID（用于缓存和搜索）
    /// </summary>
    public string GetNodeId()
    {
        return GetPath();
    }

    /// <summary>
    /// 设置子节点
    /// </summary>
    public void SetChildren(IEnumerable<JsonTreeNode> children)
    {
        Children.Clear();
        foreach (var child in children)
        {
            Children.Add(child);
            child.Parent = this;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 子节点集合变化事件处理
    /// </summary>
    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotifications)
        {
            _pendingNotifications.Add(nameof(Count));
            _pendingNotifications.Add(nameof(CountDisplay));
            _pendingNotifications.Add(nameof(DisplayValue));
            return;
        }
        
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(DisplayValue));
    }

    /// <summary>
    /// 开始批量更新，暂停属性变更通知
    /// </summary>
    public void BeginBatchUpdate()
    {
        _suppressNotifications = true;
        _pendingNotifications.Clear();
    }

    /// <summary>
    /// 结束批量更新，发送所有挂起的属性变更通知
    /// </summary>
    public void EndBatchUpdate()
    {
        _suppressNotifications = false;
        
        foreach (var propertyName in _pendingNotifications)
        {
            OnPropertyChanged(propertyName);
        }
        
        _pendingNotifications.Clear();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (_suppressNotifications && propertyName != null)
        {
            _pendingNotifications.Add(propertyName);
            return;
        }
        
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 缩进边距（用于VirtualizingTreeView）
    /// </summary>
    public Thickness IndentMargin => new Thickness(Level * 20, 0, 0, 0);

    /// <summary>
    /// 展开/折叠图标
    /// </summary>
    public string ExpanderIcon => IsExpanded ? "▼" : "▶";

    /// <summary>
    /// 节点图标
    /// </summary>
    public string Icon => ValueKind switch
    {
        JsonValueKind.Object => "📁",
        JsonValueKind.Array => "📋",
        JsonValueKind.String => "📝",
        JsonValueKind.Number => "🔢",
        JsonValueKind.True or JsonValueKind.False => "☑",
        JsonValueKind.Null => "∅",
        _ => "❓"
    };

    public override string ToString()
    {
        return $"{Key}: {DisplayValue} ({TypeInfo})";
    }
}
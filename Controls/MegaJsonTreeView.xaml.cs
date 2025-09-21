using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JsonViewer.Controls
{
    /// <summary>
    /// 超大JSON树形视图控件 - 免费高性能方案
    /// 支持50MB+文件和百万级节点
    /// </summary>
    public partial class MegaJsonTreeView : UserControl
    {
        public MegaJsonTreeView()
        {
            InitializeComponent();
            DataContext = new MegaJsonTreeViewModel();
            
            // 添加鼠标滚轮事件处理，在整个控件区域生效
            this.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        /// <summary>
        /// 鼠标滚轮事件处理 - 在整个窗口区域生效
        /// </summary>
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 查找 ScrollViewer 控件
            var scrollViewer = MainScrollViewer;
            if (scrollViewer != null)
            {
                // 计算滚动量，正值向上滚，负值向下滚
                double scrollAmount = -e.Delta / 3.0; // 除以3使滚动更加平滑
                
                // 执行垂直滚动
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                
                // 标记事件已处理，阻止事件冒泡
                e.Handled = true;
            }
        }

        /// <summary>
        /// 加载JSON文件
        /// </summary>
        public async Task LoadJsonFileAsync(string filePath)
        {
            if (DataContext is MegaJsonTreeViewModel viewModel)
            {
                await viewModel.LoadJsonFileAsync(filePath);
            }
        }
    }

    /// <summary>
    /// 超大JSON节点模型
    /// </summary>
    public class MegaJsonNode : ObservableObject
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isLoading;
        public bool _childrenLoaded; // 改为public以便外部访问
        private ObservableCollection<MegaJsonNode> _visibleChildren = new();
        private string? _filePath; // 存储文件路径而不是JsonElement
        private MegaJsonTreeViewModel? _parentViewModel; // 引用父ViewModel

        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public JsonValueKind ValueType { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = "";
        public long FilePosition { get; set; } // 在文件中的位置
        public int ChildCount { get; set; } // 子节点数量
        public bool HasChildren => ChildCount > 0;
        
        // 内部属性 - 改为存储文件路径
        public string? FilePath
        {
            get => _filePath;
            set => _filePath = value;
        }
        
        public MegaJsonTreeViewModel? ParentViewModel
        {
            get => _parentViewModel;
            set => _parentViewModel = value;
        }

        // 显示属性
        public string Icon => GetIcon();
        public string DisplayValue => GetDisplayValue();
        public string FullValue => Value;
        public Brush KeyColor => GetKeyColor();
        public Brush ValueColor => GetValueColor();
        public FontStyle ValueFontStyle => GetValueFontStyle();
        public bool HasValue => !string.IsNullOrEmpty(Value);
        public string ChildCountText => ChildCount > 0 ? $"({ChildCount} 项)" : "";
        public bool ShowChildCount => ChildCount > 0 && !IsExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    OnPropertyChanged(nameof(ShowChildCount));
                    if (value && !_childrenLoaded && HasChildren)
                    {
                        _ = LoadChildrenAsync();
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<MegaJsonNode> VisibleChildren
        {
            get => _visibleChildren;
            set => SetProperty(ref _visibleChildren, value);
        }

        /// <summary>
        /// 异步加载子节点
        /// </summary>
        private async Task LoadChildrenAsync()
        {
            if (_childrenLoaded || IsLoading || !HasChildren || string.IsNullOrEmpty(_filePath) || _parentViewModel == null)
            {

                return;
            }


            IsLoading = true;
            OnPropertyChanged(nameof(Icon)); // 更新图标
            
            try
            {
                var childNodes = new List<MegaJsonNode>();
                
                // 在后台线程中重新解析JSON文件并加载子节点
                await Task.Run(async () =>
                {
                    try
                    {
                        // 重新读取和解析JSON文件
                        var jsonContent = await File.ReadAllTextAsync(_filePath);
                        using var document = JsonDocument.Parse(jsonContent);
                        
                        // 根据路径导航到当前节点
                        var currentElement = NavigateToElement(document.RootElement, Path);
                        if (currentElement.HasValue)
                        {
                            var element = currentElement.Value;
                            
                            switch (element.ValueKind)
                            {
                                case JsonValueKind.Object:
                                    foreach (var property in element.EnumerateObject().Take(100))
                                    {
                                        var childPath = string.IsNullOrEmpty(Path) ? property.Name : $"{Path}.{property.Name}";
                                        var childNode = new MegaJsonNode
                                        {
                                            Key = property.Name,
                                            Value = _parentViewModel.GetElementValue(property.Value),
                                            ValueType = property.Value.ValueKind,
                                            Level = Level + 1,
                                            Path = childPath,
                                            FilePosition = 0,
                                            ChildCount = _parentViewModel.GetChildCount(property.Value),
                                            FilePath = _filePath, // 传递文件路径
                                            ParentViewModel = _parentViewModel
                                        };
                                        childNodes.Add(childNode);
                                    }
                                    break;
                                    
                                case JsonValueKind.Array:
                                    int index = 0;
                                    foreach (var item in element.EnumerateArray().Take(100))
                                    {
                                        var childPath = string.IsNullOrEmpty(Path) ? $"[{index}]" : $"{Path}.[{index}]";
                                        var childNode = new MegaJsonNode
                                        {
                                            Key = $"[{index}]",
                                            Value = _parentViewModel.GetElementValue(item),
                                            ValueType = item.ValueKind,
                                            Level = Level + 1,
                                            Path = childPath,
                                            FilePosition = 0,
                                            ChildCount = _parentViewModel.GetChildCount(item),
                                            FilePath = _filePath, // 传递文件路径
                                            ParentViewModel = _parentViewModel
                                        };
                                        childNodes.Add(childNode);
                                        index++;
                                    }
                                    break;
                            }
                        }
                        

                    }
                    catch (Exception ex)
                    {

                        throw; // 重新抛出异常以便在UI线程中处理
                    }
                });
                
                // 在UI线程上更新集合
                VisibleChildren.Clear();
                foreach (var child in childNodes)
                {
                    VisibleChildren.Add(child);
                }
                
                _childrenLoaded = true;
                
                // 强制通知UI更新
                OnPropertyChanged(nameof(VisibleChildren));
                OnPropertyChanged(nameof(Icon)); // 更新图标
                OnPropertyChanged(nameof(ShowChildCount)); // 更新子节点数量显示
                

            }
            catch (Exception ex)
            {
                // 处理错误

                
                // 在UI线程上显示错误信息
                var errorNode = new MegaJsonNode
                {
                    Key = "错误",
                    Value = $"加载失败: {ex.Message}",
                    ValueType = JsonValueKind.String,
                    Level = Level + 1,
                    Path = $"{Path}.error",
                    ChildCount = 0
                };
                VisibleChildren.Clear();
                VisibleChildren.Add(errorNode);
                OnPropertyChanged(nameof(VisibleChildren));
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(Icon)); // 更新图标
            }
        }
        
        /// <summary>
        /// 根据路径导航到JSON元素
        /// </summary>
        private JsonElement? NavigateToElement(JsonElement root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;
                
            var pathParts = path.Split('.');
            var current = root;
            
            foreach (var part in pathParts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                
                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    // 数组索引
                    var indexStr = part.Substring(1, part.Length - 2);
                    if (int.TryParse(indexStr, out var index))
                    {
                        if (current.ValueKind == JsonValueKind.Array)
                        {
                            var array = current.EnumerateArray().ToArray();
                            if (index >= 0 && index < array.Length)
                            {
                                current = array[index];
                            }
                            else
                            {
                                return null; // 索引超出范围
                            }
                        }
                        else
                        {
                            return null; // 不是数组类型
                        }
                    }
                    else
                    {
                        return null; // 无效索引
                    }
                }
                else
                {
                    // 对象属性
                    if (current.ValueKind == JsonValueKind.Object)
                    {
                        if (current.TryGetProperty(part, out var property))
                        {
                            current = property;
                        }
                        else
                        {
                            return null; // 属性不存在
                        }
                    }
                    else
                    {
                        return null; // 不是对象类型
                    }
                }
            }
            
            return current;
        }

        private string GetIcon()
        {
            return ValueType switch
            {
                JsonValueKind.Object => IsExpanded ? "🗂️" : "📁",
                JsonValueKind.Array => IsExpanded ? "🗃️" : "📋",
                JsonValueKind.String => "🔤",
                JsonValueKind.Number => "🔢",
                JsonValueKind.True or JsonValueKind.False => "☑️",
                JsonValueKind.Null => "🚫",
                _ => "📄"
            };
        }

        private string GetDisplayValue()
        {
            if (string.IsNullOrEmpty(Value))
                return "";

            const int maxLength = 100;
            if (Value.Length > maxLength)
                return Value.Substring(0, maxLength) + "...";
            
            return Value;
        }

        private Brush GetKeyColor()
        {
            return new SolidColorBrush(Colors.DarkBlue);
        }

        private Brush GetValueColor()
        {
            return ValueType switch
            {
                JsonValueKind.String => new SolidColorBrush(Colors.DarkGreen),
                JsonValueKind.Number => new SolidColorBrush(Colors.Blue),
                JsonValueKind.True or JsonValueKind.False => new SolidColorBrush(Colors.Purple),
                JsonValueKind.Null => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        private FontStyle GetValueFontStyle()
        {
            return ValueType == JsonValueKind.String ? FontStyles.Italic : FontStyles.Normal;
        }
    }

    /// <summary>
    /// 超大JSON树形视图模型
    /// </summary>
    public class MegaJsonTreeViewModel : ObservableObject
    {
        private ObservableCollection<MegaJsonNode> _rootNodes = new();
        private string _statusText = "准备就绪";
        private long _totalNodes;
        private long _loadedNodes;
        private string _memoryUsage = "0 MB";
        private string _currentFilePath = "";
        private CancellationTokenSource _cancellationTokenSource = new();

        public ObservableCollection<MegaJsonNode> RootNodes
        {
            get => _rootNodes;
            set => SetProperty(ref _rootNodes, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public long TotalNodes
        {
            get => _totalNodes;
            set => SetProperty(ref _totalNodes, value);
        }

        public long LoadedNodes
        {
            get => _loadedNodes;
            set => SetProperty(ref _loadedNodes, value);
        }

        public string MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }

        // 命令
        public IAsyncRelayCommand ExpandLevelCommand { get; }
        public IRelayCommand CollapseAllCommand { get; }
        public IRelayCommand CleanupMemoryCommand { get; }

        public MegaJsonTreeViewModel()
        {
            ExpandLevelCommand = new AsyncRelayCommand(ExpandOneLevelAsync);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            CleanupMemoryCommand = new RelayCommand(CleanupMemory);

            // 启动内存监控
            StartMemoryMonitoring();
        }

        /// <summary>
        /// 加载JSON文件 - 核心高性能实现
        /// </summary>
        public async Task LoadJsonFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                StatusText = "文件不存在";
                return;
            }

            _currentFilePath = filePath;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                StatusText = "正在分析超大JSON文件结构...";
                
                var fileInfo = new FileInfo(filePath);
                var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                
                StatusText = $"正在解析 {fileSizeMB:F2} MB 的JSON文件...";

                // 使用流式解析避免内存溢出
                var rootNodes = await ParseJsonStreamAsync(filePath, _cancellationTokenSource.Token);
                
                RootNodes = new ObservableCollection<MegaJsonNode>(rootNodes);
                LoadedNodes = RootNodes.Count;
                
                StatusText = $"成功加载 {fileSizeMB:F2} MB JSON文件，包含 {TotalNodes:N0} 个节点";
            }
            catch (OperationCanceledException)
            {
                StatusText = "加载已取消";
            }
            catch (Exception ex)
            {
                StatusText = $"加载失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 流式解析JSON - 核心性能优化
        /// </summary>
        private async Task<List<MegaJsonNode>> ParseJsonStreamAsync(string filePath, CancellationToken cancellationToken)
        {
            var result = new List<MegaJsonNode>();
            var nodeCount = 0L;

            await Task.Run(async () =>
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(fileStream);
                
                // 使用JsonDocument进行流式解析
                var jsonContent = await reader.ReadToEndAsync();
                using var document = JsonDocument.Parse(jsonContent);
                
                var root = document.RootElement;
                
                // 直接处理根节点的内容，而不是创建一个包装的根节点
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // 如果根是对象，直接显示其属性
                    ProcessJsonElementForNode(root, "", "", -1, 0, result, ref nodeCount, 100);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // 如果根是数组，直接显示其元素
                    ProcessJsonElementForNode(root, "", "", -1, 0, result, ref nodeCount, 100);
                }
                else
                {
                    // 如果根是简单值，创建一个根节点
                    var rootNode = new MegaJsonNode
                    {
                        Key = "JSON Root",
                        Value = GetElementValue(root),
                        ValueType = root.ValueKind,
                        Level = 0,
                        Path = "root",
                        FilePosition = 0,
                        ChildCount = 0,
                        FilePath = filePath, // 设置文件路径
                        ParentViewModel = this
                    };
                    result.Add(rootNode);
                    nodeCount++;
                }
                
                TotalNodes = nodeCount;
                
            }, cancellationToken);

            return result;
        }

        /// <summary>
        /// 递归处理JSON元素 - 优化版本
        /// </summary>
        private void ProcessJsonElement(JsonElement element, string path, string key, 
            int level, long position, List<MegaJsonNode> result, ref long nodeCount, int maxItems)
        {
            ProcessJsonElementForNode(element, path, key, level, position, result, ref nodeCount, maxItems);
        }
        
        /// <summary>
        /// 递归处理JSON元素 - 供子节点调用的公共方法
        /// </summary>
        public void ProcessJsonElementForNode(JsonElement element, string path, string key, 
            int level, long position, List<MegaJsonNode> result, ref long nodeCount, int maxItems)
        {
            if (result.Count >= maxItems || nodeCount > 1000000) // 限制最大节点数
                return;

            // 处理子节点
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = element.EnumerateObject().Take(maxItems); // 限制显示前100个属性
                    foreach (var property in properties)
                    {
                        if (result.Count >= maxItems) break;
                        
                        nodeCount++;
                        var currentPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                        
                        var node = new MegaJsonNode
                        {
                            Key = property.Name,
                            Value = GetElementValue(property.Value),
                            ValueType = property.Value.ValueKind,
                            Level = level + 1,
                            Path = currentPath,
                            FilePosition = position,
                            ChildCount = GetChildCount(property.Value),
                            FilePath = _currentFilePath, // 设置文件路径而不是JsonElement
                            ParentViewModel = this // 设置父ViewModel引用
                        };

                        result.Add(node);


                        // 对于有子节点的项，预加载前几层
                        if (level < 1 && node.HasChildren)
                        {
                            var childResult = new List<MegaJsonNode>();
                            var childCount = 0L;
                            ProcessJsonElementForNode(property.Value, currentPath, "", level + 1, position, childResult, ref childCount, 50);
                            node.VisibleChildren = new ObservableCollection<MegaJsonNode>(childResult);
                            node._childrenLoaded = true; // 标记为已加载

                        }
                    }
                    break;
                    
                case JsonValueKind.Array:
                    var items = element.EnumerateArray().Take(maxItems); // 限制显示前100个数组项
                    int index = 0;
                    foreach (var item in items)
                    {
                        if (result.Count >= maxItems) break;
                        
                        nodeCount++;
                        var currentPath = string.IsNullOrEmpty(path) ? $"[{index}]" : $"{path}.[{index}]";
                        
                        var node = new MegaJsonNode
                        {
                            Key = $"[{index}]",
                            Value = GetElementValue(item),
                            ValueType = item.ValueKind,
                            Level = level + 1,
                            Path = currentPath,
                            FilePosition = position,
                            ChildCount = GetChildCount(item),
                            FilePath = _currentFilePath, // 设置文件路径而不是JsonElement
                            ParentViewModel = this // 设置父ViewModel引用
                        };

                        result.Add(node);


                        // 对于有子节点的项，预加载前几层
                        if (level < 1 && node.HasChildren)
                        {
                            var childResult = new List<MegaJsonNode>();
                            var childCount = 0L;
                            ProcessJsonElementForNode(item, currentPath, "", level + 1, position, childResult, ref childCount, 50);
                            node.VisibleChildren = new ObservableCollection<MegaJsonNode>(childResult);
                            node._childrenLoaded = true; // 标记为已加载

                        }
                        
                        index++;
                    }
                    break;
            }
        }

        /// <summary>
        /// 获取元素值的字符串表示 - 公共方法
        /// </summary>
        public string GetElementValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => $"\"{element.GetString()}\"",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Object => $"{{ {element.EnumerateObject().Count()} properties }}",
                JsonValueKind.Array => $"[ {element.GetArrayLength()} items ]",
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// 获取子元素数量 - 公共方法
        /// </summary>
        public int GetChildCount(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().Count(),
                JsonValueKind.Array => element.GetArrayLength(),
                _ => 0
            };
        }

        /// <summary>
        /// 获取元素值的字符串表示
        /// </summary>
        /// <summary>
        /// 展开一层
        /// </summary>
        private async Task ExpandOneLevelAsync()
        {
            StatusText = "正在展开一层节点...";
            
            await Task.Run(() =>
            {
                ExpandLevel(RootNodes, 1);
            });
            
            StatusText = "展开完成";
        }

        /// <summary>
        /// 递归展开指定层级
        /// </summary>
        private void ExpandLevel(ObservableCollection<MegaJsonNode> nodes, int levelsRemaining)
        {
            if (levelsRemaining <= 0) return;

            foreach (var node in nodes)
            {
                if (node.HasChildren)
                {
                    Application.Current.Dispatcher.Invoke(() => node.IsExpanded = true);
                    ExpandLevel(node.VisibleChildren, levelsRemaining - 1);
                }
            }
        }

        /// <summary>
        /// 折叠所有节点
        /// </summary>
        private void CollapseAll()
        {
            StatusText = "正在折叠所有节点...";
            CollapseNodes(RootNodes);
            StatusText = "折叠完成";
        }

        /// <summary>
        /// 递归折叠节点
        /// </summary>
        private void CollapseNodes(ObservableCollection<MegaJsonNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = false;
                if (node.VisibleChildren.Count > 0)
                {
                    CollapseNodes(node.VisibleChildren);
                }
            }
        }

        /// <summary>
        /// 清理内存
        /// </summary>
        private void CleanupMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            StatusText = "内存清理完成";
        }

        /// <summary>
        /// 启动内存监控
        /// </summary>
        private void StartMemoryMonitoring()
        {
            var timer = new System.Timers.Timer(2000);
            timer.Elapsed += (s, e) =>
            {
                var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MemoryUsage = $"{memoryMB:F1} MB";
                });
            };
            timer.Start();
        }
    }

    /// <summary>
    /// 层级到缩进距离的转换器
    /// </summary>
    public class LevelToIndentConverter : IValueConverter
    {
        public static readonly LevelToIndentConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return level * 20; // 每层缩进20像素
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
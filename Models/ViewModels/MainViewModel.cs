using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using JsonViewer.Services;


namespace JsonViewer.Models.ViewModels;

/// <summary>
/// 主窗口视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly LargeJsonParser _jsonParser;
    private readonly AsyncLoadManager _loadManager;
    private readonly MemoryManager _memoryManager;
    private readonly JsonSearchEngine _searchEngine;
    private readonly ThemeManager _themeManager;
    private readonly UIThrottleService _uiThrottleService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private JsonTreeNode? _rootNode;

    [ObservableProperty]
    private JsonTreeNode? _selectedNode;

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _loadingProgress;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private int _totalNodes;

    [ObservableProperty]
    private string _memoryUsage = "0 MB";

    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    [ObservableProperty]
    private ObservableCollection<JsonTreeNode> _flattenedNodes = new();

    // 分页显示相关
    [ObservableProperty]
    private int _currentPageIndex = 0;
    
    [ObservableProperty]
    private int _pageSize = 50; // 每页显示数量
    
    [ObservableProperty]
    private int _totalPages = 0;
    
    [ObservableProperty]
    private bool _isPagingEnabled = false;
    
    public ICommand NextPageCommand { get; private set; }
    public ICommand PreviousPageCommand { get; private set; }
    public ICommand GoToPageCommand { get; private set; }

    // 无参数构造函数用于简单初始化
    public MainViewModel()
    {
        // 创建简单的服务实例
        _logger = new ConsoleLogger<MainViewModel>();
        _jsonParser = new LargeJsonParser(new ConsoleLogger<LargeJsonParser>());
        _loadManager = new AsyncLoadManager(new ConsoleLogger<AsyncLoadManager>());
        _memoryManager = new MemoryManager(new ConsoleLogger<MemoryManager>());
        _searchEngine = new JsonSearchEngine(new ConsoleLogger<JsonSearchEngine>());
        _themeManager = new ThemeManager(new ConsoleLogger<ThemeManager>());
        _uiThrottleService = new UIThrottleService();
        _cancellationTokenSource = new CancellationTokenSource();

        InitializeCommands();
    }

    public MainViewModel(
        LargeJsonParser jsonParser,
        AsyncLoadManager loadManager,
        MemoryManager memoryManager,
        JsonSearchEngine searchEngine,
        ThemeManager themeManager,
        UIThrottleService uiThrottleService,
        ILogger<MainViewModel> logger)
    {
        _jsonParser = jsonParser;
        _loadManager = loadManager;
        _memoryManager = memoryManager;
        _searchEngine = searchEngine;
        _themeManager = themeManager;
        _uiThrottleService = uiThrottleService;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();

        InitializeCommands();
    }

    public IAsyncRelayCommand OpenFileCommand { get; private set; }
    public IAsyncRelayCommand SaveAsCommand { get; private set; }
    public IAsyncRelayCommand SearchCommand { get; private set; }
    public IRelayCommand ClearSearchCommand { get; private set; }
    public IAsyncRelayCommand ToggleThemeCommand { get; private set; }
    public IRelayCommand ExpandAllCommand { get; private set; }
    public IRelayCommand CollapseAllCommand { get; private set; }
    public IAsyncRelayCommand RefreshCommand { get; private set; }
    public IRelayCommand CancelLoadingCommand { get; private set; }

    // 取消令牌源
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// 初始化命令
    /// </summary>
    private void InitializeCommands()
    {
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => RootNode != null);
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => RootNode != null && !string.IsNullOrWhiteSpace(SearchText));
        ClearSearchCommand = new RelayCommand(ClearSearch);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        ExpandAllCommand = new RelayCommand(ExpandAll, () => RootNode != null);
        CollapseAllCommand = new RelayCommand(CollapseAll, () => RootNode != null);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !string.IsNullOrEmpty(CurrentFilePath));
        CancelLoadingCommand = new RelayCommand(CancelLoading, () => IsLoading);
        
        // 分页命令
        NextPageCommand = new RelayCommand(NextPage, () => CurrentPageIndex < TotalPages - 1);
        PreviousPageCommand = new RelayCommand(PreviousPage, () => CurrentPageIndex > 0);
        GoToPageCommand = new RelayCommand<int>(GoToPage);
        
        // 监听属性变化
        PropertyChanged += OnPropertyChanged;
        
        // 启动内存监控
        StartMemoryMonitoring();
    }

    /// <summary>
    /// 取消加载
    /// </summary>
    private void CancelLoading()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "正在取消加载...";
            _logger.LogInformation("用户取消了文件加载操作");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消加载时发生错误");
        }
    }

    /// <summary>
    /// 刷新扁平化节点列表（用于VirtualizingTreeView）
    /// </summary>
    public void RefreshFlattenedNodes()
    {
        FlattenedNodes.Clear();
        
        if (RootNode != null)
        {
            if (IsPagingEnabled && RootNode.Children?.Count > PageSize)
            {
                // 分页显示逻辑
                RefreshPagedNodes();
            }
            else
            {
                // 原有逻辑：添加根节点
                AddNodeToFlattened(RootNode);
            }
        }
        
        // 强制通知UI更新
        OnPropertyChanged(nameof(FlattenedNodes));
    }

    /// <summary>
    /// 分页显示节点
    /// </summary>
    private void RefreshPagedNodes()
    {
        if (RootNode?.Children == null) return;

        var children = RootNode.Children.ToList();
        TotalPages = (int)Math.Ceiling((double)children.Count / PageSize);
        
        var startIndex = CurrentPageIndex * PageSize;
        var endIndex = Math.Min(startIndex + PageSize, children.Count);
        
        // 添加分页信息节点
        if (CurrentPageIndex > 0 || TotalPages > 1)
        {
            var pageInfoNode = new JsonTreeNode
            {
                Key = $"第 {CurrentPageIndex + 1} 页 / 共 {TotalPages} 页",
                Value = $"显示 {startIndex + 1}-{endIndex} / 共 {children.Count} 个节点",
                ValueType = JsonValueType.Unknown
            };
            FlattenedNodes.Add(pageInfoNode);
        }
        
        // 添加当前页的节点
        for (int i = startIndex; i < endIndex; i++)
        {
            FlattenedNodes.Add(children[i]);
        }
        
        // 添加分页导航节点
        if (TotalPages > 1)
        {
            var navNode = new JsonTreeNode
            {
                Key = "分页导航",
                Value = $"上一页 | 下一页 | 跳转到...",
                ValueType = JsonValueType.Unknown
            };
            FlattenedNodes.Add(navNode);
        }
    }

    /// <summary>
    /// 下一页
    /// </summary>
    private void NextPage()
    {
        if (CurrentPageIndex < TotalPages - 1)
        {
            CurrentPageIndex++;
            RefreshFlattenedNodes();
        }
    }

    /// <summary>
    /// 上一页
    /// </summary>
    private void PreviousPage()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            RefreshFlattenedNodes();
        }
    }

    /// <summary>
    /// 跳转到指定页
    /// </summary>
    private void GoToPage(int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex < TotalPages)
        {
            CurrentPageIndex = pageIndex;
            RefreshFlattenedNodes();
        }
    }



    /// <summary>
    /// 启用分页显示
    /// </summary>
    private void EnablePaging()
    {
        IsPagingEnabled = true;
        CurrentPageIndex = 0;
        RefreshFlattenedNodes();
    }

    /// <summary>
    /// 递归添加节点到扁平化列表
    /// </summary>
    private void AddNodeToFlattened(JsonTreeNode node)
    {
        FlattenedNodes.Add(node);
        
        if (node.IsExpanded && node.Children != null)
        {
            foreach (var child in node.Children)
            {
                AddNodeToFlattened(child);
            }
        }
    }

    /// <summary>
    /// 打开文件
    /// </summary>
    private async Task OpenFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择JSON文件",
            Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        await LoadFileAsync(openFileDialog.FileName);
    }

    /// <summary>
    /// 加载文件
    /// </summary>
    public async Task LoadFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            StatusMessage = "文件不存在";
            return;
        }

        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            StatusMessage = "正在加载文件...";
            
            var fileInfo = new FileInfo(filePath);
            FileSize = fileInfo.Length;
            var fileSizeMB = FileSize / (1024.0 * 1024.0);
            
            _logger.LogInformation("开始加载文件: {FilePath}, 大小: {FileSize} bytes", filePath, FileSize);

            // 进度报告
            var progress = new Progress<(string message, double percentage)>(p =>
            {
                LoadingProgress = p.percentage;
                StatusMessage = p.message;
            });

            // 创建新的取消令牌源
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var cancellationToken = _cancellationTokenSource.Token;
            
            // 使用传统视图加载文件
            _uiThrottleService.ThrottleUIUpdate("status", () => StatusMessage = "正在解析文件...", 100);
            
            var intProgress = new Progress<int>(value => ((IProgress<(string, double)>)progress).Report(("正在解析...", (double)value)));
            var rootNode = await _jsonParser.ParseLargeFileAsync(filePath, intProgress, cancellationToken);
            
            if (rootNode != null)
            {
                RootNode = rootNode;
                CurrentFilePath = filePath;
                TotalNodes = CountNodes(rootNode);
                
                _uiThrottleService.ThrottleUIUpdate("status", () => StatusMessage = "文件加载完成", 100);
                
                // 刷新节点显示
                Application.Current.Dispatcher.Invoke(() => {
                    RefreshFlattenedNodes();
                });
                
                // 构建搜索索引
                _uiThrottleService.ThrottleUIUpdate("status", () => StatusMessage = "正在构建搜索索引...", 100);
                await _searchEngine.BuildIndexAsync(rootNode);
            }

            StatusMessage = $"文件加载完成 - {TotalNodes} 个节点";
            LoadingProgress = 100;
            _logger.LogInformation("文件加载完成: {TotalNodes} 个节点", TotalNodes);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "加载已取消";
            _logger.LogWarning("文件加载被取消: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            _logger.LogError(ex, "加载文件失败: {FilePath}", filePath);
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// 另存为
    /// </summary>
    private async Task SaveAsAsync()
    {
        if (RootNode == null)
            return;

        var saveFileDialog = new SaveFileDialog
        {
            Title = "保存JSON文件",
            Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FilterIndex = 1,
            FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + "_exported.json"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在保存文件...";
            
            await _jsonParser.SaveToFileAsync(RootNode, saveFileDialog.FileName);
            
            StatusMessage = "文件保存成功";
            _logger.LogInformation("文件保存成功: {FilePath}", saveFileDialog.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
            _logger.LogError(ex, "保存文件失败: {FilePath}", saveFileDialog.FileName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    private async Task SearchAsync()
    {
        if (RootNode == null || string.IsNullOrWhiteSpace(SearchText))
        {
            ClearSearch();
            return;
        }

        try
        {
            StatusMessage = "正在搜索...";
            
            var results = await _searchEngine.SearchAsync(SearchText, new SearchOptions
            {
                CaseSensitive = false,
                UseRegex = false,
                SearchInKeys = true,
                SearchInValues = true
            });

            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            StatusMessage = $"找到 {SearchResults.Count} 个匹配项";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败: {ex.Message}";
            _logger.LogError(ex, "搜索失败: {SearchText}", SearchText);
        }
    }

    /// <summary>
    /// 清除搜索
    /// </summary>
    private void ClearSearch()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        StatusMessage = "就绪";
    }

    /// <summary>
    /// 切换主题
    /// </summary>
    private async Task ToggleThemeAsync()
    {
        try
        {
            IsDarkTheme = !IsDarkTheme;
            var themeName = IsDarkTheme ? ThemeType.Dark.ToString() : ThemeType.Light.ToString();
            var success = await _themeManager.ApplyThemeAsync(themeName);
            
            if (success)
            {
                StatusMessage = $"已切换到{(IsDarkTheme ? "深色" : "浅色")}主题";
                _themeManager.SaveThemeSettings();
            }
            else
            {
                // 如果切换失败，恢复原来的状态
                IsDarkTheme = !IsDarkTheme;
                StatusMessage = "主题切换失败";
            }
        }
        catch (Exception ex)
        {
            // 如果出现异常，恢复原来的状态
            IsDarkTheme = !IsDarkTheme;
            StatusMessage = $"主题切换失败: {ex.Message}";
            _logger.LogError(ex, "切换主题时发生异常");
        }
    }

    /// <summary>
    /// 展开所有节点
    /// </summary>
    private async void ExpandAll()
    {
        if (RootNode == null)
            return;

        StatusMessage = "正在展开所有节点...";
        await ExpandAllAsync(RootNode);
        StatusMessage = "已展开所有节点";
    }

    /// <summary>
    /// 折叠所有节点
    /// </summary>
    private async void CollapseAll()
    {
        if (RootNode == null)
            return;

        StatusMessage = "正在折叠所有节点...";
        await CollapseAllAsync(RootNode);
        StatusMessage = "已折叠所有节点";
    }

    /// <summary>
    /// 刷新文件
    /// </summary>
    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
            return;

        await LoadFileAsync(CurrentFilePath);
    }

    /// <summary>
    /// 异步展开所有节点
    /// </summary>
    private async Task ExpandAllAsync(JsonTreeNode rootNode)
    {
        await Task.Run(async () =>
        {
            var nodesToProcess = new Queue<JsonTreeNode>();
            nodesToProcess.Enqueue(rootNode);
            
            var processedCount = 0;
            const int batchSize = 100;
            
            while (nodesToProcess.Count > 0)
            {
                var batch = new List<JsonTreeNode>();
                
                // 收集一批节点
                for (int i = 0; i < batchSize && nodesToProcess.Count > 0; i++)
                {
                    batch.Add(nodesToProcess.Dequeue());
                }
                
                // 在UI线程上批量更新
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var node in batch)
                    {
                        node.BeginBatchUpdate();
                        node.IsExpanded = true;
                        
                        // 将子节点加入队列
                        foreach (var child in node.Children)
                        {
                            nodesToProcess.Enqueue(child);
                        }
                        
                        node.EndBatchUpdate();
                    }
                });
                
                processedCount += batch.Count;
                
                // 让出控制权，避免UI卡顿
                if (processedCount % (batchSize * 2) == 0)
                {
                    await Task.Delay(1);
                }
            }
        });
    }

    /// <summary>
    /// 异步折叠所有节点
    /// </summary>
    private async Task CollapseAllAsync(JsonTreeNode rootNode)
    {
        await Task.Run(async () =>
        {
            var nodesToProcess = new Queue<JsonTreeNode>();
            nodesToProcess.Enqueue(rootNode);
            
            var processedCount = 0;
            const int batchSize = 100;
            
            while (nodesToProcess.Count > 0)
            {
                var batch = new List<JsonTreeNode>();
                
                // 收集一批节点
                for (int i = 0; i < batchSize && nodesToProcess.Count > 0; i++)
                {
                    batch.Add(nodesToProcess.Dequeue());
                }
                
                // 在UI线程上批量更新
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var node in batch)
                    {
                        node.BeginBatchUpdate();
                        node.IsExpanded = false;
                        
                        // 将子节点加入队列
                        foreach (var child in node.Children)
                        {
                            nodesToProcess.Enqueue(child);
                        }
                        
                        node.EndBatchUpdate();
                    }
                });
                
                processedCount += batch.Count;
                
                // 让出控制权，避免UI卡顿
                if (processedCount % (batchSize * 2) == 0)
                {
                    await Task.Delay(1);
                }
            }
        });
    }

    /// <summary>
    /// 统计节点数量
    /// </summary>
    private int CountNodes(JsonTreeNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }
        return count;
    }

    /// <summary>
    /// 启动内存监控
    /// </summary>
    private void StartMemoryMonitoring()
    {
        var timer = new System.Timers.Timer(2000); // 每2秒更新一次
        timer.Elapsed += (s, e) =>
        {
            var memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                MemoryUsage = $"{memoryMB:F1} MB";
            });
        };
        timer.Start();
    }

    /// <summary>
    /// 属性变化处理
    /// </summary>
    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RootNode):
                // 更新命令状态
                SaveAsCommand?.NotifyCanExecuteChanged();
                SearchCommand?.NotifyCanExecuteChanged();
                ExpandAllCommand?.NotifyCanExecuteChanged();
                CollapseAllCommand?.NotifyCanExecuteChanged();
                // 刷新扁平化节点列表
                RefreshFlattenedNodes();
                break;
                
            case nameof(CurrentFilePath):
                RefreshCommand?.NotifyCanExecuteChanged();
                break;
                
            case nameof(SearchText):
                SearchCommand?.NotifyCanExecuteChanged();
                break;
                
            case nameof(IsLoading):
                CancelLoadingCommand?.NotifyCanExecuteChanged();
                break;
        }
    }
}
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonViewer.Controls;
using JsonViewer.Services;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace JsonViewer.Views
{
    /// <summary>
    /// 免费高性能JSON查看器视图
    /// </summary>
    public partial class FreeHighPerformanceJsonView : UserControl
    {
        private FreeHighPerformanceJsonViewModel _viewModel;
        
        public FreeHighPerformanceJsonView()
        {
            InitializeComponent();
            _viewModel = new FreeHighPerformanceJsonViewModel(this);
            DataContext = _viewModel;
            
            // 添加鼠标滚轮事件处理，在整个窗口区域生效
            this.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        
        /// <summary>
        /// 鼠标滚轮事件处理 - 在整个窗口区域生效
        /// </summary>
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 查找JsonTreeView控件中的ScrollViewer
            var jsonTreeView = GetJsonTreeView();
            if (jsonTreeView != null)
            {
                // 获取JsonTreeView内部的ScrollViewer
                var scrollViewer = FindScrollViewer(jsonTreeView);
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
        }
        
        /// <summary>
        /// 查找控件中的ScrollViewer
        /// </summary>
        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                
                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 获取JsonTreeView控件
        /// </summary>
        public MegaJsonTreeView? GetJsonTreeView()
        {
            return this.FindName("JsonTreeView") as MegaJsonTreeView;
        }
    }

    /// <summary>
    /// 免费高性能JSON查看器视图模型
    /// </summary>
    public class FreeHighPerformanceJsonViewModel : ObservableObject
    {
        private readonly MegaJsonStreamParser _jsonParser;
        private readonly ILogger<FreeHighPerformanceJsonViewModel> _logger;
        private readonly Stopwatch _parseStopwatch = new();
        private readonly System.Timers.Timer _memoryTimer;
        private readonly FreeHighPerformanceJsonView _view;
        
        private string _statusMessage = "准备就绪 - 选择JSON文件开始体验免费高性能方案";
        private string _detailedStatusMessage = "等待文件加载...";
        private bool _isLoading;
        private bool _hasJsonData;
        private string _currentFilePath = "";
        
        // 文件信息
        private string _fileSizeText = "未加载";
        private string _totalNodesText = "0";
        private string _maxDepthText = "0";
        private string _parseTimeText = "0ms";
        
        // 性能监控
        private string _memoryUsageText = "0 MB";
        private string _loadedNodesText = "0";

        public FreeHighPerformanceJsonViewModel(FreeHighPerformanceJsonView view)
        {
            _view = view;
            
            // 尝试从依赖注入容器获取Logger
            try
            {
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                var loggerFactory = serviceProvider?.GetService<ILoggerFactory>();
                _logger = loggerFactory?.CreateLogger<FreeHighPerformanceJsonViewModel>() 
                         ?? new ConsoleLogger<FreeHighPerformanceJsonViewModel>();
                
                var streamParserLogger = loggerFactory?.CreateLogger<MegaJsonStreamParser>();
                _jsonParser = new MegaJsonStreamParser(streamParserLogger);
                
                _logger.LogInformation("免费高性能JSON查看器ViewModel已初始化");
            }
            catch (Exception ex)
            {
                // 如果无法获取Logger，使用控制台Logger作为备选
                _logger = new ConsoleLogger<FreeHighPerformanceJsonViewModel>();
                _jsonParser = new MegaJsonStreamParser(new ConsoleLogger<MegaJsonStreamParser>());
                _logger.LogWarning("无法获取Logger服务，使用控制台Logger: {Exception}", ex.Message);
            }
            
            // 初始化命令
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !string.IsNullOrEmpty(_currentFilePath));
            AnalyzeStructureCommand = new AsyncRelayCommand(AnalyzeStructureAsync, () => !string.IsNullOrEmpty(_currentFilePath));
            CleanupMemoryCommand = new RelayCommand(CleanupMemory);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            
            // 启动内存监控
            _memoryTimer = new System.Timers.Timer(2000);
            _memoryTimer.Elapsed += (s, e) => UpdateMemoryUsage();
            _memoryTimer.Start();
        }

        // 属性
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string DetailedStatusMessage
        {
            get => _detailedStatusMessage;
            set => SetProperty(ref _detailedStatusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                SetProperty(ref _isLoading, value);
                RefreshCommand.NotifyCanExecuteChanged();
                AnalyzeStructureCommand.NotifyCanExecuteChanged();
            }
        }

        public bool HasJsonData
        {
            get => _hasJsonData;
            set 
            { 
                SetProperty(ref _hasJsonData, value);
                OnPropertyChanged(nameof(ShowWelcome));
            }
        }

        public bool ShowWelcome => !HasJsonData && !IsLoading;

        // 文件信息属性
        public string FileSizeText
        {
            get => _fileSizeText;
            set => SetProperty(ref _fileSizeText, value);
        }

        public string TotalNodesText
        {
            get => _totalNodesText;
            set => SetProperty(ref _totalNodesText, value);
        }

        public string MaxDepthText
        {
            get => _maxDepthText;
            set => SetProperty(ref _maxDepthText, value);
        }

        public string ParseTimeText
        {
            get => _parseTimeText;
            set => SetProperty(ref _parseTimeText, value);
        }

        // 性能监控属性
        public string MemoryUsageText
        {
            get => _memoryUsageText;
            set => SetProperty(ref _memoryUsageText, value);
        }

        public string LoadedNodesText
        {
            get => _loadedNodesText;
            set => SetProperty(ref _loadedNodesText, value);
        }

        // 命令
        public IAsyncRelayCommand OpenFileCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand AnalyzeStructureCommand { get; }
        public IRelayCommand CleanupMemoryCommand { get; }
        public IRelayCommand ShowHelpCommand { get; }

        /// <summary>
        /// 打开文件
        /// </summary>
        private async Task OpenFileAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择JSON文件 - 免费高性能方案",
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            await LoadJsonFileAsync(openFileDialog.FileName);
        }

        /// <summary>
        /// 加载JSON文件
        /// </summary>
        public async Task LoadJsonFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage = "文件不存在";
                _logger.LogWarning("尝试加载不存在的文件: {FilePath}", filePath);
                return;
            }

            _currentFilePath = filePath;
            IsLoading = true;
            HasJsonData = false;
            
            _logger.LogInformation("开始加载JSON文件: {FilePath}", filePath);

            try
            {
                _parseStopwatch.Restart();
                
                var fileInfo = new FileInfo(filePath);
                var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                
                StatusMessage = $"正在使用免费高性能方案解析 {fileSizeMB:F2} MB JSON文件...";
                DetailedStatusMessage = "第一阶段：分析文件结构...";
                
                _logger.LogInformation("第一阶段：开始分析JSON文件结构，大小: {FileSizeMB:F2} MB", fileSizeMB);
                
                // 第一阶段：分析文件结构
                var structureInfo = await _jsonParser.AnalyzeJsonStructureAsync(filePath);
                
                _logger.LogInformation("结构分析完成: 总节点={TotalNodes}, 最大深度={MaxDepth}", 
                    structureInfo.TotalNodes, structureInfo.MaxDepth);
                
                // 更新文件信息
                FileSizeText = $"{structureInfo.FileSizeMB:F2} MB";
                TotalNodesText = $"{structureInfo.TotalNodes:N0}";
                MaxDepthText = structureInfo.MaxDepth.ToString();
                
                DetailedStatusMessage = "第二阶段：流式解析JSON数据...";
                _logger.LogInformation("第二阶段：开始流式解析JSON数据");
                
                // 第二阶段：直接加载数据到树形视图
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 获取当前用户控件中的JsonTreeView
                    var treeView = _view.GetJsonTreeView();
                    if (treeView != null)
                    {
                        _logger.LogInformation("找到JsonTreeView控件，开始加载数据");
                        await treeView.LoadJsonFileAsync(filePath);
                        LoadedNodesText = "10000+"; // 预览模式限制
                        _logger.LogInformation("JsonTreeView数据加载完成");
                    }
                    else
                    {
                        _logger.LogWarning("未找到JsonTreeView控件，使用预览模式");
                        // 如果找不到控件，尝试通过ViewModel直接加载数据
                        var previewNodes = await _jsonParser.ParseJsonPreviewAsync(filePath, 10000);
                        // 这里需要将previewNodes转换为MegaJsonNode并设置到TreeView
                        LoadedNodesText = $"{previewNodes.Count}";
                        _logger.LogInformation("预览模式加载完成，节点数: {NodeCount}", previewNodes.Count);
                    }
                });
                
                _parseStopwatch.Stop();
                ParseTimeText = $"{_parseStopwatch.ElapsedMilliseconds} ms";
                
                HasJsonData = true;
                StatusMessage = $"🎉 成功加载 {fileSizeMB:F2} MB JSON文件！";
                DetailedStatusMessage = $"免费方案完成解析：{structureInfo.GetSummary()}";
                
                _logger.LogInformation("JSON文件加载成功: {FilePath}, 耗时: {ElapsedMs} ms", 
                    filePath, _parseStopwatch.ElapsedMilliseconds);
                
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
                DetailedStatusMessage = $"错误详情: {ex.Message}";
                HasJsonData = false;
                
                _logger.LogError(ex, "JSON文件加载失败: {FilePath}", filePath);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新文件
        /// </summary>
        private async Task RefreshAsync()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                await LoadJsonFileAsync(_currentFilePath);
            }
        }

        /// <summary>
        /// 分析结构
        /// </summary>
        private async Task AnalyzeStructureAsync()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                return;

            IsLoading = true;
            try
            {
                StatusMessage = "正在深度分析JSON结构...";
                
                var structureInfo = await _jsonParser.AnalyzeJsonStructureAsync(_currentFilePath);
                
                var analysisResult = $"结构分析完成:\n" +
                                   $"• 文件大小: {structureInfo.FileSizeMB:F2} MB\n" +
                                   $"• 总节点数: {structureInfo.TotalNodes:N0}\n" +
                                   $"• 最大深度: {structureInfo.MaxDepth}\n" +
                                   $"• 对象数量: {structureInfo.ObjectCount:N0}\n" +
                                   $"• 数组数量: {structureInfo.ArrayCount:N0}\n" +
                                   $"• 字符串数量: {structureInfo.StringCount:N0}\n" +
                                   $"• 数字数量: {structureInfo.NumberCount:N0}\n" +
                                   $"• 布尔值数量: {structureInfo.BooleanCount:N0}\n" +
                                   $"• 空值数量: {structureInfo.NullCount:N0}";

                MessageBox.Show(analysisResult, "JSON结构分析报告", MessageBoxButton.OK, MessageBoxImage.Information);
                
                StatusMessage = "结构分析完成";
            }
            catch (Exception ex)
            {
                StatusMessage = $"结构分析失败: {ex.Message}";
                MessageBox.Show($"结构分析失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
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
            
            StatusMessage = "内存清理完成";
            DetailedStatusMessage = "已执行垃圾回收，释放未使用的内存";
            
            // 立即更新内存使用情况
            UpdateMemoryUsage();
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            var helpMessage = "🚀 免费超高性能JSON查看器\n\n" +
                            "核心特性:\n" +
                            "• 支持50MB以上超大JSON文件\n" +
                            "• 处理百万级节点数据\n" +
                            "• 虚拟化渲染技术\n" +
                            "• 流式解析优化\n" +
                            "• 智能内存管理\n" +
                            "• 按需加载子节点\n\n" +
                            "使用说明:\n" +
                            "1. 点击'打开文件'选择JSON文件\n" +
                            "2. 系统自动分析文件结构\n" +
                            "3. 使用树形视图浏览数据\n" +
                            "4. 点击节点展开查看子项\n" +
                            "5. 使用'清理内存'优化性能\n\n" +
                            "技术优势:\n" +
                            "✓ 完全免费开源\n" +
                            "✓ 无第三方组件依赖\n" +
                            "✓ 内存占用优化\n" +
                            "✓ 响应速度快\n" +
                            "✓ 支持超大文件";

            MessageBox.Show(helpMessage, "帮助信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 更新内存使用情况
        /// </summary>
        private void UpdateMemoryUsage()
        {
            var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MemoryUsageText = $"{memoryMB:F1} MB";
            });
        }

        /// <summary>
        /// 获取树形视图控件引用
        /// </summary>
        public MegaJsonTreeView? GetTreeViewControl()
        {
            var mainWindow = Application.Current.MainWindow;
            return mainWindow?.FindName("JsonTreeView") as MegaJsonTreeView;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            // 通知命令状态变化
            switch (e.PropertyName)
            {
                case nameof(IsLoading):
                    RefreshCommand.NotifyCanExecuteChanged();
                    AnalyzeStructureCommand.NotifyCanExecuteChanged();
                    break;
            }
        }
    }
    
    /// <summary>
    /// 简单的控制台Logger实现
    /// </summary>
    public class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var logLevelText = logLevel switch
            {
                LogLevel.Trace => "[TRACE]",
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Information => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                LogLevel.Critical => "[CRITICAL]",
                _ => "[LOG]"
            };
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"{timestamp} {logLevelText} [{typeof(T).Name}] {message}";
            
            if (exception != null)
            {
                logMessage += $"\n异常: {exception}";
            }
            
            // 输出到控制台

            
            // 同时输出到调试器

        }
    }
}
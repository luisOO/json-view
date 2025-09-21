using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JsonViewer.Models;
using JsonViewer.Models.ViewModels;
using JsonViewer.Views;

namespace JsonViewer.Views;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        // 创建简单的ViewModel实例
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        // 启用拖放
        AllowDrop = true;
        Drop += MainWindow_Drop;
        DragEnter += MainWindow_DragEnter;
        
        // 绑定关闭事件
        Closing += MainWindow_Closing;
        
        // 添加鼠标滚轮事件处理，在整个主窗口区域生效
        this.PreviewMouseWheel += OnPreviewMouseWheel;
        
        // 设置窗口拖拽（对于无边框窗口）
        this.MouseLeftButtonDown += (s, e) => {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        };
    }
    
    // 窗口控制按钮事件
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// 鼠标滚轮事件处理 - 在整个主窗口区域生效
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 查找FreeHighPerformanceView中的ScrollViewer
        if (FreeHighPerformanceView != null)
        {
            var scrollViewer = FindScrollViewer(FreeHighPerformanceView);
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
    /// TreeView选中项变化事件
    /// </summary>
    private void JsonTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is JsonTreeNode selectedNode)
        {
            _viewModel.SelectedNode = selectedNode;
        }
    }

    /// <summary>
    /// TreeViewItem展开事件 - 触发懒加载
    /// </summary>
    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem && treeViewItem.DataContext is JsonTreeNode node)
        {
            // 添加调试日志

            
            // 触发懒加载
            await node.LoadChildrenAsync();
            

        }
    }



    /// <summary>
    /// 拖拽进入事件
    /// </summary>
    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && IsJsonFile(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    /// <summary>
    /// 拖放文件事件
    /// </summary>
    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && IsJsonFile(files[0]))
            {
                await _viewModel.LoadFileAsync(files[0]);
                
                // 同时加载到免费高性能方案视图
                if (FreeHighPerformanceView?.DataContext is FreeHighPerformanceJsonViewModel freeViewModel)
                {
                    await freeViewModel.LoadJsonFileAsync(files[0]);
                }
            }
        }
    }

    /// <summary>
    /// 检查是否为JSON文件
    /// </summary>
    private bool IsJsonFile(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".json";
    }

    /// <summary>
    /// 退出菜单项点击事件
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 关于菜单项点击事件
    /// </summary>
    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        aboutWindow.ShowDialog();
    }

    /// <summary>
    /// 窗口关闭事件
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 如果正在加载，询问用户是否确认关闭
        if (_viewModel.IsLoading)
        {
            var result = MessageBox.Show(
                "正在加载文件，确定要关闭应用程序吗？",
                "确认关闭",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }
        
        // 执行清理工作
        try
        {
            // 清理内存
            _viewModel.RootNode?.ClearChildren();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch
        {
            // 忽略清理过程中的错误
        }
    }

    /// <summary>
    /// 处理命令行参数
    /// </summary>
    public async Task HandleCommandLineArgsAsync(string[] args)
    {
        if (args?.Length > 0)
        {
            var filePath = args[0];
            if (System.IO.File.Exists(filePath) && IsJsonFile(filePath))
            {
                await _viewModel.LoadFileAsync(filePath);
            }
        }
    }
}
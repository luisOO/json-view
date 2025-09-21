using System.Diagnostics;
using System.IO;
using System.Windows;

namespace JsonViewer.Views;

/// <summary>
/// AboutWindow.xaml 的交互逻辑
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 确定按钮点击事件
    /// </summary>
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 系统信息按钮点击事件
    /// </summary>
    private void SystemInfoButton_Click(object sender, RoutedEventArgs e)
    {
        var systemInfo = GetSystemInfo();
        
        var systemInfoWindow = new Window
        {
            Title = "系统信息",
            Width = 500,
            Height = 400,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize,
            Background = (System.Windows.Media.Brush)FindResource("BackgroundBrush")
        };

        var scrollViewer = new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(10)
        };

        var textBlock = new System.Windows.Controls.TextBox
        {
            Text = systemInfo,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush")
        };

        scrollViewer.Content = textBlock;
        systemInfoWindow.Content = scrollViewer;
        
        systemInfoWindow.ShowDialog();
    }

    /// <summary>
    /// 获取系统信息
    /// </summary>
    private string GetSystemInfo()
    {
        var info = new System.Text.StringBuilder();
        
        try
        {
            // 操作系统信息
            info.AppendLine("=== 操作系统信息 ===");
            info.AppendLine($"操作系统: {Environment.OSVersion}");
            info.AppendLine($"平台: {Environment.OSVersion.Platform}");
            info.AppendLine($"版本: {Environment.OSVersion.Version}");
            info.AppendLine($"64位操作系统: {Environment.Is64BitOperatingSystem}");
            info.AppendLine($"计算机名: {Environment.MachineName}");
            info.AppendLine($"用户名: {Environment.UserName}");
            info.AppendLine();
            
            // .NET 运行时信息
            info.AppendLine("=== .NET 运行时信息 ===");
            info.AppendLine($".NET 版本: {Environment.Version}");
            info.AppendLine($"64位进程: {Environment.Is64BitProcess}");
            info.AppendLine($"工作目录: {Environment.CurrentDirectory}");
            info.AppendLine();
            
            // 内存信息
            info.AppendLine("=== 内存信息 ===");
            info.AppendLine($"工作集: {Environment.WorkingSet / 1024 / 1024:F1} MB");
            info.AppendLine($"GC总内存: {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");
            info.AppendLine($"GC最大代数: {GC.MaxGeneration}");
            
            for (int i = 0; i <= GC.MaxGeneration; i++)
            {
                info.AppendLine($"第{i}代GC次数: {GC.CollectionCount(i)}");
            }
            info.AppendLine();
            
            // 处理器信息
            info.AppendLine("=== 处理器信息 ===");
            info.AppendLine($"处理器数量: {Environment.ProcessorCount}");
            info.AppendLine();
            
            // 应用程序信息
            info.AppendLine("=== 应用程序信息 ===");
            var process = Process.GetCurrentProcess();
            info.AppendLine($"进程ID: {process.Id}");
            info.AppendLine($"启动时间: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
            info.AppendLine($"运行时间: {DateTime.Now - process.StartTime:hh\\:mm\\:ss}");
            info.AppendLine($"线程数: {process.Threads.Count}");
            info.AppendLine($"句柄数: {process.HandleCount}");
            info.AppendLine();
            
            // 驱动器信息
            info.AppendLine("=== 驱动器信息 ===");
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.IsReady)
                {
                    info.AppendLine($"{drive.Name} ({drive.DriveType})");
                    info.AppendLine($"  总空间: {drive.TotalSize / 1024 / 1024 / 1024:F1} GB");
                    info.AppendLine($"  可用空间: {drive.AvailableFreeSpace / 1024 / 1024 / 1024:F1} GB");
                    info.AppendLine($"  文件系统: {drive.DriveFormat}");
                }
                else
                {
                    info.AppendLine($"{drive.Name} ({drive.DriveType}) - 未就绪");
                }
            }
        }
        catch (Exception ex)
        {
            info.AppendLine($"获取系统信息时发生错误: {ex.Message}");
        }
        
        return info.ToString();
    }
}
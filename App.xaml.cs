using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using JsonViewer.Services;
using JsonViewer.Models.ViewModels;
using JsonViewer.Properties;
using JsonViewer.Views;

namespace JsonViewer;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;

    public ServiceProvider? ServiceProvider => _serviceProvider;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            // 记录构造函数异常但不输出到控制台
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 分配控制台窗口以显示控制台输出 - 已注释掉以避免显示控制台窗口
        // AllocConsole();
        // Console.WriteLine("控制台窗口已分配");

        // 绑定全局异常处理
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            // 加载配置
            LoadConfiguration();

            // 配置基本服务（主要是日志）
            var services = new ServiceCollection();
            ConfigureBasicServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // 获取日志记录器
            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            
            logger.LogInformation("应用程序启动中...");
            
            // 直接创建主窗口，不使用复杂的依赖注入
            var mainWindow = new Views.MainWindow();
            logger.LogInformation("主窗口创建成功");
            
            mainWindow.Show();
            logger.LogInformation("主窗口已显示");
        }
        catch (Exception ex)
        {
            // 记录启动异常
            try
            {
                var logger = _serviceProvider?.GetService<ILogger<App>>();
                logger?.LogCritical(ex, "应用程序启动失败: {Message}", ex.Message);
                Console.WriteLine($"启动异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
            catch
            {
                Console.WriteLine($"启动异常但无法记录: {ex.Message}");
            }
            Shutdown(1);
        }
    }

    private void LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        _configuration = builder.Build();
    }

    private void ConfigureBasicServices(IServiceCollection services)
    {
        // 配置
        services.AddSingleton(_configuration!);

        // 日志服务
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(_configuration!.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();
            
            // 文件日志（如果需要）
            var logPath = _configuration!["Logging:File:Path"];
            if (!string.IsNullOrEmpty(logPath))
            {
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 配置
        services.AddSingleton(_configuration!);
        services.AddSingleton<Settings>();

        // 日志服务
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(_configuration!.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();
            
            // 文件日志（如果需要）
            var logPath = _configuration!["Logging:File:Path"];
            if (!string.IsNullOrEmpty(logPath))
            {
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
        });

        // 核心服务
        services.AddSingleton<LargeJsonParser>();
        services.AddSingleton<AsyncLoadManager>();
        services.AddSingleton<MemoryManager>();
        services.AddSingleton<JsonSearchEngine>();
        services.AddSingleton<ThemeManager>();
        services.AddSingleton<UIThrottleService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    private void InitializeServices()
    {
        // 初始化主题管理器
        var themeManager = _serviceProvider!.GetRequiredService<ThemeManager>();
        themeManager.Initialize();

        // 初始化内存管理器
        var memoryManager = _serviceProvider.GetRequiredService<MemoryManager>();
        memoryManager.StartMonitoring();

        // 加载用户设置
        var settings = _serviceProvider.GetRequiredService<Settings>();
        ApplyUserSettings(settings);
    }

    private void ApplyUserSettings(Settings settings)
    {
        // 应用主题设置
        var themeManager = _serviceProvider!.GetRequiredService<ThemeManager>();
        if (!string.IsNullOrEmpty(settings.Theme))
        {
            themeManager.SetTheme(settings.Theme);
        }
    }

    private void HandleCommandLineArgs(string[] args)
    {
        if (args.Length > 0)
        {
            var filePath = args[0];
            if (File.Exists(filePath))
            {
                // 将文件路径传递给主窗口
                var mainViewModel = _serviceProvider!.GetRequiredService<MainViewModel>();
                _ = mainViewModel.LoadFileAsync(filePath);
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // 保存用户设置
            var settings = _serviceProvider?.GetService<Settings>();
            settings?.Save();

            // 停止服务
            var memoryManager = _serviceProvider?.GetService<MemoryManager>();
            memoryManager?.StopMonitoring();

            // 清理资源
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            // 记录退出时的异常，但不阻止应用程序退出
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "UI线程未处理的异常: {Message}", e.Exception.Message);
            
            e.Handled = true;
        }
        catch
        {
            // 如果异常处理本身出错，标记为已处理以避免崩溃
            e.Handled = true;
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            var exception = e.ExceptionObject as Exception;
            logger?.LogCritical(exception, "应用程序域未处理的异常: {Message}", exception?.Message ?? "未知异常");
            
            if (e.IsTerminating)
            {
                // 记录严重错误但不输出到控制台
            }
        }
        catch
        {
            // 静默处理，避免在异常处理中再次抛出异常
        }
    }
}
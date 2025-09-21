using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Media;

namespace JsonViewer.Services;

/// <summary>
/// 主题管理器
/// </summary>
public class ThemeManager
{
    private readonly ILogger<ThemeManager> _logger;
    private readonly Dictionary<string, ThemeInfo> _themes;
    private string _currentTheme = "Dark";
    
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    
    public string CurrentTheme => _currentTheme;
    public IReadOnlyDictionary<string, ThemeInfo> AvailableThemes => _themes;

    public ThemeManager(ILogger<ThemeManager> logger)
    {
        _logger = logger;
        _themes = new Dictionary<string, ThemeInfo>();
        InitializeThemes();
    }

    /// <summary>
    /// 初始化主题管理器
    /// </summary>
    public void Initialize()
    {
        _logger.LogInformation("主题管理器已初始化");
    }

    /// <summary>
    /// 初始化主题
    /// </summary>
    private void InitializeThemes()
    {
        // 深色主题
        _themes["Dark"] = new ThemeInfo
        {
            Name = "Dark",
            DisplayName = "深色主题",
            ResourcePath = "pack://application:,,,/Resources/Themes/DarkTheme.xaml",
            Colors = new Dictionary<string, Color>
            {
                ["Background"] = Color.FromRgb(30, 30, 30),
                ["Foreground"] = Color.FromRgb(220, 220, 220),
                ["Accent"] = Color.FromRgb(0, 120, 215),
                ["Border"] = Color.FromRgb(60, 60, 60)
            }
        };
        
        // 浅色主题
        _themes["Light"] = new ThemeInfo
        {
            Name = "Light",
            DisplayName = "浅色主题",
            ResourcePath = "pack://application:,,,/Resources/Themes/LightTheme.xaml",
            Colors = new Dictionary<string, Color>
            {
                ["Background"] = Color.FromRgb(255, 255, 255),
                ["Foreground"] = Color.FromRgb(0, 0, 0),
                ["Accent"] = Color.FromRgb(0, 120, 215),
                ["Border"] = Color.FromRgb(200, 200, 200)
            }
        };
        
        // 高对比度主题
        _themes["HighContrast"] = new ThemeInfo
        {
            Name = "HighContrast",
            DisplayName = "高对比度",
            ResourcePath = "pack://application:,,,/Resources/Themes/HighContrastTheme.xaml",
            Colors = new Dictionary<string, Color>
            {
                ["Background"] = Color.FromRgb(0, 0, 0),
                ["Foreground"] = Color.FromRgb(255, 255, 255),
                ["Accent"] = Color.FromRgb(255, 255, 0),
                ["Border"] = Color.FromRgb(255, 255, 255)
            }
        };
        
        _logger.LogInformation("已初始化 {Count} 个主题", _themes.Count);
    }

    /// <summary>
    /// 设置主题（同步版本）
    /// </summary>
    public void SetTheme(string themeName)
    {
        ApplyThemeAsync(themeName).Wait();
    }

    /// <summary>
    /// 应用主题
    /// </summary>
    public async Task<bool> ApplyThemeAsync(string themeName)
    {
        if (!_themes.ContainsKey(themeName))
        {
            _logger.LogWarning("主题不存在: {ThemeName}", themeName);
            return false;
        }

        if (_currentTheme == themeName)
        {
            _logger.LogDebug("主题已经是当前主题: {ThemeName}", themeName);
            return true;
        }

        try
        {
            _logger.LogInformation("开始应用主题: {ThemeName}", themeName);
            
            var theme = _themes[themeName];
            var oldTheme = _currentTheme;
            
            // 在UI线程上应用主题
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyThemeResources(theme);
            });
            
            _currentTheme = themeName;
            
            // 触发主题变更事件
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, themeName));
            
            _logger.LogInformation("主题应用成功: {ThemeName}", themeName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用主题失败: {ThemeName}", themeName);
            return false;
        }
    }

    /// <summary>
    /// 应用主题资源
    /// </summary>
    private void ApplyThemeResources(ThemeInfo theme)
    {
        var app = Application.Current;
        
        // 移除现有主题资源
        var existingThemeResources = app.Resources.MergedDictionaries
            .Where(rd => rd.Source?.ToString().Contains("/Themes/") == true)
            .ToList();
            
        foreach (var resource in existingThemeResources)
        {
            app.Resources.MergedDictionaries.Remove(resource);
        }
        
        // 加载新主题资源
        if (!string.IsNullOrEmpty(theme.ResourcePath))
        {
            try
            {
                var themeResource = new ResourceDictionary
                {
                    Source = new Uri(theme.ResourcePath, UriKind.Absolute)
                };
                app.Resources.MergedDictionaries.Add(themeResource);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法加载主题资源文件: {ResourcePath}", theme.ResourcePath);
                
                // 回退到代码定义的颜色
                ApplyColorsDirectly(theme);
            }
        }
        else
        {
            ApplyColorsDirectly(theme);
        }
    }

    /// <summary>
    /// 直接应用颜色
    /// </summary>
    private void ApplyColorsDirectly(ThemeInfo theme)
    {
        var app = Application.Current;
        
        foreach (var colorPair in theme.Colors)
        {
            var brush = new SolidColorBrush(colorPair.Value);
            brush.Freeze(); // 冻结画刷以提高性能
            
            app.Resources[colorPair.Key + "Brush"] = brush;
            app.Resources[colorPair.Key + "Color"] = colorPair.Value;
        }
    }

    /// <summary>
    /// 获取当前主题信息
    /// </summary>
    public ThemeInfo GetCurrentThemeInfo()
    {
        return _themes[_currentTheme];
    }

    /// <summary>
    /// 获取主题颜色
    /// </summary>
    public Color GetThemeColor(string colorName)
    {
        var theme = GetCurrentThemeInfo();
        return theme.Colors.TryGetValue(colorName, out var color) ? color : Colors.Transparent;
    }

    /// <summary>
    /// 获取主题画刷
    /// </summary>
    public Brush GetThemeBrush(string colorName)
    {
        var color = GetThemeColor(colorName);
        return new SolidColorBrush(color);
    }

    /// <summary>
    /// 检测系统主题
    /// </summary>
    public string DetectSystemTheme()
    {
        try
        {
            // 检查Windows系统主题设置
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var appsUseLightTheme = key.GetValue("AppsUseLightTheme");
                if (appsUseLightTheme is int lightTheme)
                {
                    return lightTheme == 1 ? "Light" : "Dark";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法检测系统主题，使用默认主题");
        }
        
        return "Dark"; // 默认使用深色主题
    }

    /// <summary>
    /// 自动应用系统主题
    /// </summary>
    public async Task<bool> ApplySystemThemeAsync()
    {
        var systemTheme = DetectSystemTheme();
        return await ApplyThemeAsync(systemTheme);
    }

    /// <summary>
    /// 切换主题
    /// </summary>
    public async Task<bool> ToggleThemeAsync()
    {
        var nextTheme = _currentTheme switch
        {
            "Dark" => "Light",
            "Light" => "HighContrast",
            "HighContrast" => "Dark",
            _ => "Dark"
        };
        
        return await ApplyThemeAsync(nextTheme);
    }

    /// <summary>
    /// 保存主题设置
    /// </summary>
    public void SaveThemeSettings()
    {
        try
        {
            Properties.Settings.Default.Theme = _currentTheme;
            Properties.Settings.Default.Save();
            _logger.LogDebug("主题设置已保存: {Theme}", _currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存主题设置失败");
        }
    }

    /// <summary>
    /// 加载主题设置
    /// </summary>
    public async Task LoadThemeSettingsAsync()
    {
        try
        {
            var savedTheme = Properties.Settings.Default.Theme;
            if (!string.IsNullOrEmpty(savedTheme) && _themes.ContainsKey(savedTheme))
            {
                await ApplyThemeAsync(savedTheme);
                _logger.LogDebug("已加载保存的主题设置: {Theme}", savedTheme);
            }
            else
            {
                await ApplySystemThemeAsync();
                _logger.LogDebug("使用系统主题设置");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载主题设置失败，使用默认主题");
            await ApplyThemeAsync("Dark");
        }
    }

    /// <summary>
    /// 注册自定义主题
    /// </summary>
    public void RegisterTheme(ThemeInfo theme)
    {
        if (theme == null || string.IsNullOrEmpty(theme.Name))
        {
            throw new ArgumentException("主题信息无效");
        }
        
        _themes[theme.Name] = theme;
        _logger.LogInformation("已注册自定义主题: {ThemeName}", theme.Name);
    }

    /// <summary>
    /// 移除主题
    /// </summary>
    public bool RemoveTheme(string themeName)
    {
        if (string.IsNullOrEmpty(themeName) || !_themes.ContainsKey(themeName))
        {
            return false;
        }
        
        // 不允许移除内置主题
        if (themeName == "Dark" || themeName == "Light" || themeName == "HighContrast")
        {
            _logger.LogWarning("不能移除内置主题: {ThemeName}", themeName);
            return false;
        }
        
        // 如果当前主题被移除，切换到默认主题
        if (_currentTheme == themeName)
        {
            _ = Task.Run(() => ApplyThemeAsync("Dark"));
        }
        
        _themes.Remove(themeName);
        _logger.LogInformation("已移除主题: {ThemeName}", themeName);
        return true;
    }
}

/// <summary>
/// 主题信息
/// </summary>
public class ThemeInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ResourcePath { get; set; } = string.Empty;
    public Dictionary<string, Color> Colors { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public Version Version { get; set; } = new(1, 0);
}

/// <summary>
/// 主题变更事件参数
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public string OldTheme { get; }
    public string NewTheme { get; }
    
    public ThemeChangedEventArgs(string oldTheme, string newTheme)
    {
        OldTheme = oldTheme;
        NewTheme = newTheme;
    }
}
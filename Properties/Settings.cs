using System.Configuration;
using System.IO;

namespace JsonViewer.Properties;

/// <summary>
/// 应用程序设置
/// </summary>
public sealed class Settings : ApplicationSettingsBase
{
    private static Settings? _defaultInstance;
    
    public static Settings Default
    {
        get
        {
            _defaultInstance ??= (Settings)Synchronized(new Settings());
            return _defaultInstance;
        }
    }

    /// <summary>
    /// 主题设置
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("Dark")]
    public string Theme
    {
        get => (string)this[nameof(Theme)];
        set => this[nameof(Theme)] = value;
    }

    /// <summary>
    /// 窗口位置X
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("100")]
    public double WindowLeft
    {
        get => (double)this[nameof(WindowLeft)];
        set => this[nameof(WindowLeft)] = value;
    }

    /// <summary>
    /// 窗口位置Y
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("100")]
    public double WindowTop
    {
        get => (double)this[nameof(WindowTop)];
        set => this[nameof(WindowTop)] = value;
    }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("1200")]
    public double WindowWidth
    {
        get => (double)this[nameof(WindowWidth)];
        set => this[nameof(WindowWidth)] = value;
    }

    /// <summary>
    /// 窗口高度
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("800")]
    public double WindowHeight
    {
        get => (double)this[nameof(WindowHeight)];
        set => this[nameof(WindowHeight)] = value;
    }

    /// <summary>
    /// 窗口状态
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("Normal")]
    public string WindowState
    {
        get => (string)this[nameof(WindowState)];
        set => this[nameof(WindowState)] = value;
    }

    /// <summary>
    /// 最近打开的文件列表
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string RecentFiles
    {
        get => (string)this[nameof(RecentFiles)];
        set => this[nameof(RecentFiles)] = value;
    }

    /// <summary>
    /// 自动保存间隔（分钟）
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("5")]
    public int AutoSaveInterval
    {
        get => (int)this[nameof(AutoSaveInterval)];
        set => this[nameof(AutoSaveInterval)] = value;
    }

    /// <summary>
    /// 是否启用自动保存
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool AutoSaveEnabled
    {
        get => (bool)this[nameof(AutoSaveEnabled)];
        set => this[nameof(AutoSaveEnabled)] = value;
    }

    /// <summary>
    /// 字体大小
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("12")]
    public double FontSize
    {
        get => (double)this[nameof(FontSize)];
        set => this[nameof(FontSize)] = value;
    }

    /// <summary>
    /// 字体家族
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("Consolas")]
    public string FontFamily
    {
        get => (string)this[nameof(FontFamily)];
        set => this[nameof(FontFamily)] = value;
    }

    /// <summary>
    /// 是否显示行号
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool ShowLineNumbers
    {
        get => (bool)this[nameof(ShowLineNumbers)];
        set => this[nameof(ShowLineNumbers)] = value;
    }

    /// <summary>
    /// 是否启用语法高亮
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool SyntaxHighlighting
    {
        get => (bool)this[nameof(SyntaxHighlighting)];
        set => this[nameof(SyntaxHighlighting)] = value;
    }

    /// <summary>
    /// 是否自动格式化JSON
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool AutoFormatJson
    {
        get => (bool)this[nameof(AutoFormatJson)];
        set => this[nameof(AutoFormatJson)] = value;
    }

    /// <summary>
    /// 搜索是否区分大小写
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("false")]
    public bool SearchCaseSensitive
    {
        get => (bool)this[nameof(SearchCaseSensitive)];
        set => this[nameof(SearchCaseSensitive)] = value;
    }

    /// <summary>
    /// 是否使用正则表达式搜索
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("false")]
    public bool SearchUseRegex
    {
        get => (bool)this[nameof(SearchUseRegex)];
        set => this[nameof(SearchUseRegex)] = value;
    }

    /// <summary>
    /// 内存使用警告阈值（MB）
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("500")]
    public int MemoryWarningThreshold
    {
        get => (int)this[nameof(MemoryWarningThreshold)];
        set => this[nameof(MemoryWarningThreshold)] = value;
    }

    /// <summary>
    /// 最大文件大小限制（MB）
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("100")]
    public int MaxFileSizeLimit
    {
        get => (int)this[nameof(MaxFileSizeLimit)];
        set => this[nameof(MaxFileSizeLimit)] = value;
    }

    /// <summary>
    /// 是否启用虚拟化
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool EnableVirtualization
    {
        get => (bool)this[nameof(EnableVirtualization)];
        set => this[nameof(EnableVirtualization)] = value;
    }

    /// <summary>
    /// 虚拟化阈值（节点数）
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("1000")]
    public int VirtualizationThreshold
    {
        get => (int)this[nameof(VirtualizationThreshold)];
        set => this[nameof(VirtualizationThreshold)] = value;
    }

    /// <summary>
    /// 是否启用性能监控
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool EnablePerformanceMonitoring
    {
        get => (bool)this[nameof(EnablePerformanceMonitoring)];
        set => this[nameof(EnablePerformanceMonitoring)] = value;
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("Information")]
    public string LogLevel
    {
        get => (string)this[nameof(LogLevel)];
        set => this[nameof(LogLevel)] = value;
    }

    /// <summary>
    /// 是否启用崩溃报告
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool EnableCrashReporting
    {
        get => (bool)this[nameof(EnableCrashReporting)];
        set => this[nameof(EnableCrashReporting)] = value;
    }

    /// <summary>
    /// 语言设置
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("zh-CN")]
    public string Language
    {
        get => (string)this[nameof(Language)];
        set => this[nameof(Language)] = value;
    }

    /// <summary>
    /// 是否检查更新
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool CheckForUpdates
    {
        get => (bool)this[nameof(CheckForUpdates)];
        set => this[nameof(CheckForUpdates)] = value;
    }

    /// <summary>
    /// 上次检查更新时间
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("1900-01-01")]
    public DateTime LastUpdateCheck
    {
        get => (DateTime)this[nameof(LastUpdateCheck)];
        set => this[nameof(LastUpdateCheck)] = value;
    }

    /// <summary>
    /// 树视图展开级别
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("2")]
    public int TreeViewExpandLevel
    {
        get => (int)this[nameof(TreeViewExpandLevel)];
        set => this[nameof(TreeViewExpandLevel)] = value;
    }

    /// <summary>
    /// 是否显示状态栏
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool ShowStatusBar
    {
        get => (bool)this[nameof(ShowStatusBar)];
        set => this[nameof(ShowStatusBar)] = value;
    }

    /// <summary>
    /// 是否显示工具栏
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("true")]
    public bool ShowToolBar
    {
        get => (bool)this[nameof(ShowToolBar)];
        set => this[nameof(ShowToolBar)] = value;
    }

    /// <summary>
    /// 侧边栏宽度
    /// </summary>
    [UserScopedSetting]
    [DefaultSettingValue("300")]
    public double SidebarWidth
    {
        get => (double)this[nameof(SidebarWidth)];
        set => this[nameof(SidebarWidth)] = value;
    }

    /// <summary>
    /// 获取最近文件列表
    /// </summary>
    public List<string> GetRecentFilesList()
    {
        if (string.IsNullOrEmpty(RecentFiles))
            return new List<string>();
            
        return RecentFiles.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .ToList();
    }

    /// <summary>
    /// 添加最近文件
    /// </summary>
    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;
            
        var recentFiles = GetRecentFilesList();
        
        // 移除已存在的项
        recentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
        
        // 添加到开头
        recentFiles.Insert(0, filePath);
        
        // 限制最大数量
        const int maxRecentFiles = 10;
        if (recentFiles.Count > maxRecentFiles)
        {
            recentFiles = recentFiles.Take(maxRecentFiles).ToList();
        }
        
        RecentFiles = string.Join("|", recentFiles);
        Save();
    }

    /// <summary>
    /// 清空最近文件列表
    /// </summary>
    public void ClearRecentFiles()
    {
        RecentFiles = string.Empty;
        Save();
    }

    /// <summary>
    /// 重置所有设置为默认值
    /// </summary>
    public void ResetToDefaults()
    {
        Reset();
        Save();
    }

    /// <summary>
    /// 验证设置值
    /// </summary>
    public void ValidateSettings()
    {
        // 验证窗口尺寸
        if (WindowWidth < 400) WindowWidth = 1200;
        if (WindowHeight < 300) WindowHeight = 800;
        
        // 验证字体大小
        if (FontSize < 8) FontSize = 12;
        if (FontSize > 72) FontSize = 12;
        
        // 验证内存阈值
        if (MemoryWarningThreshold < 100) MemoryWarningThreshold = 500;
        if (MemoryWarningThreshold > 2048) MemoryWarningThreshold = 500;
        
        // 验证文件大小限制
        if (MaxFileSizeLimit < 1) MaxFileSizeLimit = 100;
        if (MaxFileSizeLimit > 1024) MaxFileSizeLimit = 100;
        
        // 验证虚拟化阈值
        if (VirtualizationThreshold < 100) VirtualizationThreshold = 1000;
        if (VirtualizationThreshold > 10000) VirtualizationThreshold = 1000;
        
        // 验证自动保存间隔
        if (AutoSaveInterval < 1) AutoSaveInterval = 5;
        if (AutoSaveInterval > 60) AutoSaveInterval = 5;
        
        // 验证树视图展开级别
        if (TreeViewExpandLevel < 0) TreeViewExpandLevel = 2;
        if (TreeViewExpandLevel > 10) TreeViewExpandLevel = 2;
        
        // 验证侧边栏宽度
        if (SidebarWidth < 200) SidebarWidth = 300;
        if (SidebarWidth > 800) SidebarWidth = 300;
        
        Save();
    }
}
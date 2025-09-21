using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using JsonViewer.Models;

namespace JsonViewer.Services;

/// <summary>
/// 大文件JSON解析器 - 优化版本，支持真正的懒加载
/// </summary>
public class LargeJsonParser
{
    private readonly ILogger<LargeJsonParser> _logger;
    private readonly JsonDocumentOptions _jsonOptions;
    
    // 分块大小配置
    private const int CHUNK_SIZE = 8192; // 8KB
    private const int MAX_DEPTH = 100; // 最大嵌套深度
    private const long MAX_FILE_SIZE = 500L * 1024 * 1024; // 提高到500MB
    private const int LAZY_LOAD_THRESHOLD = 100000; // 超过100个子节点时启用懒加载

    // 缓存原始JSON文档用于懒加载
    private JsonDocument? _cachedDocument;
    private string? _cachedFilePath;

    public LargeJsonParser(ILogger<LargeJsonParser> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            MaxDepth = MAX_DEPTH
        };
    }

    /// <summary>
    /// 解析大型JSON文件 - 优化版本
    /// </summary>
    public async Task<JsonTreeNode> ParseLargeFileAsync(
        string filePath, 
        IProgress<int>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件不存在: {filePath}");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MAX_FILE_SIZE)
            throw new InvalidOperationException($"文件过大，超过{MAX_FILE_SIZE / 1024 / 1024}MB限制");

        _logger.LogInformation("开始解析JSON文件: {FilePath}, 大小: {FileSize} bytes", filePath, fileInfo.Length);

        try
        {
            // 使用流式读取
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE);
            
            var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            progress?.Report(25);
            
            // 解析JSON文档并缓存
            _cachedDocument?.Dispose();
            _cachedDocument = JsonDocument.Parse(jsonContent, _jsonOptions);
            _cachedFilePath = filePath;
            
            progress?.Report(50);
            
            // 只构建根节点，子节点懒加载
            var rootNode = await BuildRootNodeAsync(_cachedDocument.RootElement, "root", progress, cancellationToken);
            progress?.Report(100);
            
            _logger.LogInformation("JSON文件解析完成: {FilePath}", filePath);
            return rootNode;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON格式错误: {FilePath}", filePath);
            throw new InvalidOperationException($"JSON格式错误: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析JSON文件失败: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 构建根节点 - 只加载第一层
    /// </summary>
    private async Task<JsonTreeNode> BuildRootNodeAsync(
        JsonElement element, 
        string key, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var node = new JsonTreeNode { Key = key, Level = 0 };
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.ValueType = JsonValueType.Object;
                node.ValueKind = JsonValueKind.Object;
                var objectCount = element.EnumerateObject().Count();
                node.Value = $"{{...}}";
                node.HasChildren = objectCount > 0;
                
                // 对于根节点，总是直接加载第一层子节点，无论数量多少
                await LoadDirectChildrenAsync(node, element, cancellationToken);
                break;
                
            case JsonValueKind.Array:
                node.ValueType = JsonValueType.Array;
                node.ValueKind = JsonValueKind.Array;
                var arrayCount = element.GetArrayLength();
                node.Value = $"[...]";
                node.HasChildren = arrayCount > 0;
                
                // 对于根节点，总是直接加载第一层子节点，无论数量多少
                await LoadDirectChildrenAsync(node, element, cancellationToken);
                break;
                
            default:
                // 叶子节点
                SetLeafNodeValue(node, element);
                break;
        }
        
        return node;
    }

    /// <summary>
    /// 设置懒加载
    /// </summary>
    private void SetupLazyLoading(JsonTreeNode node, JsonElement element)
    {
        node.LazyLoadFunction = async () =>
        {
            if (node.ChildrenLoaded) return;
            
            node.IsLoading = true;
            try
            {
                // 清除占位符
                node.Children.Clear();
                
                // 分批加载子节点
                await LoadChildrenInBatchesAsync(node, element);
                node.ChildrenLoaded = true;
            }
            finally
            {
                node.IsLoading = false;
            }
        };
    }

    /// <summary>
    /// 分批加载子节点
    /// </summary>
    private async Task LoadChildrenInBatchesAsync(JsonTreeNode parentNode, JsonElement element)
    {
        const int batchSize = 500; // 减少批次大小以提高响应性 // 减少批次大小以提高响应性
        
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToList();
            for (int i = 0; i < properties.Count; i += batchSize)
            {
                var batch = properties.Skip(i).Take(batchSize);
                var batchChildren = new List<JsonTreeNode>();
                
                // 创建当前批次的子节点
                foreach (var property in batch)
                {
                    var childNode = CreateChildNode(property.Value, property.Name, parentNode.Level + 1);
                    childNode.Parent = parentNode;
                    batchChildren.Add(childNode);
                }
                
                // 批量添加到UI，使用批量更新机制
                parentNode.BeginBatchUpdate();
                try
                {
                    foreach (var child in batchChildren)
                    {
                        parentNode.Children.Add(child);
                    }
                }
                finally
                {
                    parentNode.EndBatchUpdate();
                }
                
                // 让出控制权，避免UI卡顿
                await Task.Delay(1);
                
                // 每处理200个节点后进行一次更强的延迟
                if ((i + batchSize) % 200 == 0)
                {
                    await Task.Delay(5);
                }
                
                // 每处理1000个节点后进行更长的延迟
                if ((i + batchSize) % 1000 == 0)
                {
                    await Task.Delay(20);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var items = element.EnumerateArray().ToList();
            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = items.Skip(i).Take(batchSize);
                var batchChildren = new List<JsonTreeNode>();
                int index = i;
                
                // 创建当前批次的子节点
                foreach (var item in batch)
                {
                    var childNode = CreateChildNode(item, $"[{index}]", parentNode.Level + 1);
                    childNode.Parent = parentNode;
                    batchChildren.Add(childNode);
                    index++;
                }
                
                // 批量添加到UI，使用批量更新机制
                parentNode.BeginBatchUpdate();
                try
                {
                    foreach (var child in batchChildren)
                    {
                        parentNode.Children.Add(child);
                    }
                }
                finally
                {
                    parentNode.EndBatchUpdate();
                }
                
                // 让出控制权，避免UI卡顿
                await Task.Delay(1);
                
                // 每处理200个节点后进行一次更强的延迟
                if ((i + batchSize) % 200 == 0)
                {
                    await Task.Delay(5);
                }
                
                // 每处理1000个节点后进行更长的延迟
                if ((i + batchSize) % 1000 == 0)
                {
                    await Task.Delay(20);
                }
            }
        }
    }

    /// <summary>
    /// 直接加载少量子节点
    /// </summary>
    private async Task LoadDirectChildrenAsync(JsonTreeNode parentNode, JsonElement element, CancellationToken cancellationToken)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childNode = CreateChildNode(property.Value, property.Name, parentNode.Level + 1);
                childNode.Parent = parentNode;
                parentNode.Children.Add(childNode);
                
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var childNode = CreateChildNode(item, $"[{index}]", parentNode.Level + 1);
                childNode.Parent = parentNode;
                parentNode.Children.Add(childNode);
                index++;
                
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        
        parentNode.ChildrenLoaded = true;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 创建子节点（不递归构建）
    /// </summary>
    private JsonTreeNode CreateChildNode(JsonElement element, string key, int level)
    {
        var node = new JsonTreeNode { Key = key, Level = level };
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.ValueType = JsonValueType.Object;
                node.ValueKind = JsonValueKind.Object;
                var objectCount = element.EnumerateObject().Count();
                node.Value = objectCount > 0 ? $"{{ {objectCount} items }}" : "{}";
                node.HasChildren = objectCount > 0;
                
                if (objectCount > 0)
                {
                    // 使用懒加载以提高性能
                    SetupLazyLoading(node, element);
                    // 添加占位符，但不标记为已加载
                    var placeholder = new JsonTreeNode 
                    { 
                        Key = "...", 
                        Value = "展开以查看内容",
                        ValueType = JsonValueType.Unknown,
                        Parent = node,
                        Level = level + 1
                    };
                    node.Children.Add(placeholder);
                }
                break;
                
            case JsonValueKind.Array:
                node.ValueType = JsonValueType.Array;
                node.ValueKind = JsonValueKind.Array;
                var arrayCount = element.GetArrayLength();
                node.Value = arrayCount > 0 ? $"[ {arrayCount} items ]" : "[]";
                node.HasChildren = arrayCount > 0;
                
                if (arrayCount > 0)
                {
                    // 使用懒加载以提高性能
                    SetupLazyLoading(node, element);
                    // 添加占位符，但不标记为已加载
                    var placeholder = new JsonTreeNode 
                    { 
                        Key = "...", 
                        Value = "展开以查看内容",
                        ValueType = JsonValueType.Unknown,
                        Parent = node,
                        Level = level + 1
                    };
                    node.Children.Add(placeholder);
                }
                break;
                
            default:
                SetLeafNodeValue(node, element);
                break;
        }
        
        return node;
    }

    /// <summary>
    /// 设置叶子节点值
    /// </summary>
    private void SetLeafNodeValue(JsonTreeNode node, JsonElement element)
    {
        node.ValueKind = element.ValueKind;
        
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                node.ValueType = JsonValueType.String;
                var stringValue = element.GetString() ?? "";
                node.Value = stringValue.Length > 100 ? stringValue.Substring(0, 100) + "..." : stringValue;
                node.RawValue = stringValue;
                break;
                
            case JsonValueKind.Number:
                node.ValueType = JsonValueType.Number;
                if (element.TryGetInt64(out var longValue))
                {
                    node.Value = longValue.ToString();
                    node.RawValue = longValue;
                }
                else if (element.TryGetDouble(out var doubleValue))
                {
                    node.Value = doubleValue.ToString("G");
                    node.RawValue = doubleValue;
                }
                else
                {
                    node.Value = element.GetRawText();
                    node.RawValue = element.GetRawText();
                }
                break;
                
            case JsonValueKind.True:
                node.ValueType = JsonValueType.Boolean;
                node.Value = "true";
                node.RawValue = true;
                break;
                
            case JsonValueKind.False:
                node.ValueType = JsonValueType.Boolean;
                node.Value = "false";
                node.RawValue = false;
                break;
                
            case JsonValueKind.Null:
                node.ValueType = JsonValueType.Null;
                node.Value = "null";
                node.RawValue = null;
                break;
                
            default:
                node.ValueType = JsonValueType.Unknown;
                node.Value = element.GetRawText();
                node.RawValue = element.GetRawText();
                break;
        }
    }

    /// <summary>
    /// 保存到文件
    /// </summary>
    public async Task SaveToFileAsync(JsonTreeNode rootNode, string filePath)
    {
        _logger.LogInformation("开始保存JSON文件: {FilePath}", filePath);
        
        try
        {
            var jsonObject = ConvertToJsonElement(rootNode);
            var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            await File.WriteAllTextAsync(filePath, jsonString);
            _logger.LogInformation("JSON文件保存完成: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存JSON文件失败: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 将树节点转换为JSON元素
    /// </summary>
    private object? ConvertToJsonElement(JsonTreeNode node)
    {
        switch (node.ValueType)
        {
            case JsonValueType.Object:
                var obj = new Dictionary<string, object?>();
                foreach (var child in node.Children)
                {
                    obj[child.Key] = ConvertToJsonElement(child);
                }
                return obj;
                
            case JsonValueType.Array:
                var array = new List<object?>();
                foreach (var child in node.Children)
                {
                    array.Add(ConvertToJsonElement(child));
                }
                return array;
                
            case JsonValueType.String:
                return node.RawValue?.ToString();
                
            case JsonValueType.Number:
                return node.RawValue;
                
            case JsonValueType.Boolean:
                return node.RawValue;
                
            case JsonValueType.Null:
                return null;
                
            default:
                return node.Value;
        }
    }

    /// <summary>
    /// 验证JSON格式
    /// </summary>
    public async Task<bool> ValidateJsonAsync(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = await JsonDocument.ParseAsync(fileStream, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证JSON文件时发生错误: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 获取文件统计信息
    /// </summary>
    public async Task<JsonFileStats> GetFileStatsAsync(string filePath)
    {
        var stats = new JsonFileStats
        {
            FilePath = filePath,
            FileSize = new FileInfo(filePath).Length
        };

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = await JsonDocument.ParseAsync(fileStream, _jsonOptions);
            
            CountElements(document.RootElement, stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件统计信息失败: {FilePath}", filePath);
            throw;
        }

        return stats;
    }

    /// <summary>
    /// 统计JSON元素
    /// </summary>
    private void CountElements(JsonElement element, JsonFileStats stats, int depth = 0)
    {
        stats.TotalNodes++;
        stats.MaxDepth = Math.Max(stats.MaxDepth, depth);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                stats.ObjectCount++;
                foreach (var property in element.EnumerateObject())
                {
                    CountElements(property.Value, stats, depth + 1);
                }
                break;
                
            case JsonValueKind.Array:
                stats.ArrayCount++;
                foreach (var item in element.EnumerateArray())
                {
                    CountElements(item, stats, depth + 1);
                }
                break;
                
            case JsonValueKind.String:
                stats.StringCount++;
                break;
                
            case JsonValueKind.Number:
                stats.NumberCount++;
                break;
                
            case JsonValueKind.True:
            case JsonValueKind.False:
                stats.BooleanCount++;
                break;
                
            case JsonValueKind.Null:
                stats.NullCount++;
                break;
        }
    }
}

/// <summary>
/// JSON文件统计信息
/// </summary>
public class JsonFileStats
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int TotalNodes { get; set; }
    public int ObjectCount { get; set; }
    public int ArrayCount { get; set; }
    public int StringCount { get; set; }
    public int NumberCount { get; set; }
    public int BooleanCount { get; set; }
    public int NullCount { get; set; }
    public int MaxDepth { get; set; }
}
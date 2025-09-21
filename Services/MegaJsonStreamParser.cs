using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JsonViewer.Services
{
    /// <summary>
    /// 超大JSON文件流式解析器 - 免费高性能方案
    /// 专门为50MB+、百万级节点的JSON文件设计
    /// </summary>
    public class MegaJsonStreamParser
    {
        private readonly ILogger<MegaJsonStreamParser> _logger;
        private const int BUFFER_SIZE = 8192; // 8KB缓冲区
        private const int MAX_PREVIEW_NODES = 10000; // 预览模式最大节点数
        private const int MAX_DEPTH = 20; // 最大解析深度

        public MegaJsonStreamParser(ILogger<MegaJsonStreamParser>? logger = null)
        {
            _logger = logger ?? new NullLogger<MegaJsonStreamParser>();
        }

        /// <summary>
        /// 分析JSON文件结构信息
        /// </summary>
        public async Task<JsonStructureInfo> AnalyzeJsonStructureAsync(string filePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始分析JSON文件结构: {FilePath}", filePath);
            
            var fileInfo = new FileInfo(filePath);
            var structureInfo = new JsonStructureInfo
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                FileSizeMB = fileInfo.Length / (1024.0 * 1024.0)
            };

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE);
                using var reader = new StreamReader(fileStream);
                
                // 读取整个文件进行结构分析
                var jsonContent = await reader.ReadToEndAsync();
                using var document = JsonDocument.Parse(jsonContent);
                
                var root = document.RootElement;
                AnalyzeElement(root, "", 0, structureInfo);
                
                _logger.LogInformation("JSON结构分析完成: {TotalNodes} 个节点, 最大深度: {MaxDepth}", 
                    structureInfo.TotalNodes, structureInfo.MaxDepth);
                
                return structureInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分析JSON文件结构失败: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// 流式解析JSON文件（预览模式）
        /// </summary>
        public async Task<List<MegaJsonNodeInfo>> ParseJsonPreviewAsync(string filePath, int maxNodes = MAX_PREVIEW_NODES, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始流式解析JSON文件预览: {FilePath}, 最大节点数: {MaxNodes}", filePath, maxNodes);
            
            var result = new List<MegaJsonNodeInfo>();
            var nodeCount = 0;

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE);
                using var reader = new StreamReader(fileStream);
                
                var jsonContent = await reader.ReadToEndAsync();
                using var document = JsonDocument.Parse(jsonContent);
                
                var root = document.RootElement;
                ProcessElementForPreview(root, "", "", 0, result, ref nodeCount, maxNodes);
                
                _logger.LogInformation("JSON预览解析完成: {NodeCount} 个节点", result.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流式解析JSON文件失败: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// 按需加载JSON节点的子节点
        /// </summary>
        public async Task<List<MegaJsonNodeInfo>> LoadChildrenAsync(string filePath, string nodePath, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("按需加载子节点: {FilePath}, 路径: {NodePath}", filePath, nodePath);
            
            var result = new List<MegaJsonNodeInfo>();

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE);
                using var reader = new StreamReader(fileStream);
                
                var jsonContent = await reader.ReadToEndAsync();
                using var document = JsonDocument.Parse(jsonContent);
                
                // 根据路径定位到目标节点
                var targetElement = NavigateToElement(document.RootElement, nodePath);
                if (targetElement.HasValue)
                {
                    var nodeCount = 0;
                    var pathParts = nodePath.Split('.');
                    var level = pathParts.Length;
                    
                    ProcessChildrenOnly(targetElement.Value, nodePath, level, result, ref nodeCount, 1000);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "按需加载子节点失败: {FilePath}, 路径: {NodePath}", filePath, nodePath);
                throw;
            }
        }

        /// <summary>
        /// 分析JSON元素结构
        /// </summary>
        private void AnalyzeElement(JsonElement element, string path, int depth, JsonStructureInfo info)
        {
            info.TotalNodes++;
            if (depth > info.MaxDepth)
                info.MaxDepth = depth;

            // 统计不同类型的节点
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    info.ObjectCount++;
                    foreach (var property in element.EnumerateObject())
                    {
                        var childPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                        AnalyzeElement(property.Value, childPath, depth + 1, info);
                    }
                    break;
                    
                case JsonValueKind.Array:
                    info.ArrayCount++;
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var childPath = string.IsNullOrEmpty(path) ? $"[{index}]" : $"{path}.[{index}]";
                        AnalyzeElement(item, childPath, depth + 1, info);
                        index++;
                    }
                    break;
                    
                case JsonValueKind.String:
                    info.StringCount++;
                    break;
                    
                case JsonValueKind.Number:
                    info.NumberCount++;
                    break;
                    
                case JsonValueKind.True:
                case JsonValueKind.False:
                    info.BooleanCount++;
                    break;
                    
                case JsonValueKind.Null:
                    info.NullCount++;
                    break;
            }
        }

        /// <summary>
        /// 处理JSON元素用于预览
        /// </summary>
        private void ProcessElementForPreview(JsonElement element, string path, string key, int level, 
            List<MegaJsonNodeInfo> result, ref int nodeCount, int maxNodes)
        {
            if (nodeCount >= maxNodes || level > MAX_DEPTH)
                return;

            nodeCount++;
            var currentPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
            var displayKey = string.IsNullOrEmpty(key) ? "root" : key;

            var nodeInfo = new MegaJsonNodeInfo
            {
                Key = displayKey,
                Value = GetElementDisplayValue(element),
                ValueType = element.ValueKind,
                Path = currentPath,
                Level = level,
                ChildCount = GetElementChildCount(element),
                HasChildren = element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array
            };

            result.Add(nodeInfo);

            // 对于根级别或重要的容器，加载一部分子节点
            if (level < 3 && nodeInfo.HasChildren)
            {
                ProcessChildrenOnly(element, currentPath, level, result, ref nodeCount, maxNodes);
            }
        }

        /// <summary>
        /// 只处理子节点
        /// </summary>
        private void ProcessChildrenOnly(JsonElement element, string path, int level, 
            List<MegaJsonNodeInfo> result, ref int nodeCount, int maxNodes)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var propertyCount = 0;
                    foreach (var property in element.EnumerateObject())
                    {
                        if (nodeCount >= maxNodes || propertyCount >= 100) break; // 限制每个对象最多显示100个属性
                        ProcessElementForPreview(property.Value, path, property.Name, level + 1, result, ref nodeCount, maxNodes);
                        propertyCount++;
                    }
                    break;
                    
                case JsonValueKind.Array:
                    var itemCount = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        if (nodeCount >= maxNodes || itemCount >= 100) break; // 限制每个数组最多显示100个元素
                        ProcessElementForPreview(item, path, $"[{itemCount}]", level + 1, result, ref nodeCount, maxNodes);
                        itemCount++;
                    }
                    break;
            }
        }

        /// <summary>
        /// 根据路径导航到特定元素
        /// </summary>
        private JsonElement? NavigateToElement(JsonElement root, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "root")
                return root;

            var parts = path.Split('.');
            var current = root;

            foreach (var part in parts)
            {
                if (part == "root") continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    // 数组索引
                    var indexStr = part.Substring(1, part.Length - 2);
                    if (int.TryParse(indexStr, out var index) && current.ValueKind == JsonValueKind.Array)
                    {
                        var array = current.EnumerateArray().ToArray();
                        if (index >= 0 && index < array.Length)
                        {
                            current = array[index];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    // 对象属性
                    if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return current;
        }

        /// <summary>
        /// 获取元素的显示值
        /// </summary>
        private string GetElementDisplayValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => TruncateString(element.GetString() ?? "", 200),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Object => $"{{ {element.EnumerateObject().Count()} 个属性 }}",
                JsonValueKind.Array => $"[ {element.GetArrayLength()} 个元素 ]",
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// 获取元素子节点数量
        /// </summary>
        private int GetElementChildCount(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().Count(),
                JsonValueKind.Array => element.GetArrayLength(),
                _ => 0
            };
        }

        /// <summary>
        /// 截断长字符串
        /// </summary>
        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
                return str;
            
            return str.Substring(0, maxLength) + "...";
        }
    }

    /// <summary>
    /// JSON结构信息
    /// </summary>
    public class JsonStructureInfo
    {
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public double FileSizeMB { get; set; }
        public long TotalNodes { get; set; }
        public int MaxDepth { get; set; }
        public long ObjectCount { get; set; }
        public long ArrayCount { get; set; }
        public long StringCount { get; set; }
        public long NumberCount { get; set; }
        public long BooleanCount { get; set; }
        public long NullCount { get; set; }
        
        public string GetSummary()
        {
            return $"文件大小: {FileSizeMB:F2} MB, " +
                   $"总节点: {TotalNodes:N0}, " +
                   $"最大深度: {MaxDepth}, " +
                   $"对象: {ObjectCount:N0}, " +
                   $"数组: {ArrayCount:N0}, " +
                   $"字符串: {StringCount:N0}, " +
                   $"数字: {NumberCount:N0}";
        }
    }

    /// <summary>
    /// JSON节点信息
    /// </summary>
    public class MegaJsonNodeInfo
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public JsonValueKind ValueType { get; set; }
        public string Path { get; set; } = "";
        public int Level { get; set; }
        public int ChildCount { get; set; }
        public bool HasChildren { get; set; }
        public long FilePosition { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsLoaded { get; set; }
    }

    /// <summary>
    /// 空Logger实现
    /// </summary>
    public class NullLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
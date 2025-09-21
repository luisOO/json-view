using System.Text.Json;
using System.Text.RegularExpressions;
using JsonViewer.Models;

namespace JsonViewer.Utils;

/// <summary>
/// JSON工具类
/// </summary>
public static class JsonUtils
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 验证JSON字符串格式
    /// </summary>
    public static bool IsValidJson(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return false;

        try
        {
            using var document = JsonDocument.Parse(jsonString);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 格式化JSON字符串
    /// </summary>
    public static string FormatJson(string jsonString, bool indent = true)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(jsonString);
            var options = new JsonSerializerOptions(DefaultOptions)
            {
                WriteIndented = indent
            };
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch
        {
            return jsonString; // 返回原始字符串如果格式化失败
        }
    }

    /// <summary>
    /// 压缩JSON字符串（移除空白字符）
    /// </summary>
    public static string MinifyJson(string jsonString)
    {
        return FormatJson(jsonString, false);
    }

    /// <summary>
    /// 获取JSON值的类型
    /// </summary>
    public static JsonValueType GetJsonValueType(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonValueType.Object,
            JsonValueKind.Array => JsonValueType.Array,
            JsonValueKind.String => JsonValueType.String,
            JsonValueKind.Number => JsonValueType.Number,
            JsonValueKind.True or JsonValueKind.False => JsonValueType.Boolean,
            JsonValueKind.Null => JsonValueType.Null,
            _ => JsonValueType.String
        };
    }

    /// <summary>
    /// 获取JSON值的字符串表示
    /// </summary>
    public static string GetJsonValueString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => $"{{...}} ({GetObjectPropertyCount(element)} 属性)",
            JsonValueKind.Array => $"[...] ({element.GetArrayLength()} 项)",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// 获取对象属性数量
    /// </summary>
    public static int GetObjectPropertyCount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        int count = 0;
        foreach (var _ in element.EnumerateObject())
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// 获取JSON路径
    /// </summary>
    public static string GetJsonPath(JsonTreeNode node)
    {
        if (node.Parent == null)
            return node.Key;

        var path = new List<string>();
        var current = node;
        
        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.Key))
            {
                // 如果键包含特殊字符，需要用引号包围
                var key = NeedsQuoting(current.Key) ? $"['{current.Key}']": current.Key;
                path.Insert(0, key);
            }
            current = current.Parent;
        }

        return string.Join(".", path);
    }

    /// <summary>
    /// 检查键名是否需要引号
    /// </summary>
    private static bool NeedsQuoting(string key)
    {
        if (string.IsNullOrEmpty(key))
            return true;

        // 检查是否包含特殊字符或空格
        return !Regex.IsMatch(key, @"^[a-zA-Z_$][a-zA-Z0-9_$]*$");
    }

    /// <summary>
    /// 转义JSON字符串
    /// </summary>
    public static string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// 反转义JSON字符串
    /// </summary>
    public static string UnescapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\\\", "\\")
            .Replace("\\\"", "\"")
            .Replace("\\b", "\b")
            .Replace("\\f", "\f")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t");
    }

    /// <summary>
    /// 比较两个JSON元素是否相等
    /// </summary>
    public static bool JsonElementsEqual(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind)
            return false;

        return element1.ValueKind switch
        {
            JsonValueKind.String => element1.GetString() == element2.GetString(),
            JsonValueKind.Number => element1.GetRawText() == element2.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => element1.GetBoolean() == element2.GetBoolean(),
            JsonValueKind.Null => true,
            JsonValueKind.Object => CompareJsonObjects(element1, element2),
            JsonValueKind.Array => CompareJsonArrays(element1, element2),
            _ => false
        };
    }

    /// <summary>
    /// 比较JSON对象
    /// </summary>
    private static bool CompareJsonObjects(JsonElement obj1, JsonElement obj2)
    {
        var props1 = obj1.EnumerateObject().ToList();
        var props2 = obj2.EnumerateObject().ToList();

        if (props1.Count != props2.Count)
            return false;

        var dict2 = props2.ToDictionary(p => p.Name, p => p.Value);

        foreach (var prop1 in props1)
        {
            if (!dict2.TryGetValue(prop1.Name, out var value2) || 
                !JsonElementsEqual(prop1.Value, value2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 比较JSON数组
    /// </summary>
    private static bool CompareJsonArrays(JsonElement arr1, JsonElement arr2)
    {
        if (arr1.GetArrayLength() != arr2.GetArrayLength())
            return false;

        var enum1 = arr1.EnumerateArray();
        var enum2 = arr2.EnumerateArray();

        return enum1.Zip(enum2, JsonElementsEqual).All(equal => equal);
    }

    /// <summary>
    /// 获取JSON统计信息
    /// </summary>
    public static JsonStatistics GetJsonStatistics(JsonElement rootElement)
    {
        var stats = new JsonStatistics();
        AnalyzeElement(rootElement, stats, 0);
        return stats;
    }

    /// <summary>
    /// 递归分析JSON元素
    /// </summary>
    private static void AnalyzeElement(JsonElement element, JsonStatistics stats, int depth)
    {
        stats.TotalNodes++;
        stats.MaxDepth = Math.Max(stats.MaxDepth, depth);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                stats.ObjectCount++;
                foreach (var property in element.EnumerateObject())
                {
                    stats.PropertyCount++;
                    AnalyzeElement(property.Value, stats, depth + 1);
                }
                break;

            case JsonValueKind.Array:
                stats.ArrayCount++;
                var arrayLength = element.GetArrayLength();
                stats.ArrayItemCount += arrayLength;
                stats.MaxArrayLength = Math.Max(stats.MaxArrayLength, arrayLength);
                
                foreach (var item in element.EnumerateArray())
                {
                    AnalyzeElement(item, stats, depth + 1);
                }
                break;

            case JsonValueKind.String:
                stats.StringCount++;
                var stringValue = element.GetString();
                if (stringValue != null)
                {
                    stats.TotalStringLength += stringValue.Length;
                    stats.MaxStringLength = Math.Max(stats.MaxStringLength, stringValue.Length);
                }
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

    /// <summary>
    /// 创建JSON路径表达式
    /// </summary>
    public static string CreateJsonPathExpression(string[] pathSegments)
    {
        if (pathSegments == null || pathSegments.Length == 0)
            return "$";

        var expression = "$";
        foreach (var segment in pathSegments)
        {
            if (int.TryParse(segment, out _))
            {
                expression += $"[{segment}]";
            }
            else if (NeedsQuoting(segment))
            {
                expression += $"['{segment}']";
            }
            else
            {
                expression += $".{segment}";
            }
        }

        return expression;
    }

    /// <summary>
    /// 解析JSON路径表达式
    /// </summary>
    public static string[] ParseJsonPathExpression(string pathExpression)
    {
        if (string.IsNullOrEmpty(pathExpression) || pathExpression == "$")
            return Array.Empty<string>();

        var segments = new List<string>();
        var regex = new Regex(@"\[([^\]]+)\]|\.([^.\[]+)");
        var matches = regex.Matches(pathExpression);

        foreach (Match match in matches)
        {
            var segment = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            
            // 移除引号
            if (segment.StartsWith("'") && segment.EndsWith("'"))
            {
                segment = segment[1..^1];
            }
            else if (segment.StartsWith("\"") && segment.EndsWith("\""))
            {
                segment = segment[1..^1];
            }
            
            segments.Add(segment);
        }

        return segments.ToArray();
    }
}

/// <summary>
/// JSON统计信息
/// </summary>
public class JsonStatistics
{
    public int TotalNodes { get; set; }
    public int ObjectCount { get; set; }
    public int ArrayCount { get; set; }
    public int StringCount { get; set; }
    public int NumberCount { get; set; }
    public int BooleanCount { get; set; }
    public int NullCount { get; set; }
    public int PropertyCount { get; set; }
    public int ArrayItemCount { get; set; }
    public int MaxDepth { get; set; }
    public int MaxArrayLength { get; set; }
    public int TotalStringLength { get; set; }
    public int MaxStringLength { get; set; }

    public override string ToString()
    {
        return $"节点总数: {TotalNodes}, 对象: {ObjectCount}, 数组: {ArrayCount}, " +
               $"字符串: {StringCount}, 数字: {NumberCount}, 布尔值: {BooleanCount}, " +
               $"空值: {NullCount}, 最大深度: {MaxDepth}";
    }
}
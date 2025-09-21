namespace JsonViewer.Models;

/// <summary>
/// 搜索结果模型
/// </summary>
public class SearchResult
{
    public SearchResult(JsonTreeNode node, string matchedText, string context = "")
    {
        Node = node;
        MatchedText = matchedText;
        Context = context;
        Path = node.GetPath();
    }

    /// <summary>
    /// 匹配的节点
    /// </summary>
    public JsonTreeNode Node { get; }

    /// <summary>
    /// 匹配的文本
    /// </summary>
    public string MatchedText { get; }

    /// <summary>
    /// 上下文信息
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// 节点路径
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 匹配类型
    /// </summary>
    public SearchMatchType MatchType { get; set; }

    /// <summary>
    /// 匹配的起始位置
    /// </summary>
    public int MatchStart { get; set; }

    /// <summary>
    /// 匹配的长度
    /// </summary>
    public int MatchLength { get; set; }

    public override string ToString()
    {
        return $"{Path}: {MatchedText}";
    }
}

/// <summary>
/// 搜索匹配类型
/// </summary>
public enum SearchMatchType
{
    /// <summary>
    /// 键名匹配
    /// </summary>
    Key,
    
    /// <summary>
    /// 值匹配
    /// </summary>
    Value,
    
    /// <summary>
    /// 路径匹配
    /// </summary>
    Path
}
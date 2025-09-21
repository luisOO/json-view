using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JsonViewer.Models;

namespace JsonViewer.Services;

/// <summary>
/// JSON搜索引擎
/// </summary>
public class JsonSearchEngine
{
    private readonly ILogger<JsonSearchEngine> _logger;
    private readonly ConcurrentDictionary<string, List<SearchIndexItem>> _searchIndex;
    private readonly object _indexLock = new object();
    
    // 搜索配置
    private const int MAX_SEARCH_RESULTS = 1000;
    private const int MIN_SEARCH_LENGTH = 1;
    private const int INDEX_BATCH_SIZE = 100;

    public JsonSearchEngine(ILogger<JsonSearchEngine> logger)
    {
        _logger = logger;
        _searchIndex = new ConcurrentDictionary<string, List<SearchIndexItem>>();
    }

    /// <summary>
    /// 构建搜索索引
    /// </summary>
    public async Task BuildIndexAsync(JsonTreeNode rootNode, CancellationToken cancellationToken = default)
    {
        if (rootNode == null)
            return;

        _logger.LogInformation("开始构建搜索索引");
        
        try
        {
            // 清空现有索引
            _searchIndex.Clear();
            
            // 异步构建索引
            await Task.Run(() => BuildIndexRecursive(rootNode, string.Empty, cancellationToken), cancellationToken);
            
            var totalItems = _searchIndex.Values.Sum(list => list.Count);
            _logger.LogInformation("搜索索引构建完成，共索引 {Count} 个项目", totalItems);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("搜索索引构建被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构建搜索索引失败");
            throw;
        }
    }

    /// <summary>
    /// 递归构建索引
    /// </summary>
    private void BuildIndexRecursive(JsonTreeNode node, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var currentPath = string.IsNullOrEmpty(path) ? node.Key : $"{path}.{node.Key}";
        
        // 索引键名
        if (!string.IsNullOrEmpty(node.Key))
        {
            AddToIndex(node.Key.ToLowerInvariant(), new SearchIndexItem
            {
                Node = node,
                Path = currentPath,
                MatchType = SearchMatchType.Key,
                Content = node.Key
            });
        }
        
        // 索引值
        var nodeValueStr = node.Value?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(nodeValueStr) && node.ValueType != JsonValueType.Object && node.ValueType != JsonValueType.Array)
        {
            var valueText = nodeValueStr.ToLowerInvariant();
            AddToIndex(valueText, new SearchIndexItem
            {
                Node = node,
                Path = currentPath,
                MatchType = SearchMatchType.Value,
                Content = nodeValueStr
            });
            
            // 为长文本创建部分索引
            if (valueText.Length > 10)
            {
                CreatePartialIndex(valueText, new SearchIndexItem
                {
                    Node = node,
                    Path = currentPath,
                    MatchType = SearchMatchType.Value,
                    Content = nodeValueStr
                });
            }
        }
        
        // 索引路径
        AddToIndex(currentPath.ToLowerInvariant(), new SearchIndexItem
        {
            Node = node,
            Path = currentPath,
            MatchType = SearchMatchType.Path,
            Content = currentPath
        });
        
        // 递归处理子节点
        foreach (var child in node.Children)
        {
            BuildIndexRecursive(child, currentPath, cancellationToken);
        }
    }

    /// <summary>
    /// 创建部分索引（用于长文本的子字符串搜索）
    /// </summary>
    private void CreatePartialIndex(string text, SearchIndexItem item)
    {
        // 创建3-gram索引
        for (int i = 0; i <= text.Length - 3; i++)
        {
            var gram = text.Substring(i, 3);
            AddToIndex(gram, item);
        }
    }

    /// <summary>
    /// 添加到索引
    /// </summary>
    private void AddToIndex(string key, SearchIndexItem item)
    {
        _searchIndex.AddOrUpdate(key,
            new List<SearchIndexItem> { item },
            (k, existing) =>
            {
                lock (existing)
                {
                    existing.Add(item);
                    return existing;
                }
            });
    }

    /// <summary>
    /// 搜索
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, SearchOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < MIN_SEARCH_LENGTH)
            return new List<SearchResult>();

        _logger.LogDebug("开始搜索: {Query}", query);
        
        try
        {
            var results = await Task.Run(() => PerformSearch(query, options, cancellationToken), cancellationToken);
            
            _logger.LogDebug("搜索完成，找到 {Count} 个结果", results.Count);
            return results;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索失败: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    private List<SearchResult> PerformSearch(string query, SearchOptions options, CancellationToken cancellationToken)
    {
        var searchQuery = options.CaseSensitive ? query : query.ToLowerInvariant();
        var results = new ConcurrentBag<SearchResult>();
        var processedNodes = new ConcurrentDictionary<string, bool>();
        
        if (options.UseRegex)
        {
            return PerformRegexSearch(searchQuery, options, cancellationToken);
        }
        
        // 并行搜索索引
        Parallel.ForEach(_searchIndex, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, kvp =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var indexKey = kvp.Key;
            var items = kvp.Value;
            
            if (IsMatch(indexKey, searchQuery, options))
            {
                lock (items)
                {
                    foreach (var item in items)
                    {
                        var nodeId = item.Node.GetNodeId();
                        if (processedNodes.TryAdd(nodeId, true))
                        {
                            if (ShouldIncludeResult(item, options))
                            {
                                var result = CreateSearchResult(item, searchQuery, options);
                                results.Add(result);
                                
                                if (results.Count >= MAX_SEARCH_RESULTS)
                                    break;
                            }
                        }
                    }
                }
            }
        });
        
        // 排序并返回结果
        return results
            .OrderByDescending(r => CalculateRelevanceScore(r, searchQuery))
            .Take(MAX_SEARCH_RESULTS)
            .ToList();
    }

    /// <summary>
    /// 执行正则表达式搜索
    /// </summary>
    private List<SearchResult> PerformRegexSearch(string pattern, SearchOptions options, CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();
        
        try
        {
            var regexOptions = RegexOptions.None;
            if (!options.CaseSensitive)
                regexOptions |= RegexOptions.IgnoreCase;
                
            var regex = new Regex(pattern, regexOptions | RegexOptions.Compiled, TimeSpan.FromSeconds(5));
            var processedNodes = new HashSet<string>();
            
            foreach (var kvp in _searchIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                foreach (var item in kvp.Value)
                {
                    var nodeId = item.Node.GetNodeId();
                    if (processedNodes.Add(nodeId))
                    {
                        if (ShouldIncludeResult(item, options))
                        {
                            var match = regex.Match(item.Content);
                            if (match.Success)
                            {
                                var result = new SearchResult(item.Node, match.Value, GetContext(item.Content, match.Index, match.Length))
                                {
                                    MatchType = item.MatchType,
                                    MatchStart = match.Index,
                                    MatchLength = match.Length
                                };
                                
                                results.Add(result);
                                
                                if (results.Count >= MAX_SEARCH_RESULTS)
                                    return results;
                            }
                        }
                    }
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning("正则表达式搜索超时: {Pattern}", pattern);
            throw new InvalidOperationException("搜索超时，请简化搜索模式");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "无效的正则表达式: {Pattern}", pattern);
            throw new InvalidOperationException($"无效的正则表达式: {ex.Message}");
        }
        
        return results;
    }

    /// <summary>
    /// 检查是否匹配
    /// </summary>
    private bool IsMatch(string text, string query, SearchOptions options)
    {
        if (options.UseWildcard)
        {
            return IsWildcardMatch(text, query);
        }
        
        return text.Contains(query);
    }

    /// <summary>
    /// 通配符匹配
    /// </summary>
    private bool IsWildcardMatch(string text, string pattern)
    {
        // 简单的通配符实现，支持 * 和 ?
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
            
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 检查是否应该包含结果
    /// </summary>
    private bool ShouldIncludeResult(SearchIndexItem item, SearchOptions options)
    {
        return (options.SearchInKeys && item.MatchType == SearchMatchType.Key) ||
               (options.SearchInValues && item.MatchType == SearchMatchType.Value) ||
               (options.SearchInPaths && item.MatchType == SearchMatchType.Path);
    }

    /// <summary>
    /// 创建搜索结果
    /// </summary>
    private SearchResult CreateSearchResult(SearchIndexItem item, string query, SearchOptions options)
    {
        var matchIndex = options.CaseSensitive 
            ? item.Content.IndexOf(query)
            : item.Content.ToLowerInvariant().IndexOf(query);
            
        var context = GetContext(item.Content, matchIndex, query.Length);
        
        return new SearchResult(item.Node, query, context)
        {
            MatchType = item.MatchType,
            MatchStart = matchIndex,
            MatchLength = query.Length
        };
    }

    /// <summary>
    /// 获取上下文
    /// </summary>
    private string GetContext(string text, int matchIndex, int matchLength)
    {
        if (matchIndex < 0 || text.Length <= 50)
            return text;
            
        var contextStart = Math.Max(0, matchIndex - 20);
        var contextEnd = Math.Min(text.Length, matchIndex + matchLength + 20);
        
        var context = text.Substring(contextStart, contextEnd - contextStart);
        
        if (contextStart > 0)
            context = "..." + context;
        if (contextEnd < text.Length)
            context = context + "...";
            
        return context;
    }

    /// <summary>
    /// 计算相关性分数
    /// </summary>
    private double CalculateRelevanceScore(SearchResult result, string query)
    {
        double score = 0;
        
        // 匹配类型权重
        switch (result.MatchType)
        {
            case SearchMatchType.Key:
                score += 3.0;
                break;
            case SearchMatchType.Value:
                score += 2.0;
                break;
            case SearchMatchType.Path:
                score += 1.0;
                break;
        }
        
        // 完全匹配加分
        if (string.Equals(result.MatchedText, query, StringComparison.OrdinalIgnoreCase))
        {
            score += 2.0;
        }
        
        // 路径深度影响（浅层节点优先）
        var pathDepth = result.Path.Count(c => c == '.');
        score += Math.Max(0, 5 - pathDepth) * 0.1;
        
        return score;
    }

    /// <summary>
    /// 清空索引
    /// </summary>
    public void ClearIndex()
    {
        _searchIndex.Clear();
        _logger.LogInformation("搜索索引已清空");
    }

    /// <summary>
    /// 获取索引统计信息
    /// </summary>
    public SearchIndexStats GetIndexStats()
    {
        return new SearchIndexStats
        {
            IndexSize = _searchIndex.Count,
            TotalItems = _searchIndex.Values.Sum(list => list.Count),
            MemoryUsage = EstimateMemoryUsage()
        };
    }

    /// <summary>
    /// 估算内存使用量
    /// </summary>
    private long EstimateMemoryUsage()
    {
        long totalSize = 0;
        
        foreach (var kvp in _searchIndex)
        {
            totalSize += kvp.Key.Length * 2; // 字符串大小
            totalSize += kvp.Value.Count * 64; // 估算每个SearchIndexItem的大小
        }
        
        return totalSize;
    }
}

/// <summary>
/// 搜索索引项
/// </summary>
public class SearchIndexItem
{
    public JsonTreeNode Node { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
    public SearchMatchType MatchType { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 搜索选项
/// </summary>
public class SearchOptions
{
    public bool CaseSensitive { get; set; } = false;
    public bool UseRegex { get; set; } = false;
    public bool UseWildcard { get; set; } = false;
    public bool SearchInKeys { get; set; } = true;
    public bool SearchInValues { get; set; } = true;
    public bool SearchInPaths { get; set; } = false;
}

/// <summary>
/// 搜索索引统计信息
/// </summary>
public class SearchIndexStats
{
    public int IndexSize { get; set; }
    public int TotalItems { get; set; }
    public long MemoryUsage { get; set; }
}
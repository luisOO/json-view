namespace JsonViewer.Models;

/// <summary>
/// JSON值类型枚举
/// </summary>
public enum JsonValueType
{
    /// <summary>
    /// 对象类型
    /// </summary>
    Object,
    
    /// <summary>
    /// 数组类型
    /// </summary>
    Array,
    
    /// <summary>
    /// 字符串类型
    /// </summary>
    String,
    
    /// <summary>
    /// 数字类型
    /// </summary>
    Number,
    
    /// <summary>
    /// 布尔类型
    /// </summary>
    Boolean,
    
    /// <summary>
    /// 空值类型
    /// </summary>
    Null,
    
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown
}
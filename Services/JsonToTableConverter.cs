using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using JsonViewer.Models;

namespace JsonViewer.Services
{
    /// <summary>
    /// JSON转表格数据转换器，支持大型JSON文件的高效处理
    /// </summary>
    public class JsonToTableConverter
    {
        /// <summary>
        /// 将JSON字符串转换为表格行数据
        /// </summary>
        public ObservableCollection<JsonTableRow> ConvertToTableRows(string jsonContent)
        {
            var rows = new List<JsonTableRow>();
            
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;
                
                ProcessJsonElement(root, "", "", 0, rows, "");
            }
            catch (Exception ex)
            {
                // 错误处理
                rows.Add(new JsonTableRow
                {
                    Path = "Error",
                    Name = "解析错误",
                    Value = ex.Message,
                    Type = "Error",
                    Level = 0,
                    HasChildren = false
                });
            }
            
            return new ObservableCollection<JsonTableRow>(rows);
        }

        /// <summary>
        /// 递归处理JSON元素
        /// </summary>
        private void ProcessJsonElement(JsonElement element, string currentPath, string propertyName, 
            int level, List<JsonTableRow> rows, string parentPath)
        {
            var fullPath = string.IsNullOrEmpty(currentPath) ? propertyName : $"{currentPath}.{propertyName}";
            
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    // 对象节点
                    var objectRow = new JsonTableRow
                    {
                        Path = fullPath,
                        Name = string.IsNullOrEmpty(propertyName) ? "Root" : propertyName,
                        Value = $"{{ {element.EnumerateObject().Count()} properties }}",
                        Type = "Object",
                        Level = level,
                        HasChildren = element.EnumerateObject().Any(),
                        ParentPath = parentPath,
                        IsExpanded = level < 2 // 默认展开前两层
                    };
                    rows.Add(objectRow);

                    // 处理对象的属性
                    if (objectRow.IsExpanded)
                    {
                        foreach (var property in element.EnumerateObject())
                        {
                            ProcessJsonElement(property.Value, fullPath, property.Name, level + 1, rows, fullPath);
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    // 数组节点
                    var arrayRow = new JsonTableRow
                    {
                        Path = fullPath,
                        Name = propertyName,
                        Value = $"[ {element.GetArrayLength()} items ]",
                        Type = "Array",
                        Level = level,
                        HasChildren = element.GetArrayLength() > 0,
                        ParentPath = parentPath,
                        IsExpanded = level < 2 && element.GetArrayLength() <= 10 // 小数组默认展开
                    };
                    rows.Add(arrayRow);

                    // 处理数组元素
                    if (arrayRow.IsExpanded)
                    {
                        int index = 0;
                        foreach (var item in element.EnumerateArray())
                        {
                            ProcessJsonElement(item, fullPath, $"[{index}]", level + 1, rows, fullPath);
                            index++;
                        }
                    }
                    break;

                case JsonValueKind.String:
                    rows.Add(new JsonTableRow
                    {
                        Path = fullPath,
                        Name = propertyName,
                        Value = element.GetString(),
                        Type = "String",
                        Level = level,
                        HasChildren = false,
                        ParentPath = parentPath
                    });
                    break;

                case JsonValueKind.Number:
                    rows.Add(new JsonTableRow
                    {
                        Path = fullPath,
                        Name = propertyName,
                        Value = element.GetRawText(),
                        Type = "Number",
                        Level = level,
                        HasChildren = false,
                        ParentPath = parentPath
                    });
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    rows.Add(new JsonTableRow
                    {
                        Path = fullPath,
                        Name = propertyName,
                        Value = element.GetBoolean().ToString().ToLower(),
                        Type = "Boolean",
                        Level = level,
                        HasChildren = false,
                        ParentPath = parentPath
                    });
                    break;

                case JsonValueKind.Null:
                    rows.Add(new JsonTableRow
                    {
                        Path = fullPath,
                        Name = propertyName,
                        Value = "null",
                        Type = "Null",
                        Level = level,
                        HasChildren = false,
                        ParentPath = parentPath
                    });
                    break;
            }
        }

        /// <summary>
        /// 更新可见性（展开/折叠功能）
        /// </summary>
        public void UpdateVisibility(ObservableCollection<JsonTableRow> rows, JsonTableRow targetRow)
        {
            targetRow.IsExpanded = !targetRow.IsExpanded;
            
            // 找到所有子节点
            var childRows = rows.Where(r => r.ParentPath?.StartsWith(targetRow.Path) == true).ToList();
            
            foreach (var child in childRows)
            {
                child.IsVisible = targetRow.IsExpanded;
                
                // 如果折叠，同时折叠所有子节点
                if (!targetRow.IsExpanded)
                {
                    child.IsExpanded = false;
                }
            }
        }

        /// <summary>
        /// 获取过滤后的可见行
        /// </summary>
        public ObservableCollection<JsonTableRow> GetVisibleRows(ObservableCollection<JsonTableRow> allRows)
        {
            return new ObservableCollection<JsonTableRow>(allRows.Where(r => r.IsVisible));
        }
    }
}
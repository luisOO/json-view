using System.ComponentModel;

namespace JsonViewer.Models
{
    /// <summary>
    /// JSON数据的表格行表示，专为DataGrid优化
    /// </summary>
    public class JsonTableRow : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isVisible = true;

        /// <summary>
        /// 节点路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 属性名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 值内容
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 嵌套层级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren { get; set; }

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        /// <summary>
        /// 是否可见（用于展开/折叠控制）
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }

        /// <summary>
        /// 显示的缩进字符串
        /// </summary>
        public string IndentString => new string(' ', Level * 4);

        /// <summary>
        /// 展开/折叠按钮文本
        /// </summary>
        public string ExpanderText => HasChildren ? (IsExpanded ? "▼" : "▶") : " ";

        /// <summary>
        /// 带缩进的名称
        /// </summary>
        public string IndentedName => IndentString + Name;

        /// <summary>
        /// 父节点路径
        /// </summary>
        public string ParentPath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
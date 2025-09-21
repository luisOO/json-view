using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace JsonViewer.Controls
{
    /// <summary>
    /// 专为大型JSON数据设计的虚拟化TreeView
    /// 使用虚拟化技术只渲染可见项，大幅提升性能
    /// </summary>
    public class VirtualizedJsonTreeView : ItemsControl
    {
        private ScrollViewer _scrollViewer;
        private VirtualizingStackPanel _itemsHost;
        private readonly Dictionary<int, FrameworkElement> _containerCache = new();
        
        // 虚拟化参数
        public double ItemHeight { get; set; } = 25;
        public int BufferSize { get; set; } = 5; // 缓冲区大小
        
        // 依赖属性
        public static readonly DependencyProperty FlattenedItemsProperty =
            DependencyProperty.Register(nameof(FlattenedItems), typeof(ObservableCollection<VirtualJsonNode>), 
                typeof(VirtualizedJsonTreeView), new PropertyMetadata(OnFlattenedItemsChanged));
        
        public ObservableCollection<VirtualJsonNode> FlattenedItems
        {
            get => (ObservableCollection<VirtualJsonNode>)GetValue(FlattenedItemsProperty);
            set => SetValue(FlattenedItemsProperty, value);
        }
        
        static VirtualizedJsonTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualizedJsonTreeView), 
                new FrameworkPropertyMetadata(typeof(VirtualizedJsonTreeView)));
        }
        
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _itemsHost = GetTemplateChild("PART_ItemsHost") as VirtualizingStackPanel;
            
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }
        }
        
        private static void OnFlattenedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirtualizedJsonTreeView treeView)
            {
                treeView.RefreshVirtualization();
            }
        }
        
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                RefreshVirtualization();
            }
        }
        
        /// <summary>
        /// 刷新虚拟化显示
        /// </summary>
        private void RefreshVirtualization()
        {
            if (_scrollViewer == null || _itemsHost == null || FlattenedItems == null)
                return;
            
            // 计算可见范围
            double viewportTop = _scrollViewer.VerticalOffset;
            double viewportBottom = viewportTop + _scrollViewer.ViewportHeight;
            
            int firstVisibleIndex = Math.Max(0, (int)(viewportTop / ItemHeight) - BufferSize);
            int lastVisibleIndex = Math.Min(FlattenedItems.Count - 1, 
                (int)(viewportBottom / ItemHeight) + BufferSize);
            
            // 清理不在可见范围内的容器
            var containersToRemove = _containerCache.Keys
                .Where(index => index < firstVisibleIndex || index > lastVisibleIndex)
                .ToList();
            
            foreach (int index in containersToRemove)
            {
                if (_containerCache.TryGetValue(index, out var container))
                {
                    _itemsHost.Children.Remove(container);
                    _containerCache.Remove(index);
                }
            }
            
            // 为可见项创建容器
            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                if (!_containerCache.ContainsKey(i) && i < FlattenedItems.Count)
                {
                    var container = CreateItemContainer(FlattenedItems[i], i);
                    _containerCache[i] = container;
                    _itemsHost.Children.Add(container);
                }
            }
            
            // 更新容器位置
            foreach (var kvp in _containerCache)
            {
                int index = kvp.Key;
                var container = kvp.Value;
                
                Canvas.SetTop(container, index * ItemHeight);
                Canvas.SetLeft(container, FlattenedItems[index].Level * 20); // 缩进
            }
            
            // 更新滚动区域高度
            _itemsHost.Height = FlattenedItems.Count * ItemHeight;
        }
        
        /// <summary>
        /// 创建单个项的容器
        /// </summary>
        private FrameworkElement CreateItemContainer(VirtualJsonNode node, int index)
        {
            var border = new Border
            {
                Height = ItemHeight,
                Background = index % 2 == 0 ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0))
            };
            
            var stackPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5, 2, 5, 2)
            };
            
            // 展开/折叠按钮
            if (node.HasChildren)
            {
                var expandButton = new Button
                {
                    Content = node.IsExpanded ? "−" : "+",
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 5, 0),
                    Background = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(0),
                    FontSize = 10
                };
                
                expandButton.Click += (s, e) => ToggleExpansion(node, index);
                stackPanel.Children.Add(expandButton);
            }
            else
            {
                stackPanel.Children.Add(new Border { Width = 21 }); // 占位符
            }
            
            // 属性名
            var nameTextBlock = new TextBlock
            {
                Text = node.Name,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            stackPanel.Children.Add(nameTextBlock);
            
            // 分隔符
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = ": ", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            
            // 值
            var valueTextBlock = new TextBlock
            {
                Text = node.DisplayValue,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = GetValueBrush(node.Type)
            };
            stackPanel.Children.Add(valueTextBlock);
            
            border.Child = stackPanel;
            return border;
        }
        
        /// <summary>
        /// 切换节点展开/折叠状态
        /// </summary>
        private void ToggleExpansion(VirtualJsonNode node, int index)
        {
            node.IsExpanded = !node.IsExpanded;
            
            // 重建扁平化列表
            RebuildFlattenedList();
            RefreshVirtualization();
        }
        
        /// <summary>
        /// 重建扁平化列表
        /// </summary>
        private void RebuildFlattenedList()
        {
            // 这里需要根据展开状态重新生成扁平化列表
            // 具体实现需要配合ViewModel
        }
        
        /// <summary>
        /// 根据数据类型获取显示颜色
        /// </summary>
        private Brush GetValueBrush(JsonNodeType type)
        {
            return type switch
            {
                JsonNodeType.String => Brushes.Green,
                JsonNodeType.Number => Brushes.Blue,
                JsonNodeType.Boolean => Brushes.Purple,
                JsonNodeType.Null => Brushes.Gray,
                JsonNodeType.Array => Brushes.Orange,
                JsonNodeType.Object => Brushes.Brown,
                _ => Brushes.Black
            };
        }
    }
    
    /// <summary>
    /// 虚拟化JSON节点
    /// </summary>
    public class VirtualJsonNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        
        public string Name { get; set; }
        public string Value { get; set; }
        public string DisplayValue => Value?.Length > 50 ? Value.Substring(0, 47) + "..." : Value;
        public JsonNodeType Type { get; set; }
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public List<VirtualJsonNode> Children { get; set; } = new();
        
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
    }
    
    public enum JsonNodeType
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }
}
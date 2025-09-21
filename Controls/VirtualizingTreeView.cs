using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using JsonViewer.Models;

namespace JsonViewer.Controls
{
    /// <summary>
    /// 高性能虚拟化TreeView控件，专为大数据量场景优化
    /// </summary>
    public class VirtualizingTreeView : ItemsControl
    {
        private ScrollViewer _scrollViewer;
        private Canvas _itemsHost;
        private readonly Dictionary<object, TreeViewItemContainer> _containerCache = new();
        private readonly List<TreeViewItemContainer> _visibleContainers = new();
        private double _itemHeight = 22; // 默认项高度
        private int _firstVisibleIndex = 0;
        private int _lastVisibleIndex = -1;
        
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(VirtualizingTreeView),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(VirtualizingTreeView),
                new PropertyMetadata(22.0, OnItemHeightChanged));

        public static readonly DependencyProperty MaxVisibleItemsProperty =
            DependencyProperty.Register(nameof(MaxVisibleItems), typeof(int), typeof(VirtualizingTreeView),
                new PropertyMetadata(100));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public int MaxVisibleItems
        {
            get => (int)GetValue(MaxVisibleItemsProperty);
            set => SetValue(MaxVisibleItemsProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<object> SelectedItemChanged;
        public event RoutedEventHandler ItemExpanded;
        public event RoutedEventHandler ItemCollapsed;

        static VirtualizingTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualizingTreeView),
                new FrameworkPropertyMetadata(typeof(VirtualizingTreeView)));
        }

        public VirtualizingTreeView()
        {

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            
            // 强制应用模板，使用更早的优先级
            this.Dispatcher.BeginInvoke(new Action(() => {

                this.ApplyTemplate();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public override void OnApplyTemplate()
        {

            base.OnApplyTemplate();
            
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
                _scrollViewer.CanContentScroll = true;

            }
            else
            {

            }

            _itemsHost = GetTemplateChild("PART_ItemsHost") as Canvas;
            if (_itemsHost != null)
            {

            }
            else
            {

            }
            
            // 模板应用后立即更新虚拟化
            if (_scrollViewer != null && _itemsHost != null && Items.Count > 0)
            {

                Dispatcher.BeginInvoke(new Action(UpdateVirtualization), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            
            // 检查模板是否存在
            var template = this.Template;

            
            // 如果没有模板，手动创建ScrollViewer和Canvas
            if (template == null)
            {

                CreateManualLayout();
            }
            else
            {

                this.ApplyTemplate();
            }
            
            // 延迟更新虚拟化，确保模板完全应用
            this.Dispatcher.BeginInvoke(new Action(() => {
                UpdateVirtualization();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        private void CreateManualLayout()
        {
            // 创建ScrollViewer
            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = true
            };
            
            // 创建Canvas作为ItemsHost
            _itemsHost = new Canvas();
            
            // 设置ScrollViewer的内容
            _scrollViewer.Content = _itemsHost;
            
            // 直接设置模板，不使用AddChild或Content
            var template = new ControlTemplate(typeof(VirtualizingTreeView));
            var factory = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ScrollViewer");
            factory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            factory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            factory.SetValue(ScrollViewer.CanContentScrollProperty, true);
            
            var canvasFactory = new FrameworkElementFactory(typeof(Canvas), "PART_ItemsHost");
            factory.AppendChild(canvasFactory);
            template.VisualTree = factory;
            
            this.Template = template;
            this.ApplyTemplate();
            
            // 绑定滚动事件
            _scrollViewer.ScrollChanged += OnScrollChanged;
            

        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVirtualization();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                UpdateVirtualization();
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirtualizingTreeView treeView)
            {
                treeView.OnSelectedItemChanged(e.OldValue, e.NewValue);
            }
        }

        private static void OnItemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirtualizingTreeView treeView)
            {
                treeView._itemHeight = (double)e.NewValue;
                treeView.UpdateVirtualization();
            }
        }

        private void OnSelectedItemChanged(object oldValue, object newValue)
        {
            // 更新选中状态
            if (oldValue != null && _containerCache.TryGetValue(oldValue, out var oldContainer))
            {
                oldContainer.IsSelected = false;
            }

            if (newValue != null && _containerCache.TryGetValue(newValue, out var newContainer))
            {
                newContainer.IsSelected = true;
            }

            SelectedItemChanged?.Invoke(this, new RoutedPropertyChangedEventArgs<object>(oldValue, newValue));
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {

            base.OnItemsChanged(e);
            
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ClearContainers();
                    break;
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    // 延迟更新虚拟化，给UI时间处理
                    this.Dispatcher.BeginInvoke(new Action(() => {
                        UpdateVirtualization();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                    break;
            }
        }

        private void UpdateVirtualization()
        {
            if (_scrollViewer == null || Items.Count == 0)
            {
                return;
            }

            var viewportHeight = _scrollViewer.ViewportHeight;
            var scrollOffset = _scrollViewer.VerticalOffset;
            
            // 计算可见范围
            var firstIndex = Math.Max(0, (int)(scrollOffset / _itemHeight));
            var visibleCount = Math.Min(MaxVisibleItems, (int)(viewportHeight / _itemHeight) + 2);
            var lastIndex = Math.Min(Items.Count - 1, firstIndex + visibleCount);

            // 如果可见范围没有变化，直接返回
            if (firstIndex == _firstVisibleIndex && lastIndex == _lastVisibleIndex)
                return;

            _firstVisibleIndex = firstIndex;
            _lastVisibleIndex = lastIndex;

            // 隐藏不可见的容器
            foreach (var container in _visibleContainers.ToList())
            {
                var index = GetItemIndex(container.DataContext);
                if (index < firstIndex || index > lastIndex)
                {
                    container.Visibility = Visibility.Collapsed;
                    _visibleContainers.Remove(container);
                }
            }

            // 显示可见范围内的项
            for (int i = firstIndex; i <= lastIndex; i++)
            {
                var item = Items[i];
                var container = GetOrCreateContainer(item);
                
                if (!_visibleContainers.Contains(container))
                {
                    container.Visibility = Visibility.Visible;
                    _visibleContainers.Add(container);
                }

                // 设置容器位置
                Canvas.SetTop(container, i * _itemHeight);
            }

            // 更新滚动条
            if (_itemsHost != null)
            {
                _itemsHost.Height = Items.Count * _itemHeight;
            }
        }

        private TreeViewItemContainer GetOrCreateContainer(object item)
        {
            if (_containerCache.TryGetValue(item, out var container))
            {
                return container;
            }

            container = new TreeViewItemContainer
            {
                DataContext = item,
                Height = _itemHeight,
                IsSelected = ReferenceEquals(item, SelectedItem)
            };

            // 绑定数据模板
            if (ItemTemplate != null)
            {
                container.ContentTemplate = ItemTemplate;
            }

            // 绑定事件
            container.MouseLeftButtonUp += OnContainerMouseLeftButtonUp;
            container.Expanded += OnContainerExpanded;
            container.Collapsed += OnContainerCollapsed;

            _containerCache[item] = container;
            
            if (_itemsHost != null)
            {
                _itemsHost.Children.Add(container);
            }

            return container;
        }

        private void OnContainerMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItemContainer container)
            {
                SelectedItem = container.DataContext;
                e.Handled = true;
            }
        }

        private void OnContainerExpanded(object sender, RoutedEventArgs e)
        {
            ItemExpanded?.Invoke(sender, e);
        }

        private void OnContainerCollapsed(object sender, RoutedEventArgs e)
        {
            ItemCollapsed?.Invoke(sender, e);
        }

        private int GetItemIndex(object item)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (ReferenceEquals(Items[i], item))
                    return i;
            }
            return -1;
        }

        private void ClearContainers()
        {
            foreach (var container in _containerCache.Values)
            {
                container.MouseLeftButtonUp -= OnContainerMouseLeftButtonUp;
                container.Expanded -= OnContainerExpanded;
                container.Collapsed -= OnContainerCollapsed;
            }
            
            _containerCache.Clear();
            _visibleContainers.Clear();
            
            if (_itemsHost != null)
            {
                _itemsHost.Children.Clear();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (SelectedItem == null)
                return;

            var currentIndex = GetItemIndex(SelectedItem);
            var newIndex = currentIndex;

            switch (e.Key)
            {
                case Key.Up:
                    newIndex = Math.Max(0, currentIndex - 1);
                    break;
                case Key.Down:
                    newIndex = Math.Min(Items.Count - 1, currentIndex + 1);
                    break;
                case Key.Home:
                    newIndex = 0;
                    break;
                case Key.End:
                    newIndex = Items.Count - 1;
                    break;
                case Key.PageUp:
                    newIndex = Math.Max(0, currentIndex - (int)(_scrollViewer?.ViewportHeight / _itemHeight ?? 10));
                    break;
                case Key.PageDown:
                    newIndex = Math.Min(Items.Count - 1, currentIndex + (int)(_scrollViewer?.ViewportHeight / _itemHeight ?? 10));
                    break;
            }

            if (newIndex != currentIndex)
            {
                SelectedItem = Items[newIndex];
                ScrollIntoView(SelectedItem);
                e.Handled = true;
            }
        }

        public void ScrollIntoView(object item)
        {
            var index = GetItemIndex(item);
            if (index >= 0 && _scrollViewer != null)
            {
                var targetOffset = index * _itemHeight;
                _scrollViewer.ScrollToVerticalOffset(targetOffset);
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            
            // 绘制背景
            if (Background != null)
            {
                drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
            }
        }
    }

    /// <summary>
    /// TreeView项容器
    /// </summary>
    public class TreeViewItemContainer : ContentControl
    {
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(TreeViewItemContainer),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TreeViewItemContainer),
                new PropertyMetadata(false, OnIsExpandedChanged));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public event RoutedEventHandler Expanded;
        public event RoutedEventHandler Collapsed;

        static TreeViewItemContainer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeViewItemContainer),
                new FrameworkPropertyMetadata(typeof(TreeViewItemContainer)));
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeViewItemContainer container)
            {
                container.UpdateVisualState();
            }
        }

        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeViewItemContainer container)
            {
                var isExpanded = (bool)e.NewValue;
                if (isExpanded)
                {
                    container.Expanded?.Invoke(container, new RoutedEventArgs());
                }
                else
                {
                    container.Collapsed?.Invoke(container, new RoutedEventArgs());
                }
                container.UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            if (IsSelected)
            {
                VisualStateManager.GoToState(this, "Selected", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Unselected", true);
            }

            if (IsExpanded)
            {
                VisualStateManager.GoToState(this, "Expanded", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Collapsed", true);
            }
        }
    }
}
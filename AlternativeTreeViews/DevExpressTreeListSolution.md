# DevExpress TreeListView 大数据解决方案

## 安装
```xml
<PackageReference Include="DevExpress.Wpf.Core" Version="23.2.3" />
<PackageReference Include="DevExpress.Wpf.TreeList" Version="23.2.3" />
```

## 核心特性
- **虚拟化模式**: 支持行虚拟化和数据虚拟化
- **异步数据源**: 支持异步数据绑定
- **智能缓存**: 自动管理数据缓存
- **高性能渲染**: 优化的渲染引擎

## 实现示例
```xml
<dxg:TreeListControl x:Name="JsonTreeList"
                     EnableSmartColumnsGeneration="True"
                     AutoGenerateColumns="AllColumns"
                     ItemsSource="{Binding FlattenedJsonNodes}">
    <dxg:TreeListControl.Columns>
        <dxg:TreeListColumn FieldName="Name" Header="属性名" />
        <dxg:TreeListColumn FieldName="Value" Header="值" />
        <dxg:TreeListColumn FieldName="Type" Header="类型" />
    </dxg:TreeListControl.Columns>
    
    <dxg:TreeListControl.View>
        <dxg:TreeListView KeyFieldName="Id" 
                          ParentFieldName="ParentId"
                          ShowTotalSummary="False"
                          AutoExpandAllNodes="False"
                          NodeWrap="NoWrap"
                          AllowPerPixelScrolling="True"/>
    </dxg:TreeListControl.View>
</dxg:TreeListControl>
```

## ViewModel适配
```csharp
public class FlatJsonNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public string Type { get; set; }
    public int Level { get; set; }
}

// 将JSON树扁平化为适合TreeList的结构
public ObservableCollection<FlatJsonNode> FlattenedJsonNodes { get; set; }
```
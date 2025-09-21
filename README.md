# JSON Viewer

一个功能强大的JSON文件查看器，专为处理大型JSON文件而设计，提供高性能的解析、搜索和可视化功能。

## 主要特性

### 🚀 高性能处理
- **大文件支持**: 支持GB级别的JSON文件处理
- **流式解析**: 采用流式解析技术，减少内存占用
- **虚拟化显示**: TreeView虚拟化，支持数百万节点的流畅显示
- **懒加载**: 按需加载子节点，提升响应速度
- **内存管理**: 智能内存管理和垃圾回收优化

### 🔍 强大搜索功能
- **全文搜索**: 支持键名、值、路径的全文搜索
- **正则表达式**: 支持复杂的正则表达式搜索
- **通配符匹配**: 支持通配符模式匹配
- **搜索结果高亮**: 搜索结果实时高亮显示
- **搜索历史**: 保存搜索历史记录

### 🎨 现代化界面
- **多主题支持**: 深色、浅色、高对比度主题
- **响应式设计**: 自适应窗口大小变化
- **语法高亮**: JSON语法高亮显示
- **图标化显示**: 不同数据类型使用不同图标
- **现代化控件**: 采用现代化的WPF控件设计

### 📁 文件操作
- **多格式支持**: 支持.json、.txt等多种文件格式
- **拖拽打开**: 支持拖拽文件到窗口打开
- **最近文件**: 记录最近打开的文件列表
- **文件验证**: 自动验证JSON文件格式
- **编码检测**: 自动检测文件编码格式

### ⚙️ 高级功能
- **性能监控**: 实时显示内存使用和性能指标
- **错误处理**: 完善的错误处理和用户提示
- **设置保存**: 用户设置自动保存和恢复
- **键盘快捷键**: 丰富的键盘快捷键支持
- **状态栏信息**: 显示文件信息、节点统计等

## 系统要求

- **操作系统**: Windows 10 或更高版本
- **运行时**: .NET 6.0 或更高版本
- **内存**: 建议4GB以上RAM（处理大文件时需要更多内存）
- **存储**: 至少100MB可用磁盘空间

## 安装说明

### 从源码构建

1. 克隆仓库:
```bash
git clone https://github.com/your-username/json-viewer.git
cd json-viewer
```

2. 还原NuGet包:
```bash
dotnet restore
```

3. 构建项目:
```bash
dotnet build --configuration Release
```

4. 运行应用程序:
```bash
dotnet run --project JsonViewer
```

### 发布版本

从[Releases页面](https://github.com/your-username/json-viewer/releases)下载最新版本的安装包。

## 使用指南

### 基本操作

1. **打开文件**:
   - 使用菜单: `文件` → `打开`
   - 快捷键: `Ctrl+O`
   - 拖拽文件到窗口

2. **搜索内容**:
   - 使用搜索框输入关键词
   - 支持正则表达式搜索
   - 使用`F3`查找下一个结果

3. **导航操作**:
   - 点击节点展开/折叠
   - 使用键盘方向键导航
   - 双击节点快速展开/折叠所有子节点

### 高级功能

#### 主题切换
- 菜单: `视图` → `主题`
- 支持深色、浅色、高对比度主题
- 主题设置自动保存

#### 性能监控
- 菜单: `视图` → `性能监控`
- 实时显示内存使用情况
- 显示加载和搜索性能指标

#### 搜索选项
- **区分大小写**: 控制搜索是否区分大小写
- **正则表达式**: 启用正则表达式搜索模式
- **搜索范围**: 选择搜索键名、值或路径

## 键盘快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+O` | 打开文件 |
| `Ctrl+F` | 打开搜索 |
| `F3` | 查找下一个 |
| `Shift+F3` | 查找上一个 |
| `Ctrl+E` | 展开所有节点 |
| `Ctrl+C` | 折叠所有节点 |
| `F1` | 显示帮助 |
| `F11` | 全屏模式 |
| `Ctrl+T` | 切换主题 |
| `Ctrl+,` | 打开设置 |

## 技术架构

### 核心技术
- **框架**: WPF (.NET 6.0)
- **架构模式**: MVVM (Model-View-ViewModel)
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **JSON解析**: System.Text.Json
- **日志记录**: Microsoft.Extensions.Logging

### 项目结构
```
JsonViewer/
├── Models/                 # 数据模型
│   ├── JsonTreeNode.cs    # JSON树节点模型
│   ├── SearchResult.cs    # 搜索结果模型
│   └── ViewModels/        # 视图模型
├── Views/                 # 视图文件
│   ├── MainWindow.xaml    # 主窗口
│   └── AboutWindow.xaml   # 关于窗口
├── Services/              # 服务层
│   ├── LargeJsonParser.cs # JSON解析服务
│   ├── JsonSearchEngine.cs # 搜索引擎
│   ├── ThemeManager.cs    # 主题管理
│   └── MemoryManager.cs   # 内存管理
├── Converters/            # 值转换器
├── Utils/                 # 工具类
├── Resources/             # 资源文件
│   ├── Themes/           # 主题资源
│   └── Icons/            # 图标资源
└── Properties/           # 应用程序属性
```

### 设计模式
- **MVVM模式**: 分离视图和业务逻辑
- **依赖注入**: 松耦合的组件设计
- **观察者模式**: 数据绑定和事件通知
- **策略模式**: 可插拔的搜索和解析策略
- **工厂模式**: 对象创建和管理

## 性能优化

### 内存优化
- 使用`WeakReference`避免内存泄漏
- 实现智能缓存机制
- 及时释放不需要的资源
- 监控内存使用情况

### 渲染优化
- TreeView虚拟化显示
- 懒加载子节点
- 异步UI更新
- 减少不必要的重绘

### 搜索优化
- 建立搜索索引
- 并行搜索处理
- 结果缓存机制
- 增量搜索更新

## 配置选项

应用程序支持多种配置选项，设置文件位于用户配置目录：

```
%APPDATA%\JsonViewer\settings.json
```

### 主要配置项
- **主题设置**: 默认主题、自动切换
- **窗口设置**: 位置、大小、状态
- **性能设置**: 内存限制、虚拟化选项
- **搜索设置**: 默认选项、历史记录
- **文件设置**: 最近文件列表、自动保存

## 故障排除

### 常见问题

**Q: 打开大文件时程序响应缓慢**
A: 这是正常现象，程序正在解析文件。可以在状态栏查看进度。建议关闭其他占用内存的程序。

**Q: 搜索结果不准确**
A: 检查搜索选项设置，确认是否启用了正确的搜索模式（区分大小写、正则表达式等）。

**Q: 程序崩溃或内存不足**
A: 尝试增加系统虚拟内存，或者使用64位版本的程序。对于超大文件，建议分批处理。

**Q: 主题切换不生效**
A: 重启应用程序，或者检查主题文件是否完整。

### 日志文件

程序运行日志保存在：
```
%APPDATA%\JsonViewer\logs\
```

遇到问题时，请查看日志文件获取详细错误信息。

## 贡献指南

欢迎贡献代码！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

### 代码规范
- 遵循C#编码规范
- 添加适当的注释和文档
- 编写单元测试
- 确保代码通过所有测试

### 报告问题

如果发现bug或有功能建议，请在[Issues页面](https://github.com/your-username/json-viewer/issues)创建issue。

## 许可证

本项目采用MIT许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 更新日志

### v1.0.0 (2024-01-XX)
- 初始版本发布
- 支持大文件JSON解析
- 实现搜索功能
- 多主题支持
- 性能监控功能

## 致谢

感谢以下开源项目和贡献者：
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json) - JSON解析
- [Microsoft.Extensions](https://docs.microsoft.com/en-us/dotnet/core/extensions/) - 依赖注入和日志
- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - UI框架

## 联系方式

- 项目主页: https://github.com/your-username/json-viewer
- 问题反馈: https://github.com/your-username/json-viewer/issues
- 邮箱: your-email@example.com

---

**JSON Viewer** - 让JSON文件查看变得简单高效！
# 此风Zepheng - Markdown编辑器

<div align="center">

![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)

一个功能丰富、简洁美观的现代化Markdown编辑器，基于WPF和.NET 8开发。

辛弃疾《贺新郎》
甚矣吾衰矣。怅平生、交游零落，只今余几！白发空垂三千丈，一笑人间万事。
问何物、能令公喜？我见青山多妩媚，料青山见我应如是。情与貌，略相似。
一尊搔首东窗里。想渊明、停云诗就， **此时风味** 。江左沉酣求名者，岂识浊醪妙理。
回首叫、云飞风起。不恨古人吾不见，恨古人不见吾狂耳。知我者，二三子。

[功能特性](#-功能特性) • [安装使用](#-安装使用) • [技术架构](#-技术架构) • [开发指南](#-开发指南) • [许可证](#-许可证)

</div>

## 📖 项目简介

此风Zepheng是一个专为Windows平台设计的现代化Markdown编辑器，采用Material Design风格，提供直观的用户界面和强大的编辑功能。无论是技术文档编写、博客创作还是日常笔记记录，都能为您提供出色的写作体验。

## ✨ 功能特性

### 🚀 核心功能

- **多视图模式**
  - 📝 编辑模式：专注文本编辑，支持语法高亮
  - 👁️ 预览模式：实时HTML渲染预览
  - ⚡ 分屏模式：同时显示编辑和预览界面

- **强大的编辑工具**
  - 🛠️ 完整工具栏：快速格式化按钮
  - ⌨️ 键盘快捷键：提升编辑效率
  - 🎯 智能语法高亮：Markdown语法着色显示
  - 🔍 查找替换：支持Ctrl+F和Ctrl+H快捷键

### 📁 文件管理

- **便捷的文件操作**
  - 新建、打开、保存、另存为
  - 文件夹浏览器：树形目录结构
  - 多标签页：同时编辑多个文档
  - 自动保存：每2分钟自动保存更改
  - 文件状态指示：显示未保存的更改

### 🎨 用户界面

- **现代化设计**
  - Material Design图标
  - MahApps.Metro风格界面
  - 明亮/暗黑主题切换
  - 响应式布局设计
  - 状态栏信息：字数统计、光标位置

### 📝 Markdown支持

- **高级语法支持**
  - 标准Markdown语法
  - 扩展语法（表格、代码块、删除线等）
  - 实时预览渲染
  - HTML导出功能

## 🛠 技术架构

### 框架与依赖

- **核心框架**: .NET 8.0 Windows
- **UI框架**: WPF (Windows Presentation Foundation)
- **界面库**: MahApps.Metro 3.0
- **图标库**: MahApps.Metro.IconPacks.Material 6.0
- **文本编辑**: AvalonEdit 6.3.1
- **Markdown解析**: Markdig 0.42.0
- **Web预览**: Microsoft.Web.WebView2

### 项目结构

```
Zepheng/
├── App.xaml                 # 应用程序入口
├── MainWindow.xaml          # 主窗口界面
├── MainWindow.xaml.cs       # 主窗口逻辑
├── RelayCommand.cs          # 命令绑定类
├── Markdown-Mode.xshd       # Markdown语法高亮配置
├── assets/                  # 资源文件
│   └── icon.ico            # 应用程序图标
└── Zepheng.csproj          # 项目文件
```

## 🚀 安装使用

### 系统要求

- **操作系统**: Windows 10/11 (版本1903或更高)
- **运行时**: .NET 8.0 Runtime
- **开发环境**: Visual Studio 2022 或 JetBrains Rider

### 从源码构建

1. **克隆仓库**
   ```bash
   git clone https://github.com/your-username/Zepheng.git
   cd Zepheng
   ```

2. **还原依赖包**
   ```bash
   dotnet restore
   ```

3. **编译项目**
   ```bash
   dotnet build
   ```

4. **运行应用**
   ```bash
   dotnet run
   ```

### 使用指南

#### 基本操作

- **创建文档**: `Ctrl+N` 或点击工具栏新建按钮
- **打开文件**: `Ctrl+O` 或点击打开按钮
- **保存文档**: `Ctrl+S` 或点击保存按钮
- **另存为**: `Ctrl+Shift+S`

#### 格式化快捷键

- **加粗文字**: `Ctrl+B` 或点击工具栏 **B** 按钮
- **斜体文字**: `Ctrl+I` 或点击工具栏 *I* 按钮
- **查找文本**: `Ctrl+F`
- **替换文本**: `Ctrl+H`

#### 视图模式切换

- 点击工具栏中的视图模式按钮：
  - 📝 编辑模式
  - 👁️ 预览模式  
  - ⚡ 分屏模式

## 🔧 开发指南

### 开发环境配置

推荐使用 **JetBrains Rider** 作为开发IDE，它提供了出色的.NET开发体验。

1. **安装JetBrains Rider**
2. **打开项目**: 选择 `Zepheng.sln` 解决方案文件
3. **配置SDK**: 确保安装了.NET 8.0 SDK

### 代码结构

#### 主要类说明

- `MainWindow`: 主窗口类，包含所有UI逻辑和事件处理
- `RelayCommand`: 实现ICommand接口的命令绑定类
- `ViewMode`: 枚举类型，定义编辑器的视图模式

#### 关键功能实现

```csharp
// 视图模式枚举
public enum ViewMode
{
    Edit,     // 编辑模式
    Preview,  // 预览模式
    Split     // 分屏模式
}

// Markdown转HTML
private string ConvertMarkdownToHtml(string markdown)
{
    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
    return Markdown.ToHtml(markdown, pipeline);
}
```

### 贡献指南

1. Fork 项目到您的GitHub账户
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建Pull Request

## 📋 更新日志

### v1.0.0 (当前版本)

- ✅ 完整的Markdown编辑功能
- ✅ 多视图模式支持
- ✅ 现代化UI设计
- ✅ 文件管理功能
- ✅ 自动保存机制
- ✅ 键盘快捷键支持

## 🎯 未来规划

- [ ] 插件系统支持
- [ ] 更多主题选择
- [ ] 云同步功能
- [ ] 数学公式支持
- [ ] 图表绘制功能
- [ ] 多语言国际化

## 🐛 问题反馈

如果您在使用过程中遇到任何问题，请通过以下方式联系我们：

- 提交 [GitHub Issue](https://github.com/your-username/Zepheng/issues)
- 发送邮件至: [your-email@example.com]

## 🤝 致谢

感谢以下开源项目的支持：

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) - 强大的文本编辑器组件
- [MahApps.Metro](https://github.com/MahApps/MahApps.Metro) - 现代化WPF界面库
- [Markdig](https://github.com/lunet-io/markdig) - 高性能Markdown解析器
- [Material Design Icons](https://materialdesignicons.com/) - 精美的图标库

## 📄 许可证

本项目基于 [Apache License 2.0](LICENSE) 许可证开源。

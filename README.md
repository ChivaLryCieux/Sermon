# Sermon - Markdown编辑器

<div align="center">

![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)

一个简洁、克制、面向写作的 Markdown 编辑器，基于 Avalonia 和 .NET 10 开发。

[功能特性](#-功能特性) • [迁移说明](#-从-wpf-迁移到-avalonia) • [安装使用](#-安装使用) • [技术架构](#-技术架构) • [开发指南](#-开发指南)

</div>

## 📖 项目简介

Sermon 是一个跨平台 Markdown 编辑器，采用 Avalonia 构建，可在 Windows、Linux 和 macOS 上运行。界面以瑞士平面设计风格为主：清晰网格、黑白灰层级、红色强调、低装饰控件和明确的排版结构。它适合技术文档、博客草稿、讲章笔记和日常写作。

## ✨ 功能特性

### 🚀 核心功能

- **多视图模式**
  - 📝 编辑模式：专注文本编辑，支持语法高亮
  - 👁️ 预览模式：Avalonia 原生控件实时预览
  - ⚡ 分屏模式：同时显示编辑和预览界面

- **编辑工具**
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

- **瑞士风格界面**
  - 黑白灰为主体，红色作为状态和结构强调
  - 清晰的左侧文件区、分割线和编辑区网格
  - 方正、低装饰的工具按钮和切换按钮
  - 统一的编辑器和 Markdown 预览排版

- **基础界面能力**
  - Avalonia Fluent 主题
  - 跨平台窗口、菜单、标签页和文件选择器
  - 低对比度边线和高可读状态栏
  - 状态栏信息：字数统计、光标位置

### 📝 Markdown支持

- **高级语法支持**
  - 标准Markdown语法
  - 常用语法（标题、列表、引用、代码块、删除线等）
  - 实时预览渲染
  - 预览刷新防抖，减少输入时的重复渲染

## 🛠 技术架构

### 框架与依赖

- **核心框架**: .NET 10.0
- **UI框架**: Avalonia 11.3
- **主题**: Avalonia Fluent
- **文本编辑**: AvaloniaEdit 11.4
- **预览**: Avalonia 原生控件渲染

### 从 WPF 迁移到 Avalonia

Sermon 最初基于 WPF 构建，只能依赖 `net10.0-windows`、`Microsoft.WindowsDesktop.App` 和 Windows 桌面组件运行。为了在 Ubuntu/Linux、macOS 和 Windows 上使用同一套桌面应用代码，项目已经迁移到 Avalonia。

迁移后的主要变化：

- **目标框架**: 从 `net10.0-windows` 改为跨平台 `net10.0`
- **UI 框架**: 从 WPF XAML 改为 Avalonia AXAML
- **应用入口**: 从 WPF `StartupUri` 改为 Avalonia `Program.cs` + `App.axaml`
- **窗口和控件**: 移除了 MahApps.Metro，改用 Avalonia 原生 `Window`、`Menu`、`TabControl`、`TreeView`、`GridSplitter` 等控件
- **文本编辑器**: 从 WPF AvalonEdit 切换到 AvaloniaEdit
- **文件选择**: 从 `Microsoft.Win32.OpenFileDialog` 和 Windows Forms 文件夹对话框切换到 Avalonia `StorageProvider`
- **预览实现**: 移除了 Windows-only WebView2，改为 Avalonia 原生控件渲染 Markdown 预览
- **代码隐藏**: `App.xaml.cs` / `MainWindow.xaml.cs` 调整为 `App.axaml.cs` / `MainWindow.axaml.cs`

迁移后可以直接在 Ubuntu 上构建和运行：

```bash
dotnet run --project Sermon/Sermon.csproj
```

当前预览实现优先保证跨平台和轻量运行，覆盖标题、列表、引用、代码块等常用 Markdown 结构。它不再使用 WebView2 的 HTML/CSS 渲染能力；如果后续需要完整 HTML 级预览，可以再引入跨平台 WebView 或 Markdown.Avalonia。

### 项目结构

```
.
├── Sermon.sln
├── Directory.Build.props
├── global.json
├── README.md
└── Sermon/
    ├── Program.cs               # Avalonia 桌面入口
    ├── App.axaml                # 应用程序资源
    ├── App.axaml.cs             # 应用程序初始化
    ├── MainWindow.axaml         # 主窗口界面
    ├── MainWindow.axaml.cs      # 主窗口逻辑
    ├── RelayCommand.cs          # 命令绑定类
    ├── Markdown-Mode.xshd       # Markdown语法高亮配置
    ├── assets/
    │   └── icon.ico             # 应用程序图标
    └── Sermon.csproj            # 项目文件
```

## 🚀 安装使用

### 系统要求

- **操作系统**: Windows 10/11、Ubuntu/Linux、macOS
- **运行时**: .NET 10 Runtime
- **开发环境**: JetBrains Rider、Visual Studio、VS Code 或命令行 .NET SDK

### 从源码构建

1. **克隆仓库**
   ```bash
   git clone https://github.com/your-username/Sermon.git
   cd Sermon
   ```

2. **还原依赖包**
   ```bash
   dotnet restore Sermon.sln
   ```

3. **编译项目**
   ```bash
   dotnet build Sermon.sln
   ```

4. **运行应用**
   ```bash
   dotnet run --project Sermon/Sermon.csproj
   ```

构建输出位于：

```text
Sermon/bin/Debug/net10.0/
```

在 Ubuntu 这类 Linux 桌面环境中，直接运行 `dotnet run --project Sermon/Sermon.csproj` 即可启动。

### 发布版本

依赖目标机器安装 .NET 10 Runtime：

```bash
dotnet publish Sermon/Sermon.csproj -c Release -r linux-x64 --self-contained false
```

自包含发布，目标机器不需要单独安装 .NET Runtime：

```bash
dotnet publish Sermon/Sermon.csproj -c Release -r linux-x64 --self-contained true
```

将 `linux-x64` 替换为 `win-x64` 或 `osx-x64` 可以发布对应平台版本。

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
2. **打开项目**: 选择 `Sermon.sln` 解决方案文件
3. **配置SDK**: 确保安装了 .NET 10 SDK；当前 `global.json` 固定到 `10.0.104`

### 代码结构

#### 主要类说明

- `MainWindow`: 主窗口类，包含所有 UI 逻辑和事件处理
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

// Markdown 预览内容
private static StackPanel BuildPreviewContent(string markdown)
{
    // 将常用 Markdown 结构转换为 Avalonia 原生文本控件。
}
```

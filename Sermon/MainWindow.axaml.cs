using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Search;

namespace Sermon;

public partial class MainWindow : Window
{
    private const string DefaultNewFileName = "新建文档.md";

    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly Dictionary<TabItem, ViewMode> _tabViewModes = new();
    private readonly Dictionary<TabItem, bool> _tabModified = new();
    private DispatcherTimer? _autoSaveTimer;
    private DispatcherTimer? _previewRefreshTimer;
    private TabItem? _pendingPreviewTab;
    private bool _isClosingConfirmed;
    private bool _suppressHeadingSelection;
    private ComboBox _headingComboBox = null!;
    private ToggleButton _editModeToggle = null!;
    private ToggleButton _previewModeToggle = null!;
    private ToggleButton _splitModeToggle = null!;
    private ToggleButton _memoryPanelToggle = null!;
    private TreeView _fileTreeView = null!;
    private Grid _mainContentGrid = null!;
    private GridSplitter _memoryPanelSplitter = null!;
    private Border _memoryPanelBorder = null!;
    private TabControl _editorTabControl = null!;
    private TextBlock _wordCountTextBlock = null!;
    private TextBlock _cursorPositionTextBlock = null!;
    private TextBlock _statusTextBlock = null!;
    private bool _isMemoryPanelVisible = true;

    public enum ViewMode
    {
        Edit,
        Preview,
        Split
    }

    public MainWindow()
    {
        InitializeComponent();
        BindNamedControls();
        InitializeEditor();
        InitializeMemorySystem();
        SetMemoryPanelVisible(false);
        SetupAutoSave();
        SetupPreviewRefresh();
        SetupKeyBindings();
        LoadMarkdownHighlighting();
        CreateNewTab(DefaultNewFileName, string.Empty, null);
    }

    private void BindNamedControls()
    {
        _headingComboBox = RequiredControl<ComboBox>("HeadingComboBox");
        _editModeToggle = RequiredControl<ToggleButton>("EditModeToggle");
        _previewModeToggle = RequiredControl<ToggleButton>("PreviewModeToggle");
        _splitModeToggle = RequiredControl<ToggleButton>("SplitModeToggle");
        _memoryPanelToggle = RequiredControl<ToggleButton>("MemoryPanelToggle");
        _fileTreeView = RequiredControl<TreeView>("FileTreeView");
        _mainContentGrid = RequiredControl<Grid>("MainContentGrid");
        _memoryPanelSplitter = RequiredControl<GridSplitter>("MemoryPanelSplitter");
        _memoryPanelBorder = RequiredControl<Border>("MemoryPanelBorder");
        _editorTabControl = RequiredControl<TabControl>("EditorTabControl");
        _wordCountTextBlock = RequiredControl<TextBlock>("WordCountTextBlock");
        _cursorPositionTextBlock = RequiredControl<TextBlock>("CursorPositionTextBlock");
        _statusTextBlock = RequiredControl<TextBlock>("StatusTextBlock");
    }

    private T RequiredControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ??
               throw new InvalidOperationException($"控件 '{name}' 未在 MainWindow.axaml 中找到。");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeEditor()
    {
        _statusTextBlock.Text = "准备就绪";
        _suppressHeadingSelection = true;
        _headingComboBox.SelectedIndex = 0;
        _suppressHeadingSelection = false;
        UpdateViewModeButtons(ViewMode.Edit);
    }

    private void SetupAutoSave()
    {
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(2)
        };
        _autoSaveTimer.Tick += AutoSave_Tick;
        _autoSaveTimer.Start();
    }

    private void SetupPreviewRefresh()
    {
        _previewRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _previewRefreshTimer.Tick += PreviewRefreshTimer_Tick;
    }

    private void SetupKeyBindings()
    {
        KeyBindings.Add(CreateKeyBinding("Ctrl+N", () => NewFileMenuItem_Click(this, new RoutedEventArgs())));
        KeyBindings.Add(CreateKeyBinding("Ctrl+O", () => _ = OpenFileAsync()));
        KeyBindings.Add(CreateKeyBinding("Ctrl+S", SaveCurrentFile));
        KeyBindings.Add(CreateKeyBinding("Ctrl+Shift+S", () => _ = SaveCurrentFileAsAsync()));
        KeyBindings.Add(CreateKeyBinding("Ctrl+B", () => BoldButton_Click(this, new RoutedEventArgs())));
        KeyBindings.Add(CreateKeyBinding("Ctrl+I", () => ItalicButton_Click(this, new RoutedEventArgs())));
        KeyBindings.Add(CreateKeyBinding("Ctrl+F", () => FindMenuItem_Click(this, new RoutedEventArgs())));
        KeyBindings.Add(CreateKeyBinding("Ctrl+H", () => ReplaceMenuItem_Click(this, new RoutedEventArgs())));
    }

    private static KeyBinding CreateKeyBinding(string gesture, Action action)
    {
        return new KeyBinding
        {
            Gesture = KeyGesture.Parse(gesture),
            Command = new RelayCommand(action)
        };
    }

    private void LoadMarkdownHighlighting()
    {
        try
        {
            const string xshdPath = "Markdown-Mode.xshd";
            if (!File.Exists(xshdPath))
            {
                CreateDefaultMarkdownHighlighting();
                return;
            }

            using var reader = new System.Xml.XmlTextReader(xshdPath);
            var customHighlighting = AvaloniaEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md" }, customHighlighting);
        }
        catch
        {
            CreateDefaultMarkdownHighlighting();
        }
    }

    private static void CreateDefaultMarkdownHighlighting()
    {
        var textHighlighting = HighlightingManager.Instance.GetDefinition("TXT");
        if (textHighlighting != null)
        {
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md" }, textHighlighting);
        }
    }

    private void NewFileMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        CreateNewTab(DefaultNewFileName, string.Empty, null);
    }

    private async void OpenFileMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await OpenFileAsync();
    }

    private async Task OpenFileAsync()
    {
        var storageProvider = StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择文本文档",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Markdown文件") { Patterns = new[] { "*.md", "*.markdown" } },
                new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        var filePath = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            OpenFileInNewTab(filePath);
        }
    }

    private void SaveMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentFile();
    }

    private async void SaveAsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await SaveCurrentFileAsAsync();
    }

    private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveCurrentFile()
    {
        if (_editorTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        var filePath = selectedTab.Tag as string;
        if (string.IsNullOrEmpty(filePath))
        {
            _ = SaveCurrentFileAsAsync();
            return;
        }

        if (TrySaveTabToFile(selectedTab, filePath))
        {
            _statusTextBlock.Text = $"已保存: {Path.GetFileName(filePath)}";
        }
    }

    private async Task SaveCurrentFileAsAsync()
    {
        if (_editorTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存文本文档",
            SuggestedFileName = GetTabFileName(selectedTab),
            DefaultExtension = GetDefaultExtension(selectedTab),
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Markdown文件") { Patterns = new[] { "*.md" } },
                new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        var filePath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (TrySaveTabToFile(selectedTab, filePath))
        {
            var oldPath = selectedTab.Tag as string;
            if (!string.IsNullOrEmpty(oldPath))
            {
                _openTabs.Remove(oldPath);
            }

            selectedTab.Tag = filePath;
            _openTabs[filePath] = selectedTab;
            UpdateTabHeader(selectedTab);
            _statusTextBlock.Text = $"已保存: {Path.GetFileName(filePath)}";
        }
    }

    private void BoldButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("**", "**", "加粗文字");
    }

    private void ItalicButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("*", "*", "斜体文字");
    }

    private void StrikethroughButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("~~", "~~", "删除线文字");
    }

    private void HeadingComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressHeadingSelection)
        {
            return;
        }

        if (sender is not ComboBox comboBox)
        {
            return;
        }

        if (comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var prefix = selectedItem.Tag as string;
            if (!string.IsNullOrEmpty(prefix))
            {
                InsertAtLineStart(prefix);
                _suppressHeadingSelection = true;
                comboBox.SelectedIndex = 0;
                _suppressHeadingSelection = false;
            }
        }
    }

    private void BulletListButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertAtLineStart("- ");
    }

    private void NumberedListButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertAtLineStart("1. ");
    }

    private void LinkButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("[", "](url)", "链接文字");
    }

    private void ImageButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("![", "](image-url)", "图片描述");
    }

    private void CodeBlockButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertMarkdownFormat("```\n", "\n```", "代码内容");
    }

    private void TableButton_Click(object? sender, RoutedEventArgs e)
    {
        InsertText("| 列1 | 列2 | 列3 |\n|------|------|------|\n| 内容1 | 内容2 | 内容3 |\n");
    }

    private void InsertMarkdownFormat(string prefix, string suffix, string placeholder)
    {
        EnsureEditableModeForCurrentTab();
        var textEditor = GetCurrentTextEditor();
        if (textEditor == null)
        {
            return;
        }

        var selectedText = textEditor.SelectedText;
        var textToInsert = string.IsNullOrEmpty(selectedText) ? placeholder : selectedText;
        var formattedText = prefix + textToInsert + suffix;
        var selectionStart = textEditor.SelectionStart;

        textEditor.Document.Replace(selectionStart, textEditor.SelectionLength, formattedText);

        if (string.IsNullOrEmpty(selectedText))
        {
            textEditor.SelectionStart = selectionStart + prefix.Length;
            textEditor.SelectionLength = placeholder.Length;
        }
    }

    private void InsertAtLineStart(string prefix)
    {
        EnsureEditableModeForCurrentTab();
        var textEditor = GetCurrentTextEditor();
        if (textEditor == null)
        {
            return;
        }

        var line = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);
        textEditor.Document.Insert(line.Offset, prefix);
    }

    private void InsertText(string text)
    {
        EnsureEditableModeForCurrentTab();
        var textEditor = GetCurrentTextEditor();
        if (textEditor == null)
        {
            return;
        }

        textEditor.Document.Insert(textEditor.CaretOffset, text);
    }

    private bool TrySaveTabToFile(TabItem tab, string filePath, bool showError = true)
    {
        var textEditor = GetTextEditorFromTab(tab);
        if (textEditor == null)
        {
            return false;
        }

        try
        {
            File.WriteAllText(filePath, textEditor.Text);
            _tabModified[tab] = false;
            UpdateTabHeader(tab);
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
            {
                _ = ShowInfoDialogAsync("保存文件失败", ex.Message);
            }
            return false;
        }
    }

    private void EditModeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Edit);
    }

    private void PreviewModeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Preview);
    }

    private void SplitModeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Split);
    }

    private void EditModeToggle_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Edit);
    }

    private void PreviewModeToggle_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Preview);
    }

    private void SplitModeToggle_Click(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ViewMode.Split);
    }

    private void MemoryPanelToggle_Click(object? sender, RoutedEventArgs e)
    {
        SetMemoryPanelVisible(_memoryPanelToggle.IsChecked == true);
    }

    private void ToggleMemoryPanelMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SetMemoryPanelVisible(!_isMemoryPanelVisible);
    }

    private void SetViewMode(ViewMode mode)
    {
        if (_editorTabControl.SelectedItem is TabItem selectedTab)
        {
            _tabViewModes[selectedTab] = mode;
            UpdateTabView(selectedTab, mode);
            UpdateViewModeButtons(mode);
            if (mode is ViewMode.Edit or ViewMode.Split)
            {
                GetTextEditorFromTab(selectedTab)?.Focus();
            }
        }
    }

    private void UpdateViewModeButtons(ViewMode mode)
    {
        _editModeToggle.IsChecked = mode == ViewMode.Edit;
        _previewModeToggle.IsChecked = mode == ViewMode.Preview;
        _splitModeToggle.IsChecked = mode == ViewMode.Split;
    }

    private void SetMemoryPanelVisible(bool visible)
    {
        _isMemoryPanelVisible = visible;
        _memoryPanelBorder.IsVisible = visible;
        _memoryPanelSplitter.IsVisible = visible;
        _memoryPanelToggle.IsChecked = visible;

        if (_mainContentGrid.ColumnDefinitions.Count < 5)
        {
            return;
        }

        _mainContentGrid.ColumnDefinitions[3].Width = visible
            ? new GridLength(1)
            : new GridLength(0);
        _mainContentGrid.ColumnDefinitions[4].Width = visible
            ? new GridLength(320)
            : new GridLength(0);
        _mainContentGrid.ColumnDefinitions[4].MinWidth = visible ? 280 : 0;
    }

    private void FindMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        OpenSearchPanelForCurrentEditor();
    }

    private void ReplaceMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        OpenSearchPanelForCurrentEditor();
    }

    private async void LightThemeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await ShowInfoDialogAsync("主题", "明亮主题已选中");
    }

    private async void DarkThemeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await ShowInfoDialogAsync("主题", "暗黑主题已选中");
    }

    private async void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await ShowInfoDialogAsync("设置", "设置功能即将推出！");
    }

    private async void AboutMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await ShowInfoDialogAsync("关于", "Sermon - 现代Markdown编辑器\n版本 1.0\n\n一个简洁而强大的Markdown编辑器。");
    }

    private TextEditor? GetCurrentTextEditor()
    {
        return _editorTabControl.SelectedItem is TabItem selectedTab
            ? GetTextEditorFromTab(selectedTab)
            : null;
    }

    private static TextEditor? GetTextEditorFromTab(TabItem tab)
    {
        return tab.Content is Grid containerGrid
            ? containerGrid.Children.OfType<TextEditor>().FirstOrDefault()
            : null;
    }

    private void CreateNewTab(string title, string content, string? filePath)
    {
        var document = new TextDocument(content);
        var textEditor = CreateEditor(document);
        textEditor.TextChanged += Editor_TextChanged;
        textEditor.Document.Changed += Document_Changed;
        textEditor.TextArea.Caret.PositionChanged += (_, _) => UpdateCursorPosition(textEditor);

        var preview = CreatePreview(content, filePath);
        var splitter = new GridSplitter
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(220, 220, 214))
        };

        var containerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,0,0")
        };

        Grid.SetColumn(textEditor, 0);
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(preview, 2);
        containerGrid.Children.Add(textEditor);
        containerGrid.Children.Add(splitter);
        containerGrid.Children.Add(preview);

        var header = CreateTabHeader(title);
        var newTab = new TabItem
        {
            Header = header,
            Content = containerGrid,
            Tag = filePath
        };

        if (header.Children.OfType<Button>().FirstOrDefault() is { } closeButton)
        {
            closeButton.Tag = newTab;
        }

        _tabViewModes[newTab] = ViewMode.Edit;
        _tabModified[newTab] = false;

        if (!string.IsNullOrEmpty(filePath))
        {
            _openTabs[filePath] = newTab;
        }

        _editorTabControl.Items.Add(newTab);
        _editorTabControl.SelectedItem = newTab;
        UpdateTabView(newTab, ViewMode.Edit);
        UpdateWordCount(content);
    }

    private static TextEditor CreateEditor(TextDocument document)
    {
        return new TextEditor
        {
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown"),
            ShowLineNumbers = true,
            WordWrap = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Document = document,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 14,
            Padding = new Thickness(18),
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
            LineNumbersForeground = new SolidColorBrush(Color.FromRgb(106, 106, 100))
        };
    }

    private static ScrollViewer CreatePreview(string content, string? filePath)
    {
        return new ScrollViewer
        {
            IsVisible = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            Content = IsPlainTextPath(filePath)
                ? BuildPlainTextContent(content)
                : BuildPreviewContent(content)
        };
    }

    private static StackPanel BuildPreviewContent(string markdown)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(40, 32),
            Spacing = 8
        };

        var inCodeBlock = false;
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = line.Length == 0 ? " " : line,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                    FontSize = 13,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    Padding = new Thickness(10, 4),
                    TextWrapping = TextWrapping.Wrap
                });
                continue;
            }

            if (line.Length == 0)
            {
                panel.Children.Add(new Border { Height = 8 });
                continue;
            }

            var heading = Regex.Match(line, "^(#{1,6})\\s+(.*)$");
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length;
                panel.Children.Add(new TextBlock
                {
                    Text = StripInlineMarkdown(heading.Groups[2].Value),
                    FontSize = Math.Max(18, 34 - level * 3),
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, level == 1 ? 12 : 8, 0, 4),
                    TextWrapping = TextWrapping.Wrap
                });
                continue;
            }

            var quote = Regex.Match(line, "^>\\s?(.*)$");
            if (quote.Success)
            {
                panel.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 0, 27)),
                    BorderThickness = new Thickness(4, 0, 0, 0),
                    Padding = new Thickness(12, 4, 0, 4),
                    Child = new TextBlock
                    {
                        Text = StripInlineMarkdown(quote.Groups[1].Value),
                        Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                        TextWrapping = TextWrapping.Wrap
                    }
                });
                continue;
            }

            var listItem = Regex.Match(line, "^\\s*(-|\\*|\\d+\\.)\\s+(.*)$");
            if (listItem.Success)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "- " + StripInlineMarkdown(listItem.Groups[2].Value),
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap
                });
                continue;
            }

            panel.Children.Add(new TextBlock
            {
                Text = StripInlineMarkdown(line),
                FontSize = 15,
                LineHeight = 24,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "预览将在这里显示",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            });
        }

        return panel;
    }

    private static string StripInlineMarkdown(string text)
    {
        var result = Regex.Replace(text, @"!\[(.*?)\]\((.*?)\)", "$1");
        result = Regex.Replace(result, @"\[(.*?)\]\((.*?)\)", "$1");
        result = Regex.Replace(result, @"(`|\*\*|__|\*|_|~~)", string.Empty);
        return result;
    }

    private static StackPanel BuildPlainTextContent(string text)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(40, 32),
            Spacing = 4
        };

        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            panel.Children.Add(new TextBlock
            {
                Text = line.Length == 0 ? " " : line,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                FontSize = 14,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "预览将在这里显示",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            });
        }

        return panel;
    }

    private static bool IsPlainTextPath(string? filePath)
    {
        return Path.GetExtension(filePath ?? string.Empty)
            .Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateTabView(TabItem tab, ViewMode mode)
    {
        if (!TryGetTabLayout(tab, out var containerGrid, out var textEditor, out var splitter, out var preview))
        {
            return;
        }

        textEditor.IsVisible = false;
        splitter.IsVisible = false;
        preview.IsVisible = false;

        switch (mode)
        {
            case ViewMode.Edit:
                textEditor.IsVisible = true;
                containerGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                containerGrid.ColumnDefinitions[1].Width = new GridLength(0);
                containerGrid.ColumnDefinitions[2].Width = new GridLength(0);
                break;
            case ViewMode.Preview:
                preview.IsVisible = true;
                containerGrid.ColumnDefinitions[0].Width = new GridLength(0);
                containerGrid.ColumnDefinitions[1].Width = new GridLength(0);
                containerGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                RefreshPreview(tab);
                break;
            case ViewMode.Split:
                textEditor.IsVisible = true;
                splitter.IsVisible = true;
                preview.IsVisible = true;
                containerGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                containerGrid.ColumnDefinitions[1].Width = new GridLength(1);
                containerGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                RefreshPreview(tab);
                break;
        }
    }

    private void SchedulePreviewRefresh(TabItem tab)
    {
        _pendingPreviewTab = tab;
        _previewRefreshTimer?.Stop();
        _previewRefreshTimer?.Start();
    }

    private void PreviewRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _previewRefreshTimer?.Stop();
        if (_pendingPreviewTab != null)
        {
            RefreshPreview(_pendingPreviewTab);
            _pendingPreviewTab = null;
        }
    }

    private static void RefreshPreview(TabItem tab)
    {
        if (!TryGetTabLayout(tab, out _, out var textEditor, out _, out var preview))
        {
            return;
        }

        var filePath = tab.Tag as string;
        var content = IsPlainTextPath(filePath)
            ? BuildPlainTextContent(textEditor.Text)
            : BuildPreviewContent(textEditor.Text);
        if (preview.IsVisible)
        {
            preview.Content = content;
        }
    }

    private static bool TryGetTabLayout(
        TabItem tab,
        out Grid containerGrid,
        out TextEditor textEditor,
        out GridSplitter splitter,
        out ScrollViewer preview)
    {
        containerGrid = null!;
        textEditor = null!;
        splitter = null!;
        preview = null!;

        if (tab.Content is not Grid grid)
        {
            return false;
        }

        var editor = grid.Children.OfType<TextEditor>().FirstOrDefault();
        var split = grid.Children.OfType<GridSplitter>().FirstOrDefault();
        var pv = grid.Children.OfType<ScrollViewer>().FirstOrDefault();
        if (editor == null || split == null || pv == null)
        {
            return false;
        }

        containerGrid = grid;
        textEditor = editor;
        splitter = split;
        preview = pv;
        return true;
    }

    private StackPanel CreateTabHeader(string fileName)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var textBlock = new TextBlock
        {
            Text = fileName,
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeButton = new Button
        {
            Content = "x",
            Width = 24,
            Height = 24,
            MinWidth = 24,
            MinHeight = 24,
            Padding = new Thickness(0),
            FontSize = 12,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        closeButton.Click += CloseTab_Click;

        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(closeButton);
        return stackPanel;
    }

    private void UpdateTabHeader(TabItem tab)
    {
        if (tab.Header is StackPanel stackPanel &&
            stackPanel.Children.FirstOrDefault() is TextBlock textBlock)
        {
            var fileName = GetTabFileName(tab);
            var isModified = _tabModified.TryGetValue(tab, out var modified) && modified;
            textBlock.Text = isModified ? fileName + " *" : fileName;
        }
    }

    private static string GetTabFileName(TabItem tab)
    {
        var filePath = tab.Tag as string;
        return string.IsNullOrEmpty(filePath) ? DefaultNewFileName : Path.GetFileName(filePath);
    }

    private async void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        var tab = (sender as Button)?.Tag as TabItem;
        if (tab == null && sender is Visual visual)
        {
            tab = visual.GetVisualAncestors().OfType<TabItem>().FirstOrDefault();
        }

        if (tab != null)
        {
            await CloseTabAsync(tab);
        }
    }

    private async Task<bool> CloseTabAsync(TabItem tab)
    {
        var filePath = tab.Tag as string;
        if (_tabModified.TryGetValue(tab, out var isModified) && isModified)
        {
            var displayName = string.IsNullOrEmpty(filePath) ? DefaultNewFileName : Path.GetFileName(filePath);
            var result = await ShowQuestionDialogAsync("确认", $"文件 '{displayName}' 尚未保存，是否保存？", includeCancel: true);
            if (result == DialogChoice.Yes)
            {
                _editorTabControl.SelectedItem = tab;
                SaveCurrentFile();
                if (_tabModified.TryGetValue(tab, out var stillModified) && stillModified)
                {
                    return false;
                }
            }
            else if (result == DialogChoice.Cancel)
            {
                return false;
            }
        }

        _editorTabControl.Items.Remove(tab);
        if (!string.IsNullOrEmpty(filePath))
        {
            _openTabs.Remove(filePath);
        }
        _tabViewModes.Remove(tab);
        _tabModified.Remove(tab);
        return true;
    }

    private async void OpenFolderMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "请选择一个文件夹",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        _fileTreeView.Items.Clear();
        var rootNode = new TreeViewItem
        {
            Header = Path.GetFileName(folderPath),
            Tag = folderPath,
            FontWeight = FontWeight.Bold
        };
        _fileTreeView.Items.Add(rootNode);
        LoadDirectory(folderPath, rootNode);
        rootNode.IsExpanded = true;
    }

    private static void LoadDirectory(string path, TreeViewItem parentNode)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(Path.GetFileName))
            {
                var node = new TreeViewItem { Header = Path.GetFileName(dir), Tag = dir };
                parentNode.Items.Add(node);
                LoadDirectory(dir, node);
            }

            foreach (var file in Directory.GetFiles(path).Where(IsSupportedTextFile).OrderBy(Path.GetFileName))
            {
                parentNode.Items.Add(new TreeViewItem { Header = Path.GetFileName(file), Tag = file });
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch
        {
        }
    }

    private static bool IsSupportedTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDefaultExtension(TabItem tab)
    {
        var filePath = tab.Tag as string;
        var extension = Path.GetExtension(filePath ?? string.Empty);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ? "txt" : "md";
    }

    private void FileTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fileTreeView.SelectedItem is TreeViewItem selectedItem)
        {
            var path = selectedItem.Tag as string;
            if (path != null && File.Exists(path))
            {
                OpenFileInNewTab(path);
            }
        }
    }

    private void OpenFileInNewTab(string filePath)
    {
        if (_openTabs.TryGetValue(filePath, out var existingTab))
        {
            _editorTabControl.SelectedItem = existingTab;
            return;
        }

        try
        {
            var content = File.ReadAllText(filePath);
            CreateNewTab(Path.GetFileName(filePath), content, filePath);
            _statusTextBlock.Text = $"已打开: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            _ = ShowInfoDialogAsync("打开文件失败", ex.Message);
        }
    }

    private void EditorTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl ||
            tabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        var textEditor = GetTextEditorFromTab(selectedTab);
        if (textEditor != null)
        {
            UpdateWordCount(textEditor.Text);
            UpdateCursorPosition(textEditor);
        }

        if (!_tabViewModes.TryGetValue(selectedTab, out var viewMode))
        {
            viewMode = ViewMode.Edit;
            _tabViewModes[selectedTab] = viewMode;
        }
        UpdateTabView(selectedTab, viewMode);
        UpdateViewModeButtons(viewMode);

        if (viewMode is ViewMode.Edit or ViewMode.Split)
        {
            GetTextEditorFromTab(selectedTab)?.Focus();
        }
        SyncMemoryPanelWithCurrentTab();
    }

    private void EnsureEditableModeForCurrentTab()
    {
        if (_editorTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        if (_tabViewModes.TryGetValue(selectedTab, out var mode) && mode == ViewMode.Preview)
        {
            SetViewMode(ViewMode.Edit);
        }
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextEditor textEditor)
        {
            return;
        }

        UpdateWordCount(textEditor.Text);
        UpdateCursorPosition(textEditor);

        if (FindTabForDocument(textEditor.Document) is { } tab &&
            _tabViewModes.TryGetValue(tab, out var viewMode) &&
            viewMode != ViewMode.Edit)
        {
            SchedulePreviewRefresh(tab);
        }
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs e)
    {
        if (FindTabForDocument(sender as TextDocument) is { } tab)
        {
            _tabModified[tab] = true;
            UpdateTabHeader(tab);
        }
    }

    private TabItem? FindTabForDocument(TextDocument? document)
    {
        if (document == null)
        {
            return null;
        }

        return _editorTabControl.Items
            .OfType<TabItem>()
            .FirstOrDefault(tab => GetTextEditorFromTab(tab)?.Document == document);
    }

    private void OpenSearchPanelForCurrentEditor()
    {
        var textEditor = GetCurrentTextEditor();
        if (textEditor != null)
        {
            SearchPanel.Install(textEditor);
        }
    }

    private void UpdateWordCount(string text)
    {
        var charCount = text.Length;
        var wordCount = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        _wordCountTextBlock.Text = $"字数: {wordCount}  字符: {charCount}";
    }

    private void UpdateCursorPosition(TextEditor textEditor)
    {
        var location = textEditor.Document.GetLocation(textEditor.CaretOffset);
        _cursorPositionTextBlock.Text = $"行: {location.Line}, 列: {location.Column}";
    }

    private void AutoSave_Tick(object? sender, EventArgs e)
    {
        foreach (var kvp in _tabModified.Where(kvp => kvp.Value).ToList())
        {
            var tab = kvp.Key;
            var filePath = tab.Tag as string;
            if (!string.IsNullOrEmpty(filePath) && TrySaveTabToFile(tab, filePath, showError: false))
            {
                _statusTextBlock.Text = $"自动保存: {Path.GetFileName(filePath)}";
            }
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosingConfirmed)
        {
            base.OnClosing(e);
            return;
        }

        var unsavedFiles = _tabModified.Where(kvp => kvp.Value).ToList();
        if (!unsavedFiles.Any())
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        var fileNames = string.Join(", ", unsavedFiles.Select(kvp => GetTabFileName(kvp.Key)));
        var result = await ShowQuestionDialogAsync(
            "确认退出",
            $"以下文件尚未保存:\n{fileNames}\n\n是否保存所有更改？",
            includeCancel: true);

        if (result == DialogChoice.Cancel)
        {
            return;
        }

        if (result == DialogChoice.Yes)
        {
            foreach (var kvp in unsavedFiles)
            {
                var tab = kvp.Key;
                _editorTabControl.SelectedItem = tab;
                var filePath = tab.Tag as string;
                if (string.IsNullOrEmpty(filePath))
                {
                    await SaveCurrentFileAsAsync();
                }
                else
                {
                    TrySaveTabToFile(tab, filePath);
                }

                if (_tabModified.TryGetValue(tab, out var stillModified) && stillModified)
                {
                    return;
                }
            }
        }

        _isClosingConfirmed = true;
        Close();
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        await ShowDialogAsync(title, message, DialogButtons.Ok);
    }

    private async Task<DialogChoice> ShowQuestionDialogAsync(string title, string message, bool includeCancel)
    {
        return await ShowDialogAsync(title, message, includeCancel ? DialogButtons.YesNoCancel : DialogButtons.YesNo);
    }

    private async Task<DialogChoice> ShowDialogAsync(string title, string message, DialogButtons buttons)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        dialog.Content = BuildDialogContent(message, buttons);

        return await dialog.ShowDialog<DialogChoice>(this);

        Control BuildDialogContent(string body, DialogButtons dialogButtons)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 18
            };

            panel.Children.Add(new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap
            });

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };

            void AddButton(string caption, DialogChoice choice)
            {
                var button = new Button
                {
                    Content = caption,
                    MinWidth = 76
                };
                button.Click += (_, _) => dialog.Close(choice);
                buttonsPanel.Children.Add(button);
            }

            if (dialogButtons == DialogButtons.Ok)
            {
                AddButton("确定", DialogChoice.Ok);
            }
            else
            {
                AddButton("是", DialogChoice.Yes);
                AddButton("否", DialogChoice.No);
                if (dialogButtons == DialogButtons.YesNoCancel)
                {
                    AddButton("取消", DialogChoice.Cancel);
                }
            }

            panel.Children.Add(buttonsPanel);
            return panel;
        }
    }

    private enum DialogButtons
    {
        Ok,
        YesNo,
        YesNoCancel
    }

    private enum DialogChoice
    {
        Ok,
        Yes,
        No,
        Cancel
    }
}

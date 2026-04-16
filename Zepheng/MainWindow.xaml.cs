using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;
using Markdig;
using System.Windows.Media;
using MahApps.Metro.Controls;
using Microsoft.Web.WebView2.Wpf;
using System.Linq;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using FontFamily = System.Windows.Media.FontFamily;

namespace Zepheng 
{
    public partial class MainWindow : MetroWindow
    {
        private readonly Dictionary<string, MetroTabItem> _openTabs = new Dictionary<string, MetroTabItem>();
        private readonly Dictionary<MetroTabItem, ViewMode> _tabViewModes = new Dictionary<MetroTabItem, ViewMode>();
        private readonly Dictionary<MetroTabItem, bool> _tabModified = new Dictionary<MetroTabItem, bool>();
        private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        private DispatcherTimer? _autoSaveTimer;
        private DispatcherTimer? _previewRefreshTimer;
        private MetroTabItem? _pendingPreviewTab;
        
        public enum ViewMode
        {
            Edit,
            Preview,
            Split
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeEditor();
            SetupAutoSave();
            SetupPreviewRefresh();
            SetupKeyBindings();
            LoadMarkdownHighlighting();
        }

        private void InitializeEditor()
        {
            // 设置默认状态
            StatusTextBlock.Text = "准备就绪";
            HeadingComboBox.SelectedIndex = 0;
            
            // 设置默认视图模式
            EditModeToggle.IsChecked = true;
            PreviewModeToggle.IsChecked = false;
            SplitModeToggle.IsChecked = false;
        }

        private void SetupAutoSave()
        {
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(2) // 每2分钟自动保存
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
            // 文件操作
            InputBindings.Add(new KeyBinding(new RelayCommand(() => NewFileMenuItem_Click(null!, null!)), Key.N, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => OpenFileMenuItem_Click(null!, null!)), Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SaveMenuItem_Click(null!, null!)), Key.S, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SaveAsMenuItem_Click(null!, null!)), Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            
            // 格式化
            InputBindings.Add(new KeyBinding(new RelayCommand(() => BoldButton_Click(null!, null!)), Key.B, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => ItalicButton_Click(null!, null!)), Key.I, ModifierKeys.Control));
            
            // 查找替换
            InputBindings.Add(new KeyBinding(new RelayCommand(() => FindMenuItem_Click(null!, null!)), Key.F, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => ReplaceMenuItem_Click(null!, null!)), Key.H, ModifierKeys.Control));
        }

        private void LoadMarkdownHighlighting()
        {
            try
            {
                string xshdPath = "Markdown-Mode.xshd";
                if (!File.Exists(xshdPath))
                {
                    CreateDefaultMarkdownHighlighting();
                    return;
                }

                using (var stream = new System.Xml.XmlTextReader(xshdPath))
                {
                    var customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(stream, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md" }, customHighlighting);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载语法高亮文件失败: {ex.Message}\n将使用默认高亮。");
                CreateDefaultMarkdownHighlighting();
            }
        }

        private void CreateDefaultMarkdownHighlighting()
        {
            try
            {
                var textHighlighting = HighlightingManager.Instance.GetDefinition("TXT");
                if (textHighlighting != null)
                {
                    HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md" }, textHighlighting);
                }
            }
            catch { }
        }
        
        // ===== 文件操作 =====
        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab("新建文档.md", "", null!);
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Markdown文件 (*.md)|*.md|所有文件 (*.*)|*.*",
                Title = "选择Markdown文件"
            };

            if (dialog.ShowDialog() == true)
            {
                OpenFileInNewTab(dialog.FileName);
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFile();
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFileAs();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveCurrentFile()
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                var filePath = selectedTab.Tag as string;
                if (string.IsNullOrEmpty(filePath))
                {
                    SaveCurrentFileAs();
                    return;
                }

                var textEditor = GetTextEditorFromTab(selectedTab);
                if (textEditor != null)
                {
                    try
                    {
                            File.WriteAllText(filePath, textEditor.Text);
                            _tabModified[selectedTab] = false;
                            UpdateTabHeader(selectedTab);
                            StatusTextBlock.Text = $"已保存: {Path.GetFileName(filePath)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存文件失败: {ex.Message}");
                    }
                }
            }
        }

        private void SaveCurrentFileAs()
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Markdown文件 (*.md)|*.md|所有文件 (*.*)|*.*",
                    Title = "保存Markdown文件"
                };

                if (dialog.ShowDialog() == true)
                {
                    var textEditor = GetTextEditorFromTab(selectedTab);
                    if (textEditor != null)
                    {
                        try
                        {
                            File.WriteAllText(dialog.FileName, textEditor.Text);
                            
                            // 更新标签页信息
                            var oldPath = selectedTab.Tag as string;
                            if (!string.IsNullOrEmpty(oldPath) && _openTabs.ContainsKey(oldPath))
                            {
                                _openTabs.Remove(oldPath);
                            }
                            
                            selectedTab.Tag = dialog.FileName;
                            _openTabs[dialog.FileName] = selectedTab;
                            _tabModified[selectedTab] = false;
                            UpdateTabHeader(selectedTab);
                            StatusTextBlock.Text = $"已保存: {Path.GetFileName(dialog.FileName)}";
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"保存文件失败: {ex.Message}");
                        }
                    }
                }
            }
        }

        // ===== 工具栏功能 =====
        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("**", "**", "加粗文字");
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("*", "*", "斜体文字");
        }

        private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("~~", "~~", "删除线文字");
        }

        private void HeadingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HeadingComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var prefix = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(prefix))
                {
                    InsertAtLineStart(prefix);
                }
            }
        }

        private void BulletListButton_Click(object sender, RoutedEventArgs e)
        {
            InsertAtLineStart("- ");
        }

        private void NumberedListButton_Click(object sender, RoutedEventArgs e)
        {
            InsertAtLineStart("1. ");
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("[", "](url)", "链接文字");
        }

        private void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("![", "](image-url)", "图片描述");
        }

        private void CodeBlockButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownFormat("```\n", "\n```", "代码内容");
        }

        private void TableButton_Click(object sender, RoutedEventArgs e)
        {
            var tableMarkdown = "| 列1 | 列2 | 列3 |\n|------|------|------|\n| 内容1 | 内容2 | 内容3 |\n";
            InsertText(tableMarkdown);
        }

        private void InsertMarkdownFormat(string prefix, string suffix, string placeholder)
        {
            var textEditor = GetCurrentTextEditor();
            if (textEditor == null) return;

            var selectedText = textEditor.SelectedText;
            var textToInsert = string.IsNullOrEmpty(selectedText) ? placeholder : selectedText;
            var formattedText = prefix + textToInsert + suffix;
            
            textEditor.Document.Replace(textEditor.SelectionStart, textEditor.SelectionLength, formattedText);
            
            if (string.IsNullOrEmpty(selectedText))
            {
                textEditor.SelectionStart = textEditor.SelectionStart - suffix.Length - placeholder.Length;
                textEditor.SelectionLength = placeholder.Length;
            }
        }

        private void InsertAtLineStart(string prefix)
        {
            var textEditor = GetCurrentTextEditor();
            if (textEditor == null) return;

            var line = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);
            textEditor.Document.Insert(line.Offset, prefix);
        }

        private void InsertText(string text)
        {
            var textEditor = GetCurrentTextEditor();
            if (textEditor == null) return;

            textEditor.Document.Insert(textEditor.CaretOffset, text);
        }
        
        // ===== 视图模式切换 =====
        private void EditModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Edit);
        }

        private void PreviewModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Preview);
        }

        private void SplitModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Split);
        }

        private void EditModeToggle_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Edit);
        }

        private void PreviewModeToggle_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Preview);
        }

        private void SplitModeToggle_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Split);
        }

        private void SetViewMode(ViewMode mode)
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                _tabViewModes[selectedTab] = mode;
                UpdateTabView(selectedTab, mode);
                UpdateViewModeButtons(mode);
            }
        }

        private void UpdateViewModeButtons(ViewMode mode)
        {
            EditModeToggle.IsChecked = mode == ViewMode.Edit;
            PreviewModeToggle.IsChecked = mode == ViewMode.Preview;
            SplitModeToggle.IsChecked = mode == ViewMode.Split;
        }

        // ===== 查找替换功能 =====
        private void FindMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var textEditor = GetCurrentTextEditor();
            if (textEditor != null)
            {
                SearchPanel.Install(textEditor);
            }
        }

        private void ReplaceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var textEditor = GetCurrentTextEditor();
            if (textEditor != null)
            {
                SearchPanel.Install(textEditor);
            }
        }

        // ===== 主题切换 =====
        private void LightThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 目前不实现主题切换，留待后续完善
            MessageBox.Show("明亮主题已选中", "主题");
        }

        private void DarkThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 目前不实现主题切换，留待后续完善
            MessageBox.Show("暗黑主题已选中", "主题");
        }

        // ===== 设置和关于 =====
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("设置功能即将推出！", "设置");
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sermon - 现代Markdown编辑器\n版本 1.0\n\n一个简洁而强大的Markdown编辑器。", "关于");
        }

        // ===== 核心辅助方法 =====
        private TextEditor? GetCurrentTextEditor()
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                return GetActiveTextEditorFromTab(selectedTab);
            }
            return null;
        }

        private TextEditor? GetActiveTextEditorFromTab(MetroTabItem tab)
        {
            if (tab.Content is Grid containerGrid && containerGrid.Children.Count >= 3)
            {
                if (containerGrid.Children[2] is Grid splitGrid &&
                    splitGrid.Visibility == Visibility.Visible &&
                    splitGrid.Children.Count > 0 &&
                    splitGrid.Children[0] is TextEditor splitTextEditor)
                {
                    return splitTextEditor;
                }

                return containerGrid.Children.OfType<TextEditor>().FirstOrDefault();
            }
            return null;
        }

        private TextEditor? GetTextEditorFromTab(MetroTabItem tab)
        {
            if (tab.Content is Grid containerGrid)
            {
                return containerGrid.Children.OfType<TextEditor>().FirstOrDefault();
            }
            return null;
        }

        private void CreateNewTab(string title, string content, string? filePath)
        {
            var textEditor = new TextEditor
            {
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                ShowLineNumbers = true,
                WordWrap = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = content
            };
            
            textEditor.TextChanged += Editor_TextChanged;
            textEditor.Document.Changed += Document_Changed;

            // 创建WebView2用于预览
            var webView = new WebView2
            {
                Visibility = Visibility.Collapsed
            };
            
            // 创建分屏网格
            var splitGrid = new Grid
            {
                Visibility = Visibility.Collapsed
            };
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var textEditorClone = new TextEditor
            {
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                ShowLineNumbers = true,
                WordWrap = true,
                Document = textEditor.Document
            };
            textEditorClone.TextChanged += Editor_TextChanged;
            
            var webViewClone = new WebView2();
            var splitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.LightGray)
            };
            
            Grid.SetColumn(textEditorClone, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(webViewClone, 2);
            
            splitGrid.Children.Add(textEditorClone);
            splitGrid.Children.Add(splitter);
            splitGrid.Children.Add(webViewClone);

            // 创建主容器
            var containerGrid = new Grid();
            containerGrid.Children.Add(textEditor);
            containerGrid.Children.Add(webView);
            containerGrid.Children.Add(splitGrid);

            var newTab = new MetroTabItem
            {
                Header = CreateTabHeader(title, filePath),
                Content = containerGrid,
                Tag = filePath
            };

            _tabViewModes[newTab] = ViewMode.Edit;
            _tabModified[newTab] = false;

            if (!string.IsNullOrEmpty(filePath))
            {
                _openTabs[filePath] = newTab;
            }
            
            EditorTabControl.Items.Add(newTab);
            EditorTabControl.SelectedItem = newTab;
            
            // 初始化WebView
            InitializeWebView(webView, content);
            InitializeWebView(webViewClone, content);
            
            UpdateWordCount(content);
        }
        
        private async void InitializeWebView(WebView2 webView, string markdownContent)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                var htmlContent = ConvertMarkdownToHtml(markdownContent);
                webView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebView初始化失败: {ex.Message}");
            }
        }
        
        private string ConvertMarkdownToHtml(string markdown)
        {
            var htmlContent = Markdown.ToHtml(markdown, _markdownPipeline);
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
            background-color: #fff;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
        }}
        h1 {{ border-bottom: 1px solid #eaecef; padding-bottom: 10px; }}
        h2 {{ border-bottom: 1px solid #eaecef; padding-bottom: 8px; }}
        code {{
            background-color: #f3f4f6;
            padding: 2px 4px;
            border-radius: 3px;
            font-family: 'Courier New', monospace;
        }}
        pre {{
            background-color: #f6f8fa;
            border-radius: 6px;
            padding: 16px;
            overflow: auto;
        }}
        blockquote {{
            border-left: 4px solid #dfe2e5;
            padding-left: 16px;
            color: #6a737d;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
        }}
        th, td {{
            border: 1px solid #dfe2e5;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background-color: #f6f8fa;
            font-weight: 600;
        }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
{htmlContent}
</body>
</html>";
        }
        
        private void UpdateTabView(MetroTabItem tab, ViewMode mode)
        {
            if (tab.Content is Grid containerGrid && containerGrid.Children.Count >= 3)
            {
                var textEditor = containerGrid.Children[0] as TextEditor;
                var webView = containerGrid.Children[1] as WebView2;
                var splitGrid = containerGrid.Children[2] as Grid;
                
                // 隐藏所有视图
                if (textEditor != null) textEditor.Visibility = Visibility.Collapsed;
                if (webView != null) webView.Visibility = Visibility.Collapsed;
                if (splitGrid != null) splitGrid.Visibility = Visibility.Collapsed;
                
                switch (mode)
                {
                    case ViewMode.Edit:
                        if (textEditor != null) textEditor.Visibility = Visibility.Visible;
                        break;
                    case ViewMode.Preview:
                        if (webView != null) webView.Visibility = Visibility.Visible;
                        RefreshPreview(tab);
                        break;
                    case ViewMode.Split:
                        if (splitGrid != null) splitGrid.Visibility = Visibility.Visible;
                        if (splitGrid != null && splitGrid.Children.Count >= 3 && textEditor != null)
                        {
                            RefreshPreview(tab);
                        }
                        break;
                }
            }
        }

        private void SchedulePreviewRefresh(MetroTabItem tab)
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

        private void RefreshPreview(MetroTabItem tab)
        {
            if (tab.Content is not Grid containerGrid || containerGrid.Children.Count < 3)
            {
                return;
            }

            var textEditor = containerGrid.Children[0] as TextEditor;
            if (textEditor == null)
            {
                return;
            }

            var htmlContent = ConvertMarkdownToHtml(textEditor.Text);
            if (containerGrid.Children[1] is WebView2 webView && webView.Visibility == Visibility.Visible)
            {
                webView.NavigateToString(htmlContent);
            }

            if (containerGrid.Children[2] is Grid splitGrid &&
                splitGrid.Visibility == Visibility.Visible &&
                splitGrid.Children.Count >= 3 &&
                splitGrid.Children[2] is WebView2 splitWebView)
            {
                splitWebView.NavigateToString(htmlContent);
            }
        }
        
        private StackPanel CreateTabHeader(string fileName, string? filePath)
        {
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var textBlock = new TextBlock { Text = fileName };
            textBlock.Margin = new Thickness(0, 0, 5, 0);
            
            var closeButton = new Button();
            closeButton.Content = "✕";
            closeButton.Width = 16;
            closeButton.Height = 16;
            closeButton.FontSize = 10;
            closeButton.Tag = null;
            closeButton.Click += CloseTab_Click;
            
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(closeButton);
            
            return stackPanel;
        }
        
        private void UpdateTabHeader(MetroTabItem tab)
        {
            if (tab.Header is StackPanel stackPanel && stackPanel.Children.Count > 0)
            {
                var textBlock = stackPanel.Children[0] as TextBlock;
                if (textBlock != null)
                {
                    var filePath = tab.Tag as string;
                    var fileName = string.IsNullOrEmpty(filePath) ? "新建文档.md" : Path.GetFileName(filePath);
                    var isModified = _tabModified.TryGetValue(tab, out var modified) && modified;
                    textBlock.Text = isModified ? fileName + " *" : fileName;
                }
            }
        }
        
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject source)
            {
                var tab = FindParent<MetroTabItem>(source) ?? FindTabByHeaderButton(sender as Button);
                if (tab != null)
                {
                    var filePath = tab.Tag as string;

                    if (_tabModified.TryGetValue(tab, out var isModified) && isModified)
                    {
                        var displayName = string.IsNullOrEmpty(filePath) ? "新建文档.md" : Path.GetFileName(filePath);
                        var result = MessageBox.Show($"文件 '{displayName}' 尚未保存，是否保存？", "确认", MessageBoxButton.YesNoCancel);
                        if (result == MessageBoxResult.Yes)
                        {
                            EditorTabControl.SelectedItem = tab;
                            SaveCurrentFile();
                            if (_tabModified.TryGetValue(tab, out var stillModified) && stillModified)
                            {
                                return;
                            }
                        }
                        else if (result == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                    }
                    
                    EditorTabControl.Items.Remove(tab);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        _openTabs.Remove(filePath);
                    }
                    _tabViewModes.Remove(tab);
                    _tabModified.Remove(tab);
                }
            }
        }

        private MetroTabItem? FindTabByHeaderButton(Button? button)
        {
            if (button == null)
            {
                return null;
            }

            return EditorTabControl.Items
                .OfType<MetroTabItem>()
                .FirstOrDefault(tab => tab.Header is StackPanel stackPanel && stackPanel.Children.Contains(button));
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                {
                    return typedParent;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "请选择一个文件夹"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileTreeView.Items.Clear();
                var rootNode = new TreeViewItem
                {
                    Header = Path.GetFileName(dialog.SelectedPath),
                    Tag = dialog.SelectedPath,
                    FontWeight = FontWeights.Bold
                };
                FileTreeView.Items.Add(rootNode);
                LoadDirectory(dialog.SelectedPath, rootNode);
            }
        }

        private void LoadDirectory(string path, TreeViewItem parentNode)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var node = new TreeViewItem { Header = Path.GetFileName(dir), Tag = dir };
                    parentNode.Items.Add(node);
                    LoadDirectory(dir, node);
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        var node = new TreeViewItem { Header = Path.GetFileName(file), Tag = file };
                        parentNode.Items.Add(node);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex) { Console.WriteLine($"加载目录失败: {ex.Message}"); }
        }
        
        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FileTreeView.SelectedItem is TreeViewItem selectedItem)
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
            if (_openTabs.ContainsKey(filePath))
            {
                EditorTabControl.SelectedItem = _openTabs[filePath];
                return;
            }

            try
            {
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileName(filePath);
                CreateNewTab(fileName, content, filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败: {ex.Message}");
            }
        }
        
        private void EditorTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                if (selectedTab.Content is Grid)
                {
                    var textEditor = GetTextEditorFromTab(selectedTab);
                    if (textEditor != null)
                    {
                        UpdateWordCount(textEditor.Text);
                        UpdateCursorPosition(textEditor);
                        
                        if (_tabViewModes.TryGetValue(selectedTab, out var viewMode))
                        {
                            UpdateViewModeButtons(viewMode);
                        }
                    }
                }
            }
        }
        
        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            if (sender is TextEditor textEditor)
            {
                UpdateWordCount(textEditor.Text);
                UpdateCursorPosition(textEditor);
                
                // 标记为已修改
                if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
                {
                    _tabModified[selectedTab] = true;
                    UpdateTabHeader(selectedTab);

                    if (_tabViewModes.TryGetValue(selectedTab, out var viewMode) && viewMode != ViewMode.Edit)
                    {
                        SchedulePreviewRefresh(selectedTab);
                    }
                }
            }
        }
        
        private void Document_Changed(object? sender, DocumentChangeEventArgs e)
        {
            if (EditorTabControl.SelectedItem is MetroTabItem selectedTab)
            {
                _tabModified[selectedTab] = true;
                UpdateTabHeader(selectedTab);
            }
        }
        
        private void UpdateWordCount(string text)
        {
            int charCount = text.Length;
            int wordCount = string.IsNullOrWhiteSpace(text) ? 0 : 
                text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            WordCountTextBlock.Text = $"字数: {wordCount}  字符: {charCount}";
        }
        
        private void UpdateCursorPosition(TextEditor textEditor)
        {
            var location = textEditor.Document.GetLocation(textEditor.CaretOffset);
            CursorPositionTextBlock.Text = $"行: {location.Line}, 列: {location.Column}";
        }
        
        private void AutoSave_Tick(object? sender, EventArgs e)
        {
            foreach (var kvp in _tabModified.Where(kvp => kvp.Value).ToList())
            {
                var tab = kvp.Key;
                var filePath = tab.Tag as string;
                if (!string.IsNullOrEmpty(filePath))
                {
                    var textEditor = GetTextEditorFromTab(tab);
                    if (textEditor != null)
                    {
                        try
                        {
                            File.WriteAllText(filePath, textEditor.Text);
                            _tabModified[tab] = false;
                            UpdateTabHeader(tab);
                            StatusTextBlock.Text = $"自动保存: {Path.GetFileName(filePath)}";
                        }
                        catch
                        {
                            // 自动保存失败时忽略错误
                        }
                    }
                }
            }
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 检查是否有未保存的文件
            var unsavedFiles = _tabModified.Where(kvp => kvp.Value).ToList();
            if (unsavedFiles.Any())
            {
                var fileNames = string.Join(", ", unsavedFiles.Select(kvp =>
                {
                    var filePath = kvp.Key.Tag as string;
                    return string.IsNullOrEmpty(filePath) ? "新建文档.md" : Path.GetFileName(filePath);
                }));
                var result = MessageBox.Show(
                    $"以下文件尚未保存:\n{fileNames}\n\n是否保存所有更改？", 
                    "确认退出", 
                    MessageBoxButton.YesNoCancel);
                    
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var kvp in unsavedFiles)
                    {
                        var tab = kvp.Key;
                        EditorTabControl.SelectedItem = tab;
                        var textEditor = GetTextEditorFromTab(tab);
                        if (textEditor != null)
                        {
                            var filePath = tab.Tag as string;
                            if (string.IsNullOrEmpty(filePath))
                            {
                                SaveCurrentFileAs();
                            }
                            else
                            {
                                try
                                {
                                    File.WriteAllText(filePath, textEditor.Text);
                                    _tabModified[tab] = false;
                                    UpdateTabHeader(tab);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"保存文件 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                                }
                            }

                            if (_tabModified.TryGetValue(tab, out var stillModified) && stillModified)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            
            base.OnClosing(e);
        }
    }
}

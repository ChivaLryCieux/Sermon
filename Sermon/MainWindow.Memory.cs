using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sermon.Services;
using Sermon.ViewModels;

namespace Sermon;

public partial class MainWindow
{
    private readonly MemoryPanelViewModel _memoryPanel = new(new MemoryService());
    private TextBox _memorySearchTextBox = null!;
    private TextBox _memoryIntentTextBox = null!;
    private ListBox _memoryListBox = null!;
    private ListBox _backlinkListBox = null!;
    private TextBlock _memoryStatsTextBlock = null!;

    private void InitializeMemorySystem()
    {
        _memorySearchTextBox = RequiredControl<TextBox>("MemorySearchTextBox");
        _memoryIntentTextBox = RequiredControl<TextBox>("MemoryIntentTextBox");
        _memoryListBox = RequiredControl<ListBox>("MemoryListBox");
        _backlinkListBox = RequiredControl<ListBox>("BacklinkListBox");
        _memoryStatsTextBlock = RequiredControl<TextBlock>("MemoryStatsTextBlock");
        _memoryPanel.Results.CollectionChanged += (_, _) => RenderMemoryResults();
        _memoryPanel.Backlinks.CollectionChanged += (_, _) => RenderBacklinks();
        _memoryPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MemoryPanelViewModel.StatsText))
            {
                _memoryStatsTextBlock.Text = _memoryPanel.StatsText;
            }
        };
        _ = LoadMemoryStoreAsync();
    }

    private async Task LoadMemoryStoreAsync()
    {
        try
        {
            await _memoryPanel.LoadAsync();
            RenderMemoryResults();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = "记忆库加载失败";
            await ShowInfoDialogAsync("记忆库", $"加载失败: {ex.Message}");
        }
    }

    private void RenderMemoryResults()
    {
        _memoryListBox.ItemsSource = _memoryPanel.Results.Select(MemoryService.DisplayText).ToList();
        _memoryStatsTextBlock.Text = _memoryPanel.StatsText;
    }

    private void RenderBacklinks()
    {
        if (_memoryPanel.Backlinks.Count == 0)
        {
            return;
        }

        _backlinkListBox.ItemsSource = _memoryPanel.Backlinks.Select(x => x.FileName).ToList();
    }

    private string GetCurrentDocumentText()
    {
        return GetCurrentTextEditor()?.Text ?? string.Empty;
    }

    private string GetCurrentSourcePath()
    {
        if (_editorTabControl.SelectedItem is TabItem selectedTab && selectedTab.Tag is string path && path.Length > 0)
        {
            return path;
        }

        return "未保存文档";
    }

    private async void ExtractMemoryButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = GetCurrentDocumentText();
        if (string.IsNullOrWhiteSpace(content))
        {
            await ShowInfoDialogAsync("AI记忆", "当前文档为空，无法提取记忆。");
            return;
        }

        var sourcePath = GetCurrentSourcePath();
        var changed = await _memoryPanel.ExtractFromAsync(content, sourcePath);
        RenderMemoryResults();
        _statusTextBlock.Text = $"记忆提取完成，新增/更新 {changed} 条";
    }

    private async void GenerateContextPackButton_Click(object? sender, RoutedEventArgs e)
    {
        _memoryPanel.Intent = _memoryIntentTextBox.Text?.Trim() ?? string.Empty;
        var pack = _memoryPanel.BuildContextPack();
        var editor = GetCurrentTextEditor();
        if (editor == null)
        {
            await ShowInfoDialogAsync("AI记忆", "当前没有可写入的文档页。");
            return;
        }

        var insert = $"\n\n---\n\n{pack}\n";
        editor.Document.Insert(editor.CaretOffset, insert);
        _statusTextBlock.Text = "已生成 AI 上下文包并插入文档";
    }

    private async void DeleteSelectedMemoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_memoryListBox.SelectedItem is not string selectedText)
        {
            await ShowInfoDialogAsync("AI记忆", "请先选择要删除的记忆。");
            return;
        }

        var match = _memoryPanel.CurrentResults.FirstOrDefault(x =>
            selectedText.Contains(x.Title, StringComparison.Ordinal) &&
            selectedText.Contains(x.Summary, StringComparison.Ordinal));
        if (match == null)
        {
            await ShowInfoDialogAsync("AI记忆", "未找到要删除的记忆条目。");
            return;
        }

        await _memoryPanel.DeleteAsync(match);
        RenderMemoryResults();
        _statusTextBlock.Text = "已删除选中的记忆";
    }

    private void MemorySearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshMemoryList(_memorySearchTextBox.Text);
    }

    private void RefreshMemoryList(string? query)
    {
        _memoryPanel.SearchQuery = query ?? string.Empty;
        RenderMemoryResults();
    }

    private void UpdateMemoryStats()
    {
        _memoryPanel.RefreshStats();
        _memoryStatsTextBlock.Text = _memoryPanel.StatsText;
    }

    private void SyncMemoryPanelWithCurrentTab()
    {
        _memoryPanel.RefreshResults();
        _memoryPanel.RefreshStats();
        RenderMemoryResults();
        _ = RefreshBacklinksAsync();
    }

    private void RefreshMemoryUi()
    {
        SyncMemoryPanelWithCurrentTab();
    }

    private async Task RefreshBacklinksAsync()
    {
        var currentPath = GetCurrentSourcePath();
        if (string.IsNullOrEmpty(_currentVaultPath)
            || string.IsNullOrEmpty(currentPath)
            || currentPath == "未保存文档")
        {
            _backlinkListBox.ItemsSource = new List<string> { "（打开 Vault 内的文档后显示反链）" };
            return;
        }

        await _memoryPanel.RefreshBacklinksAsync(_currentVaultPath, currentPath);
        if (_memoryPanel.Backlinks.Count == 0)
        {
            _backlinkListBox.ItemsSource = File.Exists(currentPath)
                ? new List<string> { "（暂无反向链接）" }
                : new List<string> { "（打开 Vault 内的文档后显示反链）" };
        }
        else
        {
            RenderBacklinks();
        }
    }

    private void BacklinkListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_backlinkListBox.SelectedItem is not string name)
        {
            return;
        }

        var match = _memoryPanel.Backlinks.FirstOrDefault(x => x.FileName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            OpenFileInNewTab(match.FilePath);
        }

        _backlinkListBox.SelectedItem = null;
    }

    private async void FollowWikiLinkAtCaret()
    {
        var editor = GetCurrentTextEditor();
        if (editor == null)
        {
            return;
        }

        var offset = Math.Clamp(editor.CaretOffset, 0, editor.Text.Length);
        var link = WikiLinkResolver.Parse(editor.Text)
            .FirstOrDefault(l => offset >= l.Offset && offset <= l.Offset + l.Length);
        if (link == null)
        {
            _statusTextBlock.Text = "光标不在 [[双链]] 上";
            return;
        }

        // 默认 Obsidian 行为：目标不存在则自动新建。
        if (!string.IsNullOrEmpty(_currentVaultPath))
        {
            var resolved = WikiLinkResolver.ResolveToFile(_currentVaultPath, link.Target);
            if (resolved != null)
            {
                OpenFileInNewTab(resolved);
                return;
            }

            var newPath = WikiLinkResolver.DefaultNewFilePath(_currentVaultPath, link.Target);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                if (!File.Exists(newPath))
                {
                    await File.WriteAllTextAsync(newPath, $"# {link.Target}\n\n");
                }

                OpenFileInNewTab(newPath);
                _statusTextBlock.Text = $"已新建并打开: {Path.GetFileName(newPath)}";
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("双链", $"无法创建目标文件: {ex.Message}");
            }

            return;
        }

        await ShowInfoDialogAsync("双链", "请先打开一个知识库（Vault）再使用双链跳转。");
    }
}

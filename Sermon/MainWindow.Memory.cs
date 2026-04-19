using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sermon;

public partial class MainWindow
{
    private readonly MemoryStore _memoryStore = new();
    private readonly MemoryEngine _memoryEngine = new();
    private readonly List<MemoryEntry> _memoryEntries = new();
    private bool _memoryLoaded;
    private TextBox _memorySearchTextBox = null!;
    private TextBox _memoryIntentTextBox = null!;
    private ListBox _memoryListBox = null!;
    private TextBlock _memoryStatsTextBlock = null!;

    private void InitializeMemorySystem()
    {
        _memorySearchTextBox = RequiredControl<TextBox>("MemorySearchTextBox");
        _memoryIntentTextBox = RequiredControl<TextBox>("MemoryIntentTextBox");
        _memoryListBox = RequiredControl<ListBox>("MemoryListBox");
        _memoryStatsTextBlock = RequiredControl<TextBlock>("MemoryStatsTextBlock");
        _ = LoadMemoryStoreAsync();
    }

    private async Task LoadMemoryStoreAsync()
    {
        try
        {
            var loaded = await _memoryStore.LoadAsync();
            _memoryEntries.Clear();
            _memoryEntries.AddRange(loaded.OrderByDescending(x => x.UpdatedAt));
            _memoryLoaded = true;
            RefreshMemoryList(_memorySearchTextBox.Text);
            UpdateMemoryStats();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = "记忆库加载失败";
            await ShowInfoDialogAsync("记忆库", $"加载失败: {ex.Message}");
        }
    }

    private async Task SaveMemoryStoreAsync()
    {
        await _memoryStore.SaveAsync(_memoryEntries);
    }

    private void RefreshMemoryList(string? query)
    {
        if (!_memoryLoaded)
        {
            return;
        }

        var result = _memoryEngine.Search(_memoryEntries, query ?? string.Empty, limit: 40);
        _memoryListBox.ItemsSource = result
            .Select(x =>
            {
                var tags = x.Tags.Count == 0 ? "-" : string.Join(", ", x.Tags);
                return $"[{x.DisplaySource}] {x.Title}\n{x.Summary}\n标签: {tags}";
            })
            .ToList();
    }

    private void UpdateMemoryStats()
    {
        var count = _memoryEntries.Count;
        var topTag = _memoryEntries
            .SelectMany(x => x.Tags)
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "-";
        _memoryStatsTextBlock.Text = $"记忆: {count} 条  热门标签: {topTag}";
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
        var extracted = _memoryEngine.ExtractFromMarkdown(content, sourcePath);
        var mergedCount = MergeMemories(extracted);
        await SaveMemoryStoreAsync();
        RefreshMemoryList(_memorySearchTextBox.Text);
        UpdateMemoryStats();
        _statusTextBlock.Text = $"记忆提取完成，新增/更新 {mergedCount} 条";
    }

    private async void GenerateContextPackButton_Click(object? sender, RoutedEventArgs e)
    {
        var intent = _memoryIntentTextBox.Text?.Trim() ?? string.Empty;
        var pack = _memoryEngine.BuildContextPack(_memoryEntries, intent, limit: 10);
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

        var match = _memoryEntries.FirstOrDefault(x =>
            selectedText.Contains(x.Title, StringComparison.Ordinal) &&
            selectedText.Contains(x.Summary, StringComparison.Ordinal));
        if (match == null)
        {
            await ShowInfoDialogAsync("AI记忆", "未找到要删除的记忆条目。");
            return;
        }

        _memoryEntries.Remove(match);
        await SaveMemoryStoreAsync();
        RefreshMemoryList(_memorySearchTextBox.Text);
        UpdateMemoryStats();
        _statusTextBlock.Text = "已删除选中的记忆";
    }

    private void MemorySearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshMemoryList(_memorySearchTextBox.Text);
    }

    private void SyncMemoryPanelWithCurrentTab()
    {
        RefreshMemoryList(_memorySearchTextBox.Text);
        UpdateMemoryStats();
    }

    private int MergeMemories(IReadOnlyList<MemoryEntry> incoming)
    {
        var changed = 0;
        foreach (var item in incoming)
        {
            var existing = _memoryEntries.FirstOrDefault(x =>
                x.Title.Equals(item.Title, StringComparison.OrdinalIgnoreCase) &&
                x.SourceFilePath.Equals(item.SourceFilePath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                _memoryEntries.Add(item);
                changed++;
                continue;
            }

            if (!existing.Summary.Equals(item.Summary, StringComparison.Ordinal))
            {
                existing.Summary = item.Summary;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.Importance = Math.Max(existing.Importance, item.Importance);
                existing.Tags = existing.Tags
                    .Concat(item.Tags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
                changed++;
            }
        }

        return changed;
    }
}

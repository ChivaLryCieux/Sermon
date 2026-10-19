using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sermon.Services;

namespace Sermon.ViewModels;

/// <summary>记忆面板 ViewModel：接管 MainWindow.Memory.cs 的全部状态。</summary>
public sealed partial class MemoryPanelViewModel : ViewModelBase
{
    private readonly MemoryService _service;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _intent = string.Empty;

    [ObservableProperty]
    private string _statsText = "记忆: 0 条  热门标签: -";

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<MemoryEntry> Results { get; } = new();

    public ObservableCollection<BacklinkItem> Backlinks { get; } = new();

    public MemoryPanelViewModel(MemoryService? service = null)
    {
        _service = service ?? new MemoryService();
    }

    public IReadOnlyList<MemoryEntry> CurrentResults { get; private set; } = new List<MemoryEntry>();

    public async Task LoadAsync()
    {
        await _service.LoadAsync();
        RefreshResults();
        RefreshStats();
    }

    public void RefreshResults()
    {
        CurrentResults = _service.Search(SearchQuery, limit: 40);
        Results.Clear();
        foreach (var entry in CurrentResults)
        {
            Results.Add(entry);
        }
    }

    public void RefreshStats()
    {
        var (count, topTag) = _service.Stats();
        StatsText = $"记忆: {count} 条  热门标签: {topTag}";
    }

    public async Task<int> ExtractFromAsync(string markdown, string sourcePath)
    {
        var changed = _service.ImportFromMarkdown(markdown, sourcePath);
        if (changed > 0)
        {
            await _service.SaveAsync();
            RefreshResults();
            RefreshStats();
        }

        return changed;
    }

    public string BuildContextPack()
    {
        return _service.BuildContextPack(Intent);
    }

    public async Task<bool> DeleteAsync(MemoryEntry? entry)
    {
        if (entry == null || !_service.Delete(entry))
        {
            return false;
        }

        await _service.SaveAsync();
        RefreshResults();
        RefreshStats();
        return true;
    }

    public async Task RefreshBacklinksAsync(string? vaultPath, string? currentPath)
    {
        var links = await Task.Run(() => MemoryService.FindBacklinks(vaultPath, currentPath));
        Backlinks.Clear();
        foreach (var link in links)
        {
            Backlinks.Add(new BacklinkItem(link.FilePath, link.FileName));
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (_service.IsLoaded)
        {
            RefreshResults();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    public sealed record BacklinkItem(string FilePath, string FileName);
}

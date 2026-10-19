using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sermon.Services;

/// <summary>记忆面板服务：抽取/合并/搜索/反链，可单测。</summary>
public sealed class MemoryService
{
    private readonly MemoryStore _store;
    private readonly MemoryEngine _engine = new();
    private readonly List<MemoryEntry> _entries = new();
    private bool _loaded;

    public MemoryService(MemoryStore? store = null)
    {
        _store = store ?? new MemoryStore();
    }

    public IReadOnlyList<MemoryEntry> Entries => _entries.AsReadOnly();

    public bool IsLoaded => _loaded;

    public async Task LoadAsync()
    {
        var loaded = await _store.LoadAsync();
        _entries.Clear();
        _entries.AddRange(loaded.OrderByDescending(x => x.UpdatedAt));
        _loaded = true;
    }

    public Task SaveAsync()
    {
        return _store.SaveAsync(_entries);
    }

    public IReadOnlyList<MemoryEntry> Search(string query, int limit = 40)
    {
        return _engine.Search(_entries, query ?? string.Empty, limit);
    }

    public int ImportFromMarkdown(string markdown, string sourcePath)
    {
        var incoming = _engine.ExtractFromMarkdown(markdown, sourcePath);
        return Merge(incoming);
    }

    public string BuildContextPack(string intent, int limit = 8)
    {
        return _engine.BuildContextPack(_entries, intent ?? string.Empty, limit);
    }

    public bool Delete(MemoryEntry entry)
    {
        return entry != null && _entries.Remove(entry);
    }

    public (int Count, string TopTag) Stats()
    {
        var topTag = _entries
            .SelectMany(x => x.Tags)
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "-";
        return (_entries.Count, topTag);
    }

    public static List<(string FilePath, string FileName)> FindBacklinks(string? vaultPath, string? targetPath, int maxResults = 50)
    {
        if (string.IsNullOrEmpty(vaultPath) || string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        {
            return new List<(string, string)>();
        }

        return WikiLinkResolver.FindBacklinks(vaultPath, targetPath, maxResults);
    }

    public static string DisplayText(MemoryEntry entry)
    {
        var tags = entry.Tags.Count == 0 ? "-" : string.Join(", ", entry.Tags);
        return $"[{entry.DisplaySource}] {entry.Title}\n{entry.Summary}\n标签: {tags}";
    }

    private int Merge(IReadOnlyList<MemoryEntry> incoming)
    {
        var changed = 0;
        foreach (var item in incoming)
        {
            var existing = _entries.FirstOrDefault(x =>
                x.Title.Equals(item.Title, StringComparison.OrdinalIgnoreCase) &&
                x.SourceFilePath.Equals(item.SourceFilePath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                _entries.Add(item);
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

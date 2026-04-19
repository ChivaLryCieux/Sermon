using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sermon;

public sealed class MemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storeFilePath;

    public MemoryStore()
    {
        var appName = "Sermon";
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName);
        Directory.CreateDirectory(baseDirectory);
        _storeFilePath = Path.Combine(baseDirectory, "memory-store.json");
    }

    public async Task<IReadOnlyList<MemoryEntry>> LoadAsync()
    {
        if (!File.Exists(_storeFilePath))
        {
            return Array.Empty<MemoryEntry>();
        }

        await using var stream = File.OpenRead(_storeFilePath);
        var entries = await JsonSerializer.DeserializeAsync<List<MemoryEntry>>(stream, JsonOptions);
        return entries is null ? Array.Empty<MemoryEntry>() : entries;
    }

    public async Task SaveAsync(IReadOnlyList<MemoryEntry> entries)
    {
        await using var stream = File.Create(_storeFilePath);
        await JsonSerializer.SerializeAsync(stream, entries, JsonOptions);
    }
}

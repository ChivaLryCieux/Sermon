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
    private readonly object _saveLock = new();
    private Task _saveChain = Task.CompletedTask;

    public MemoryStore()
    {
        var appName = "Sermon";
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName);
        Directory.CreateDirectory(baseDirectory);
        _storeFilePath = Path.Combine(baseDirectory, "memory-store.json");
    }

    internal MemoryStore(string storeFilePath)
    {
        _storeFilePath = storeFilePath;
    }

    public async Task<IReadOnlyList<MemoryEntry>> LoadAsync()
    {
        if (!File.Exists(_storeFilePath))
        {
            return Array.Empty<MemoryEntry>();
        }

        try
        {
            await using var stream = File.OpenRead(_storeFilePath);
            var entries = await JsonSerializer.DeserializeAsync<List<MemoryEntry>>(stream, JsonOptions);
            return entries is null ? Array.Empty<MemoryEntry>() : entries;
        }
        catch (JsonException)
        {
            // 损坏的 JSON 不应直接吞掉用户记忆：备份后返回空库，保证应用仍可启动。
            BackupCorruptedStore();
            return Array.Empty<MemoryEntry>();
        }
    }

    public Task SaveAsync(IReadOnlyList<MemoryEntry> entries)
    {
        var snapshot = entries is null
            ? new List<MemoryEntry>()
            : new List<MemoryEntry>(entries);

        lock (_saveLock)
        {
            // 串行化并发保存，避免两个 SaveAsync 交错写坏同一个 JSON。
            _saveChain = _saveChain.ContinueWith(
                _ => PersistSnapshotAsync(snapshot),
                TaskScheduler.Default).Unwrap();
            return _saveChain;
        }
    }

    private async Task PersistSnapshotAsync(List<MemoryEntry> snapshot)
    {
        var directory = Path.GetDirectoryName(_storeFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _storeFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);
        }

        // 原子替换 + 失败回退，避免崩溃/断电留下半截 JSON。
        try
        {
            File.Move(tempPath, _storeFilePath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(_storeFilePath))
                {
                    File.Delete(_storeFilePath);
                }

                File.Move(tempPath, _storeFilePath);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                throw;
            }
        }
    }

    private void BackupCorruptedStore()
    {
        try
        {
            var backupPath = _storeFilePath + $".corrupted-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(_storeFilePath, backupPath, overwrite: false);
        }
        catch
        {
            // 备份失败也不应阻断启动；用户至少还能得到一个空库。
        }
    }
}

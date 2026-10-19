using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sermon.Services;

/// <summary>Vault 文件树服务：后台快照 + watcher 事件，可单测。</summary>
public sealed class VaultService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public event Action? VaultChanged;

    public Task<List<VaultFileEntry>> SnapshotAsync(string rootPath, int maxFiles = 4000)
    {
        return Task.Run(() => Snapshot(rootPath, maxFiles));
    }

    public static List<VaultFileEntry> Snapshot(string rootPath, int maxFiles = 4000)
    {
        var result = new List<VaultFileEntry>();
        try
        {
            var stack = new Stack<string>();
            stack.Push(rootPath);
            while (stack.Count > 0 && result.Count < maxFiles)
            {
                var dir = stack.Pop();
                string[] subDirs = Array.Empty<string>();
                string[] files = Array.Empty<string>();
                try
                {
                    subDirs = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir).Where(DocumentService.IsSupportedTextFile).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var file in files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    if (result.Count >= maxFiles)
                    {
                        break;
                    }

                    result.Add(new VaultFileEntry(Path.GetFileName(file), file));
                }

                foreach (var sub in subDirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).Reverse())
                {
                    var name = Path.GetFileName(sub);
                    if (name.StartsWith(".", StringComparison.Ordinal) ||
                        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(".git", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    stack.Push(sub);
                }
            }
        }
        catch
        {
        }

        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Watch(string vaultPath)
    {
        StopWatching();
        try
        {
            _watcher = new FileSystemWatcher(vaultPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };
            _watcher.Created += (_, _) => VaultChanged?.Invoke();
            _watcher.Deleted += (_, _) => VaultChanged?.Invoke();
            _watcher.Renamed += (_, _) => VaultChanged?.Invoke();
            _watcher.Changed += (_, e) =>
            {
                if (e.FullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    VaultChanged?.Invoke();
                }
            };
        }
        catch
        {
            StopWatching();
        }
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatching();
    }

    public sealed record VaultFileEntry(string Name, string FullPath);
}

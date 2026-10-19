using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sermon;

/// <summary>Vault（Obsidian 式知识库）：记住最近打开的库目录，启动自动恢复。</summary>
public sealed class VaultManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const int MaxRecent = 8;

    private readonly string _stateFilePath;
    private List<string> _recent = new();

    public string? CurrentVaultPath { get; private set; }

    public IReadOnlyList<string> RecentVaults => _recent.AsReadOnly();

    public VaultManager()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sermon");
        Directory.CreateDirectory(baseDir);
        _stateFilePath = Path.Combine(baseDir, "vault.json");
    }

    internal VaultManager(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            await using var stream = File.OpenRead(_stateFilePath);
            var state = await JsonSerializer.DeserializeAsync<VaultState>(stream, JsonOptions);
            if (state is null)
            {
                return;
            }

            _recent = (state.Recent ?? new List<string>())
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecent)
                .ToList();
            CurrentVaultPath = _recent.Contains(state.Current ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ? state.Current
                : _recent.FirstOrDefault();
        }
        catch
        {
            _recent = new List<string>();
            CurrentVaultPath = null;
        }
    }

    public async Task SetCurrentAsync(string? vaultPath)
    {
        if (!string.IsNullOrEmpty(vaultPath) && Directory.Exists(vaultPath))
        {
            CurrentVaultPath = vaultPath;
            _recent.RemoveAll(p => p.Equals(vaultPath, StringComparison.OrdinalIgnoreCase));
            _recent.Insert(0, vaultPath);
            _recent = _recent.Take(MaxRecent).ToList();
        }
        else if (string.IsNullOrEmpty(vaultPath))
        {
            CurrentVaultPath = null;
        }

        await PersistAsync();
    }

    public async Task RemoveAsync(string vaultPath)
    {
        _recent.RemoveAll(p => p.Equals(vaultPath, StringComparison.OrdinalIgnoreCase));
        if (CurrentVaultPath?.Equals(vaultPath, StringComparison.OrdinalIgnoreCase) == true)
        {
            CurrentVaultPath = _recent.FirstOrDefault();
        }

        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var stream = File.Create(_stateFilePath);
            await JsonSerializer.SerializeAsync(stream, new VaultState
            {
                Current = CurrentVaultPath,
                Recent = _recent
            }, JsonOptions);
        }
        catch
        {
            // Vault 记录写失败不应打断主流程。
        }
    }

    private sealed class VaultState
    {
        public string? Current { get; set; }
        public List<string>? Recent { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sermon.Services;

namespace Sermon.ViewModels;

/// <summary>Vault 面板 ViewModel：接管文件树快照 + 最近库状态。</summary>
public sealed partial class VaultPanelViewModel : ViewModelBase, IDisposable
{
    private readonly VaultManager _vaults;
    private readonly VaultService _files;
    private bool _disposed;

    [ObservableProperty]
    private string? _currentVaultPath;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "准备就绪";

    public ObservableCollection<VaultEntry> Files { get; } = new();

    public ObservableCollection<VaultEntry> RecentVaults { get; } = new();

    public event Action? VaultFilesChanged;

    public VaultPanelViewModel(VaultManager? vaults = null, VaultService? files = null)
    {
        _vaults = vaults ?? new VaultManager();
        _files = files ?? new VaultService();
        _files.VaultChanged += () => VaultFilesChanged?.Invoke();
    }

    public async Task RestoreAsync()
    {
        await _vaults.LoadAsync();
        CurrentVaultPath = _vaults.CurrentVaultPath;
        RefreshRecent();
        if (!string.IsNullOrEmpty(CurrentVaultPath))
        {
            await OpenAsync(CurrentVaultPath);
        }
    }

    public async Task OpenAsync(string vaultPath)
    {
        if (!Directory.Exists(vaultPath))
        {
            await _vaults.RemoveAsync(vaultPath);
            RefreshRecent();
            return;
        }

        CurrentVaultPath = vaultPath;
        await _vaults.SetCurrentAsync(vaultPath);
        RefreshRecent();
        _files.Watch(vaultPath);
        await ReloadFilesAsync();
        StatusText = $"已打开知识库: {Path.GetFileName(vaultPath)}";
    }

    public async Task ReloadFilesAsync()
    {
        if (string.IsNullOrEmpty(CurrentVaultPath))
        {
            return;
        }

        IsLoading = true;
        try
        {
            var snapshot = await _files.SnapshotAsync(CurrentVaultPath);
            Files.Clear();
            foreach (var entry in snapshot)
            {
                Files.Add(new VaultEntry(entry.Name, entry.FullPath));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshRecent()
    {
        RecentVaults.Clear();
        foreach (var path in _vaults.RecentVaults)
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            RecentVaults.Add(new VaultEntry(string.IsNullOrEmpty(name) ? path : name, path));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _files.Dispose();
    }

    public sealed record VaultEntry(string Name, string FullPath);
}

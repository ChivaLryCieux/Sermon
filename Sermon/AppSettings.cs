using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sermon;

/// <summary>应用设置持久化：主题/字号/自动保存间隔。</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public string Theme { get; set; } = "light";
    public double EditorFontSize { get; set; } = 15;
    public int AutoSaveMinutes { get; set; } = 2;

    public AppSettings()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sermon");
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, "settings.json");
    }

    internal AppSettings(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public bool IsDark => Theme.Equals("dark", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            await using var stream = File.OpenRead(_settingsPath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
            if (loaded == null)
            {
                return;
            }

            Theme = loaded.Theme == "dark" ? "dark" : "light";
            EditorFontSize = Math.Clamp(loaded.EditorFontSize, 11, 26);
            AutoSaveMinutes = Math.Clamp(loaded.AutoSaveMinutes, 1, 30);
        }
        catch
        {
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var stream = File.Create(_settingsPath);
            await JsonSerializer.SerializeAsync(stream, this, JsonOptions);
        }
        catch
        {
        }
    }
}

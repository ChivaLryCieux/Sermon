using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sermon;

/// <summary>Obsidian 式全局搜索：文件名优先，全文扫 vault 内 md，返回片段。</summary>
public static class VaultSearchEngine
{
    public sealed record SearchHit(string FilePath, string FileName, string Snippet, bool FileNameHit);

    public static async Task<List<SearchHit>> SearchAsync(
        string vaultPath,
        string query,
        int maxResults = 60,
        CancellationToken cancellationToken = default)
    {
        var result = new List<SearchHit>();
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(vaultPath) || !Directory.Exists(vaultPath) || q.Length == 0)
        {
            return result;
        }

        string[] files;
        try
        {
            files = await Task.Run(
                () => Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return result;
        }
        catch
        {
            return result;
        }

        // 文件名命中优先（Obsidian Quick Switcher 行为）。
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new SearchHit(file, Path.GetFileName(file), $"文件名匹配：{name}", true));
                if (result.Count >= maxResults)
                {
                    return result;
                }
            }
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Count >= maxResults)
            {
                break;
            }

            if (result.Any(h => h.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return result;
            }
            catch
            {
                continue;
            }

            var snippet = ExtractSnippet(content, q);
            if (snippet != null)
            {
                result.Add(new SearchHit(file, Path.GetFileName(file), snippet, false));
            }
        }

        return result;
    }

    public static List<(int Line, string Title, int Level)> ExtractOutline(string markdown, int maxItems = 200)
    {
        var outline = new List<(int, string, int)>();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length && outline.Count < maxItems; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.Length == 0 || !line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var level = 0;
            while (level < line.Length && line[level] == '#')
            {
                level++;
            }

            if (level is < 1 or > 6 || level >= line.Length || line[level] != ' ')
            {
                continue;
            }

            var title = InlineParser.Strip(line[(level + 1)..]).Trim();
            if (title.Length > 0)
            {
                outline.Add((i + 1, title, level));
            }
        }

        return outline;
    }

    private static string? ExtractSnippet(string content, string query)
    {
        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = Math.Max(0, index - 40);
        var length = Math.Min(content.Length - start, query.Length + 80);
        var snippet = content.Substring(start, length)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return (start > 0 ? "…" : string.Empty) + snippet + (start + length < content.Length ? "…" : string.Empty);
    }
}

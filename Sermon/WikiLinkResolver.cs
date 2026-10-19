using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sermon;

/// <summary>Obsidian 式双链：[[页面]] 解析、目标定位、反向链接扫描。</summary>
public static class WikiLinkResolver
{
    private static readonly Regex WikiLinkPattern = new(@"\[\[([^\[\]]+)\]\]", RegexOptions.Compiled);

    public sealed record WikiLink(string Target, string? Alias, int Offset, int Length);

    public static List<WikiLink> Parse(string markdown)
    {
        var links = new List<WikiLink>();
        var text = markdown ?? string.Empty;
        foreach (Match m in WikiLinkPattern.Matches(text))
        {
            var raw = m.Groups[1].Value.Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            string target;
            string? alias = null;
            var pipe = raw.IndexOf('|');
            if (pipe >= 0)
            {
                target = raw[..pipe].Trim();
                alias = raw[(pipe + 1)..].Trim();
            }
            else
            {
                target = raw;
            }

            if (target.Length == 0)
            {
                continue;
            }

            links.Add(new WikiLink(target, string.IsNullOrEmpty(alias) ? null : alias, m.Index, m.Length));
        }

        return links;
    }

    public static string? ResolveToFile(string vaultPath, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrEmpty(vaultPath) || !Directory.Exists(vaultPath))
        {
            return null;
        }

        var cleaned = target.Trim().Replace('/', Path.DirectorySeparatorChar);
        // 去掉 #锚点（Obsidian 支持 [[页面#标题]]，这里先定位到文件）。
        var hash = cleaned.IndexOf('#');
        if (hash >= 0)
        {
            cleaned = cleaned[..hash].Trim();
        }

        if (cleaned.Length == 0 || cleaned.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        var candidates = new List<string>
        {
            cleaned.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? cleaned : cleaned + ".md",
            Path.Combine(cleaned, cleaned + ".md")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(vaultPath, candidate));
            if (full.StartsWith(Path.GetFullPath(vaultPath), StringComparison.OrdinalIgnoreCase)
                && File.Exists(full))
            {
                return full;
            }
        }

        // 兜底：按文件名模糊找（ vault 内重名时取最短路径，即最接近根目录的）。
        try
        {
            var wanted = Path.GetFileNameWithoutExtension(cleaned);
            return Directory.EnumerateFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).Equals(wanted, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static string DefaultNewFilePath(string vaultPath, string target)
    {
        var cleaned = target.Trim();
        var hash = cleaned.IndexOf('#');
        if (hash >= 0)
        {
            cleaned = cleaned[..hash].Trim();
        }

        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(ch, '_');
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "未命名";
        }

        if (!cleaned.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            cleaned += ".md";
        }

        return Path.Combine(vaultPath, cleaned);
    }

    /// <summary>扫描 vault 内引用指定文件名的文档（反向链接）。</summary>
    public static List<(string FilePath, string FileName)> FindBacklinks(string vaultPath, string targetFilePath, int maxResults = 50)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrEmpty(vaultPath) || !Directory.Exists(vaultPath)
            || string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath))
        {
            return result;
        }

        var wanted = Path.GetFileNameWithoutExtension(targetFilePath);
        string[] files;
        try
        {
            files = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories);
        }
        catch
        {
            return result;
        }

        foreach (var file in files)
        {
            if (file.Equals(targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var hit = Parse(content).Any(l =>
                l.Target.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                l.Target.Equals(Path.GetFileName(targetFilePath), StringComparison.OrdinalIgnoreCase));
            if (hit)
            {
                result.Add((file, Path.GetFileName(file)));
                if (result.Count >= maxResults)
                {
                    break;
                }
            }
        }

        return result;
    }
}

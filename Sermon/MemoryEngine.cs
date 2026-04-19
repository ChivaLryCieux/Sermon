using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sermon;

public sealed class MemoryEngine
{
    public IReadOnlyList<MemoryEntry> ExtractFromMarkdown(string markdown, string sourceFilePath)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var memories = new List<MemoryEntry>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 12)
            {
                continue;
            }

            var heading = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
            if (heading.Success)
            {
                var title = CleanMarkdownInline(heading.Groups[2].Value).Trim();
                if (title.Length > 0)
                {
                    memories.Add(CreateMemory(
                        title,
                        $"文档主题: {title}",
                        sourceFilePath,
                        importance: 4,
                        GuessTags(title)));
                }
                continue;
            }

            var list = Regex.Match(trimmed, @"^(-|\*|\d+\.)\s+(.+)$");
            if (list.Success)
            {
                var text = CleanMarkdownInline(list.Groups[2].Value).Trim();
                if (text.Length >= 12)
                {
                    memories.Add(CreateMemory(
                        ShrinkTitle(text),
                        text,
                        sourceFilePath,
                        importance: 3,
                        GuessTags(text)));
                }
                continue;
            }

            if (trimmed.Length >= 24 && !trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var cleaned = CleanMarkdownInline(trimmed);
                memories.Add(CreateMemory(
                    ShrinkTitle(cleaned),
                    cleaned,
                    sourceFilePath,
                    importance: 2,
                    GuessTags(cleaned)));
            }
        }

        return MergeDuplicates(memories);
    }

    public IReadOnlyList<MemoryEntry> Search(IReadOnlyList<MemoryEntry> entries, string query, int limit = 20)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return entries
                .OrderByDescending(x => x.Importance)
                .ThenByDescending(x => x.UpdatedAt)
                .Take(limit)
                .ToList();
        }

        var tokens = Tokenize(q);
        return entries
            .Select(entry => new { Entry = entry, Score = Score(entry, tokens) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.Importance)
            .ThenByDescending(x => x.Entry.UpdatedAt)
            .Take(limit)
            .Select(x => x.Entry)
            .ToList();
    }

    public string BuildContextPack(IReadOnlyList<MemoryEntry> entries, string userIntent, int limit = 8)
    {
        var matched = Search(entries, userIntent, limit);
        var sb = new StringBuilder();
        sb.AppendLine("# AI 记忆上下文包");
        sb.AppendLine();
        sb.AppendLine("## 用户意图");
        sb.AppendLine(string.IsNullOrWhiteSpace(userIntent) ? "（未提供）" : userIntent.Trim());
        sb.AppendLine();
        sb.AppendLine("## 可用记忆");

        if (matched.Count == 0)
        {
            sb.AppendLine("- 暂无匹配记忆。");
            return sb.ToString();
        }

        foreach (var entry in matched)
        {
            sb.Append("- [");
            sb.Append(entry.DisplaySource);
            sb.Append("] ");
            sb.Append(entry.Title);
            sb.Append(" | ");
            sb.Append(entry.Summary);
            if (entry.Tags.Count > 0)
            {
                sb.Append(" | 标签: ");
                sb.Append(string.Join(", ", entry.Tags));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int Score(MemoryEntry entry, IReadOnlyList<string> tokens)
    {
        var text = $"{entry.Title} {entry.Summary} {string.Join(' ', entry.Tags)}".ToLowerInvariant();
        var score = 0;
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.Ordinal))
            {
                score += 3;
            }

            if (entry.SourceFilePath.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        score += Math.Max(1, entry.Importance);
        return score;
    }

    private static MemoryEntry CreateMemory(
        string title,
        string summary,
        string sourceFilePath,
        int importance,
        IReadOnlyList<string> tags)
    {
        var now = DateTimeOffset.UtcNow;
        return new MemoryEntry
        {
            Title = title,
            Summary = summary,
            SourceFilePath = sourceFilePath ?? string.Empty,
            Importance = Math.Clamp(importance, 1, 5),
            Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<MemoryEntry> MergeDuplicates(IReadOnlyList<MemoryEntry> items)
    {
        return items
            .GroupBy(x => $"{x.Title}|{x.Summary}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.Importance)
                .ThenByDescending(x => x.UpdatedAt)
                .First())
            .ToList();
    }

    private static List<string> GuessTags(string text)
    {
        var tags = new List<string>();
        var lower = text.ToLowerInvariant();
        var dictionary = new Dictionary<string, string>
        {
            ["todo"] = "任务",
            ["计划"] = "计划",
            ["设计"] = "设计",
            ["bug"] = "缺陷",
            ["问题"] = "问题",
            ["api"] = "接口",
            ["架构"] = "架构",
            ["优化"] = "优化",
            ["性能"] = "性能",
            ["测试"] = "测试",
            ["部署"] = "部署",
            ["ai"] = "AI"
        };

        foreach (var kv in dictionary)
        {
            if (lower.Contains(kv.Key, StringComparison.Ordinal))
            {
                tags.Add(kv.Value);
            }
        }

        return tags.Count == 0 ? new List<string> { "通用" } : tags;
    }

    private static string ShrinkTitle(string text)
    {
        var clean = CleanMarkdownInline(text).Trim();
        return clean.Length <= 28 ? clean : clean[..28] + "...";
    }

    private static string CleanMarkdownInline(string text)
    {
        var result = Regex.Replace(text, @"!\[(.*?)\]\((.*?)\)", "$1");
        result = Regex.Replace(result, @"\[(.*?)\]\((.*?)\)", "$1");
        result = Regex.Replace(result, @"(`|\*\*|__|\*|_|~~)", string.Empty);
        return result.Trim();
    }

    private static List<string> Tokenize(string text)
    {
        return Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(token => token.Length >= 2)
            .Distinct()
            .ToList();
    }
}

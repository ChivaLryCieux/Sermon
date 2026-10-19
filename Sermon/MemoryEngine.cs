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
                .Take(Math.Clamp(limit, 1, 100))
                .ToList();
        }

        var tokens = Tokenize(q);
        if (tokens.Count == 0)
        {
            return Array.Empty<MemoryEntry>();
        }

        return entries
            .Select(entry => new { Entry = entry, Score = Score(entry, tokens) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.Importance)
            .ThenByDescending(x => x.Entry.UpdatedAt)
            .Take(Math.Clamp(limit, 1, 100))
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
        var match = 0;
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.Ordinal))
            {
                match += 3;
            }

            if (entry.SourceFilePath.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                match += 1;
            }
        }

        // 无任何关键词命中时返回 0，让调用方可以过滤掉不相关条目；
        // Importance 只做排序微调，避免“恒为正”导致搜索失效。
        if (match == 0)
        {
            return 0;
        }

        return match * 10 + Math.Clamp(entry.Importance, 1, 5);
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
        var lowered = text.ToLowerInvariant();
        // 用空白符与标点切分（保留 CJK 连续文本），避免 \W 在不同正则引擎下对中文处理不一致。
        var raw = Regex.Split(lowered, @"[\s\p{P}\p{S}]+")
            .Where(token => token.Length > 0)
            .Distinct()
            .ToList();

        var tokens = new List<string>(raw.Count + 1);
        foreach (var token in raw)
        {
            // 中文单字也有检索意义（如“优”），英文仍要求至少 2 字符以减少噪声。
            if (token.Length >= 2 || ContainsCjk(token))
            {
                tokens.Add(token);
            }
        }

        // 保留完整查询做子串匹配，对中文短语（“性能优化”）更友好。
        var compact = Regex.Replace(lowered, @"[\s\p{P}\p{S}]+", string.Empty);
        if (compact.Length >= 2 && !tokens.Contains(compact, StringComparer.Ordinal))
        {
            tokens.Add(compact);
        }

        return tokens.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool ContainsCjk(string token)
    {
        foreach (var ch in token)
        {
            if ((ch >= '\u3400' && ch <= '\u4DBF') ||
                (ch >= '\u4E00' && ch <= '\u9FFF') ||
                (ch >= '\uF900' && ch <= '\uFAFF') ||
                (ch >= '\u3040' && ch <= '\u30FF') ||
                (ch >= '\uAC00' && ch <= '\uD7AF'))
            {
                return true;
            }
        }

        return false;
    }
}

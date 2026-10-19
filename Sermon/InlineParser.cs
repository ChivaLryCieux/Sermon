using System;
using System.Collections.Generic;
using System.Linq;

namespace Sermon;

/// <summary>轻量行内 Markdown 解析：**粗体** *斜体* `代码` ~~删除线~~ [文本](链接)。</summary>
public static class InlineParser
{
    public sealed record InlineToken(string Text, bool Bold, bool Italic, bool Code, bool Strikethrough, string? Link);

    public static List<InlineToken> Parse(string text)
    {
        var tokens = new List<InlineToken>();
        var current = new System.Text.StringBuilder();
        var bold = false;
        var italic = false;
        var code = false;
        var strike = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(new InlineToken(current.ToString(), bold, italic, code, strike, null));
                current.Clear();
            }
        }

        var span = (text ?? string.Empty).AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            // 行内代码优先级最高：`code`
            if (span[i] == '`')
            {
                Flush();
                code = !code;
                i++;
                continue;
            }

            if (!code && i + 1 < span.Length && span[i] == '*' && span[i + 1] == '*')
            {
                Flush();
                bold = !bold;
                i += 2;
                continue;
            }

            if (!code && i + 1 < span.Length && span[i] == '_' && span[i + 1] == '_')
            {
                Flush();
                bold = !bold;
                i += 2;
                continue;
            }

            if (!code && i + 1 < span.Length && span[i] == '~' && span[i + 1] == '~')
            {
                Flush();
                strike = !strike;
                i += 2;
                continue;
            }

            if (!code && (span[i] == '*' || span[i] == '_'))
            {
                Flush();
                italic = !italic;
                i++;
                continue;
            }

            // [文本](链接)：只保留文本，丢掉 URL（预览不做可点击跳转，保持轻量）。
            if (!code && span[i] == '[')
            {
                var close = span.Slice(i).IndexOf(']');
                if (close > 0 && i + close + 1 < span.Length && span[i + close + 1] == '(')
                {
                    var end = span.Slice(i + close + 1).IndexOf(')');
                    if (end > 0)
                    {
                        Flush();
                        var label = span.Slice(i + 1, close - 1).ToString();
                        tokens.Add(new InlineToken(label, bold, italic, code, strike, null));
                        i += close + 1 + end + 1;
                        continue;
                    }
                }
            }

            current.Append(span[i]);
            i++;
        }

        Flush();
        return tokens.Where(t => t.Text.Length > 0).ToList();
    }

    public static string Strip(string text)
    {
        return string.Concat(Parse(text).Select(t => t.Text));
    }
}

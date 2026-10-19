using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Sermon;

/// <summary>Obsidian 风格预览：将行内 token 转为 Avalonia Inlines，表格/任务列表用原生控件呈现。</summary>
public static class MarkdownPreviewBuilder
{
    public static TextBlock RichText(string markdown, double fontSize = 15, FontWeight? weight = null)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap
        };
        if (weight.HasValue)
        {
            block.FontWeight = weight.Value;
        }

        foreach (var token in InlineParser.Parse(markdown))
        {
            Inline inline;
            if (token.Code)
            {
                inline = new Run(token.Text)
                {
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 238))
                };
            }
            else
            {
                inline = new Run(token.Text);
            }

            if (token.Bold)
            {
                var bold = new Bold();
                bold.Inlines.Add(inline);
                inline = bold;
            }
            if (token.Italic)
            {
                var italic = new Italic();
                italic.Inlines.Add(inline);
                inline = italic;
            }
            if (token.Strikethrough)
            {
                inline.TextDecorations = TextDecorations.Strikethrough;
            }
            block.Inlines.Add(inline);
        }

        if (block.Inlines.Count == 0)
        {
            block.Text = string.Empty;
        }

        return block;
    }
}

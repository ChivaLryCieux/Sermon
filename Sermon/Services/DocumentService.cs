using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaEdit.Document;

namespace Sermon.Services;

/// <summary>文档 IO 服务：从 code-behind 抽出的纯文件读写，可单测。</summary>
public sealed class DocumentService
{
    public static bool IsSupportedTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    public static string DefaultExtension(string? filePath)
    {
        var extension = Path.GetExtension(filePath ?? string.Empty);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ? "txt" : "md";
    }

    public async Task<string> ReadTextAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    public async Task WriteTextAsync(string filePath, string content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content ?? string.Empty);
        try
        {
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                File.Move(tempPath, filePath);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                throw;
            }
        }
    }

    public (int Line, int Column) GetCursorPosition(TextDocument? document, int caretOffset)
    {
        if (document == null)
        {
            return (1, 1);
        }

        try
        {
            var location = document.GetLocation(Math.Clamp(caretOffset, 0, document.TextLength));
            return (location.Line, location.Column);
        }
        catch
        {
            return (1, 1);
        }
    }

    public static (int Words, int Chars) CountWords(string text)
    {
        var source = text ?? string.Empty;
        var words = string.IsNullOrWhiteSpace(source)
            ? 0
            : source.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return (words, source.Length);
    }

    public static IEnumerable<string> SupportedFiles(IEnumerable<string> files)
    {
        return (files ?? Enumerable.Empty<string>()).Where(IsSupportedTextFile);
    }
}

using System;
using System.Collections.Generic;

namespace Sermon;

public sealed class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public int Importance { get; set; } = 3;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Tags { get; set; } = new();

    public string DisplaySource => string.IsNullOrWhiteSpace(SourceFilePath)
        ? "未知来源"
        : System.IO.Path.GetFileName(SourceFilePath);
}

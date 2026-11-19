using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Templar.Generators;

internal sealed class TemplateLocation
{
    public IReadOnlyList<string> FolderSegments { get; }
    public string LeafName { get; }

    private TemplateLocation(IReadOnlyList<string> folder, string leaf)
    {
        FolderSegments = folder;
        LeafName = leaf;
    }

    public static TemplateLocation? From(string absolutePath, string projectDir)
    {
        var full = Path.GetFullPath(absolutePath);
        var dir = Path.GetFullPath(projectDir);
        var dirWithSep = dir.EndsWith($"{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                      || dir.EndsWith($"{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            ? dir
            : dir + Path.DirectorySeparatorChar;
        if (!full.StartsWith(dirWithSep, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rel = full.Substring(dirWithSep.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var noExt = rel.Substring(0, rel.Length - 4);
        var parts = noExt.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var folder = parts.Take(parts.Length - 1).ToList();
        return new TemplateLocation(folder, parts[parts.Length - 1]);
    }
}

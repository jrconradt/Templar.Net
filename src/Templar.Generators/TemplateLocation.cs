using System;
using System.IO;

namespace Templar.Generators;

internal static class TemplateLocation
{
    public static string Root(string projectDir)
    {
        return Path.Combine(Path.GetFullPath(projectDir), "Templates") + Path.DirectorySeparatorChar;
    }

    public static string? NameOf(string absolutePath, string projectDir)
    {
        var full = Path.GetFullPath(absolutePath);
        var root = Root(projectDir);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rel = full.Substring(root.Length);
        var noExt = rel.Substring(0, rel.Length - 4);
        var parts = noExt.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        return string.Join("/", parts);
    }
}

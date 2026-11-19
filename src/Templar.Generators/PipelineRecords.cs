using Microsoft.CodeAnalysis;

namespace Templar.Generators;

internal sealed record TemplateFile(string Path, string Text);

internal sealed record ProjectSettings(string? RootNamespace, string? ProjectDir);

internal sealed record Site(string Path, int Start, int Length, int StartLine, int StartCharacter, int EndLine, int EndCharacter)
{
    public static Site Line(string path, int line)
    {
        return new Site(path, 0, 0, line - 1, 0, line - 1, 0);
    }

    public static Site Of(Location location)
    {
        var span = location.GetLineSpan();
        return new Site(span.Path,
                        location.SourceSpan.Start,
                        location.SourceSpan.Length,
                        span.StartLinePosition.Line,
                        span.StartLinePosition.Character,
                        span.EndLinePosition.Line,
                        span.EndLinePosition.Character);
    }
}

internal sealed record Finding(string Id, Site Site, EquatableArray<string> Arguments);

internal sealed record TemplateOutput(string HintName, string Source, EquatableArray<Finding> Findings);

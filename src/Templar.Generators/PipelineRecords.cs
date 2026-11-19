namespace Templar.Generators;

internal sealed record TemplateFile(string Path, string Text);

internal sealed record ProjectSettings(string? RootNamespace, string? ProjectDir);

internal sealed record Finding(string Id, string Path, int Line, EquatableArray<string> Arguments);

internal sealed record TemplateOutput(string HintName, string Source, EquatableArray<Finding> Findings);

namespace Templar.Presets;
using Templar.Rendering;

[Template("Presets/CSharpFile")]
public sealed class CSharpFile
{
    public string Namespace { get; init; } = "";

    public IEnumerable<string> Usings { get; init; } = Array.Empty<string>();

    public string Body { get; init; } = "";

    public string Pragmas { get; init; } = "";

    public string Header { get; init; } = "#nullable enable";

    public Sequence<Using> UsingsBlock => Sequence.Lines(Usings.Select(u => new Using { Name = u }));
}

namespace Templar.Presets;
using Templar.Rendering;

public class CSharpFile : Compositor
{
    public virtual string Namespace { get; init; } = "";

    [TemplateIgnore]
    public virtual IEnumerable<string> Usings { get; init; } = Array.Empty<string>();

    public virtual string Body { get; init; } = "";

    public virtual string Pragmas { get; init; } = "";

    public virtual string Header { get; init; } = "#nullable enable";

    public Sequence UsingsBlock => Sequence.Lines(Usings.Select(u => new Using { Name = u }));
}

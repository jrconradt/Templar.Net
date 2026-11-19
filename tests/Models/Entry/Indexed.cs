using Templar.Rendering;

namespace Templar.Tests.Models.Entry;

[Template("Models/Entry/Indexed")]
public sealed record Indexed
{
    public string this[int i] => "INDEXED";

    public string Real { get; init; } = "value";
}

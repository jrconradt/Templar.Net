using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

public sealed class Rows
{
    public required Sequence<Sequence<Item>> All { get; init; }
}

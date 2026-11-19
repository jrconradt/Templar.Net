using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

public sealed class Items
{
    public required Sequence<Item> All { get; init; }
}

using Templar.Rendering;

namespace Templar.Tests.Models.Engine;

public sealed class LitSequence
{
    public required Sequence<Lit> Items { get; init; }
}

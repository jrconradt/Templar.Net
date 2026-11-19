using Templar.Rendering;

namespace Templar.Tests.Models.Swallow;

public sealed class ListHost
{
    public required Sequence<Empty> Inner { get; init; }
}

using Templar.Rendering;

namespace Fixture.Generator.Orders;

public sealed class Order
{
    public required string Id { get; init; }

    public required Sequence<ILine> Lines { get; init; }

    public int Count { get; init; }

    public required Sequence<Tag> Tags { get; init; }
}

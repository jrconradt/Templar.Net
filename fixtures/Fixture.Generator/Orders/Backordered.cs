namespace Fixture.Generator.Orders;

public sealed class Backordered : ILine
{
    public required string Sku { get; init; }

    public required string Eta { get; init; }
}

namespace Templar.Tests.DefaultNaming.Orders.Lines;

public abstract class Line : Entry
{
    public required string Sku { get; init; }
}

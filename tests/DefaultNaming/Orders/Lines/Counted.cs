namespace Templar.Tests.DefaultNaming.Orders.Lines;

// No Counted.tpl: renders by Line.tpl, the nearest base type's template.
public sealed class Counted : Line
{
    public required int Quantity { get; init; }
}

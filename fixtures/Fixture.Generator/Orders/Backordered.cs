using Templar.Rendering;

namespace Fixture.Generator.Orders;

[Template("Orders/Backordered")]
public sealed record Backordered(string Sku, string Eta) : ILine;

using Templar.Rendering;

namespace Fixture.Generator.Orders;

[Template("Orders/Stocked")]
public sealed record Stocked(string Sku);

using Templar.Rendering;

namespace Templar.Tests.Models.Names;

[Template("Names/Orders/Lines/Backordered")]
public sealed record Backordered(string Sku, string Eta);

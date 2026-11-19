using Templar.Rendering;

namespace Fixture.Generator.Orders;

[Template("Orders/Order")]
public sealed record Order(string Id, Sequence<ILine> Lines, int Count, Sequence<Tag> Tags);

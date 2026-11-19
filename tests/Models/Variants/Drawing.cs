using Templar.Rendering;

namespace Templar.Tests.Models.Variants;

public sealed class Drawing
{
    public required IShape Main { get; init; }

    public required Sequence<IShape> Shapes { get; init; }
}

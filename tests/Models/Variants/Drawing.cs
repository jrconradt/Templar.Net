using Templar.Rendering;

namespace Templar.Tests.Models.Variants;

[Template("Models/Variants/Drawing")]
public sealed record Drawing(IShape Main, Sequence<IShape> Shapes);

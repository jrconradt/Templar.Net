using Templar.Rendering;

namespace Templar.Tests.Models.Variants;

[Template("Models/Variants/Circle")]
public sealed record Circle(int Radius) : IShape;

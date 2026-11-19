using Templar.Rendering;

namespace Templar.Tests.Models.Variants;

[Template("Models/Variants/Square")]
public sealed record Square(int Side) : IShape;

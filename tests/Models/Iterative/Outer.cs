using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

[Template("Models/Iterative/Outer")]
public sealed record Outer(TwoLines? Inside);

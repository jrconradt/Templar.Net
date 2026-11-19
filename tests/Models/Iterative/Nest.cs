using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

[Template("Models/Iterative/Nest")]
public sealed record Nest(Nest? Inner, string Leaf = "");

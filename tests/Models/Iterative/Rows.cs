using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

[Template("Models/Iterative/Rows")]
public sealed record Rows(Sequence<Sequence<Item>> All);

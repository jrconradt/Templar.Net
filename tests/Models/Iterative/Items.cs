using Templar.Rendering;

namespace Templar.Tests.Models.Iterative;

[Template("Models/Iterative/Items")]
public sealed record Items(Sequence<Item> All);

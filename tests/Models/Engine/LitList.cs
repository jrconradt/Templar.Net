using Templar.Rendering;

namespace Templar.Tests.Models.Engine;

[Template("Models/Engine/LitList")]
public sealed record LitList(IReadOnlyList<Lit> Items);

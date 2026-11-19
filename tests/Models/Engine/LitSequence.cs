using Templar.Rendering;

namespace Templar.Tests.Models.Engine;

[Template("Models/Engine/LitSequence")]
public sealed record LitSequence(Sequence<Lit> Items);

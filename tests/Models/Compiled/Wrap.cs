using Templar.Rendering;

namespace Templar.Tests.Models.Compiled;

[Template("Models/Compiled/Wrap")]
public sealed record Wrap(Wrap? Inside, string Leaf = "");

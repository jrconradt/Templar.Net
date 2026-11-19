using Templar.Rendering;

namespace Templar.Tests.Models.Compiled;

[Template("Models/Compiled/Outer")]
public sealed record Outer(TwoLines Inside);

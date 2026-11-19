using Templar.Rendering;

namespace Templar.Tests.Models.Writer;

[Template("Models/Writer/Hosting")]
public sealed record Hosting(Inner Child);

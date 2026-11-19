using Templar.Rendering;

namespace Templar.Tests.Models.Entry;

[Template("Models/Entry/Simple")]
public sealed record Simple(string Verb, string Name);

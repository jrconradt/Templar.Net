using Templar.Rendering;

namespace Fixture.Generator;

[Template("SourceFile")]
public sealed record SourceFile(string Namespace, string Body);

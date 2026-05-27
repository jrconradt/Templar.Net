using Templar.Rendering;
namespace Templar.Generators.Emit;

internal sealed class CompositorFile : Compositor
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required Lines Properties { get; init; }
}

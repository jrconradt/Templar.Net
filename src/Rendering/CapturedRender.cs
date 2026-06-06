namespace Templar.Rendering;

public sealed class CapturedRender
{
    public required IComposable Child { get; init; }
    public Func<string, string>? Transform { get; init; }
}

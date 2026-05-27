namespace Templar.Rendering;

public sealed class RenderOptions
{
    public string IndentString { get; init; } = "    ";
    public string Newline { get; init; } = "\n";
    public bool StrictUndefined { get; init; } = false;
}

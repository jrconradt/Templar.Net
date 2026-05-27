namespace Templar.Rendering;

public abstract class Sequence : Compositor
{
    public IEnumerable<Compositor> Items { get; init; } = Array.Empty<Compositor>();
    protected abstract string Separator { get; }

    internal string SeparatorInternal => Separator;

    protected override string Structure => string.Empty;

    public override string Render()
    {
        string result = "";
        bool first = true;
        foreach (var item in Items)
        {
            if (!first)
            {
                result += Separator;
            }
            first = false;
            result += item.Render();
        }
        return result;
    }
}

public sealed class Lines : Sequence
{
    protected override string Separator => "\n";
}

public sealed class BlankLines : Sequence
{
    protected override string Separator => "\n\n";
}

public sealed class CommaList : Sequence
{
    protected override string Separator => ", ";
}

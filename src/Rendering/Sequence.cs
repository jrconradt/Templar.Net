namespace Templar.Rendering;

public sealed class Sequence : IComposable
{
    private readonly IEnumerable<IComposable> _items;
    private readonly string _separator;

    public Sequence(IEnumerable<IComposable> items, string separator)
    {
        _items = items;
        _separator = separator;
    }

    public static Sequence Lines(IEnumerable<IComposable> items)
    {
        return new Sequence(items, "\n");
    }

    public static Sequence BlankLines(IEnumerable<IComposable> items)
    {
        return new Sequence(items, "\n\n");
    }

    public static Sequence CommaList(IEnumerable<IComposable> items)
    {
        return new Sequence(items, ", ");
    }

    public string Render()
    {
        var writer = new TemplarWriter(new RenderOptions());
        RenderInto(writer);
        Renderer.Drive(writer);
        return writer.Result;
    }

    public void RenderInto(TemplarWriter writer)
    {
        writer.Frames.Push(new Renderer.SequenceFrame
        {
            Items = _items.GetEnumerator(),
            Separator = _separator,
        });
    }
}

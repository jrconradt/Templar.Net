namespace Templar.Rendering;

public static class Sequence
{
    public static Sequence<T> Lines<T>(IEnumerable<T> items)
    {
        return new Sequence<T>(items, "\n");
    }

    public static Sequence<T> BlankLines<T>(IEnumerable<T> items)
    {
        return new Sequence<T>(items, "\n\n");
    }

    public static Sequence<T> CommaList<T>(IEnumerable<T> items)
    {
        return new Sequence<T>(items, ", ");
    }
}

public sealed class Sequence<T>
{
    public Sequence(IEnumerable<T> items, string separator)
    {
        Items = items;
        Separator = separator;
    }

    public IEnumerable<T> Items { get; }

    public string Separator { get; }
}

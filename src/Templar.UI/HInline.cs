using Templar.Rendering;

namespace Templar.UI;

public static partial class H
{
    public static Fragment Inline(params object?[] parts)
    {
        var items = new List<Compositor>();
        foreach (var part in parts)
        {
            if (part is Compositor fragment)
            {
                items.Add(fragment);
            }
            else if (part is not null)
            {
                items.Add(new Text { Value = part });
            }
        }
        return new Fragment { Items = items };
    }
}

using Templar.Rendering;

namespace Templar.UI;

public enum ElementLayout
{
    Block,
    Inline,
    Void,
    Verbatim,
}

public sealed class Element : UIComponent
{
    public required string Tag { get; init; }

    [TemplateIgnore]
    public ElementLayout Layout { get; init; } = ElementLayout.Block;

    [TemplateIgnore]
    public string DefaultClass { get; init; } = "";

    [TemplateIgnore]
    public object? Class { get; init; }

    [TemplateIgnore]
    public object? Attrs { get; init; }

    [TemplateIgnore]
    public bool RawContent { get; init; }

    [TemplateBind("classes")]
    public Sequence RenderedClasses => new Sequence(BuildClasses(), " ");

    public bool HasClasses => BuildClasses().Count > 0;

    [TemplateBind("attrs")]
    public object? AttrsMarkup => BuildAttrs();

    [TemplateBind("body")]
    public IPreformattedContent? Body => Layout == ElementLayout.Verbatim
        ? new Verbatim(VerbatimText())
        : (IPreformattedContent?)null;

    protected override string Structure => Layout switch
    {
        ElementLayout.Void => "<{{ tag }}{{? hasClasses }} class=\"{{ classes }}\"{{?}}{{ attrs }}>",
        ElementLayout.Inline => "<{{ tag }}{{? hasClasses }} class=\"{{ classes }}\"{{?}}{{ attrs }}>{{ children }}</{{ tag }}>",
        ElementLayout.Verbatim => "<{{ tag }}{{? hasClasses }} class=\"{{ classes }}\"{{?}}{{ attrs }}>{{ body }}</{{ tag }}>",
        _ => """
            <{{ tag }}{{? hasClasses }} class="{{ classes }}"{{?}}{{ attrs }}>
                {{ children }}
            </{{ tag }}>
            """,
    };

    protected override void Validate()
    {
        Safety.TagName(Tag);
        BuildAttrs();
    }

    private string VerbatimText()
    {
        string content = ContentToString(Children);
        return RawContent ? content : Markup.Escape(content);
    }

    private static string ContentToString(object? children)
    {
        if (children is null)
        {
            return "";
        }
        if (children is string s)
        {
            return s;
        }
        if (children is IIndentedContent raw)
        {
            return raw.Value;
        }
        if (children is IComposable c)
        {
            return c.Render();
        }
        return children.ToString() ?? "";
    }

    private List<IComposable> BuildClasses()
    {
        var items = new List<IComposable>();
        if (!string.IsNullOrEmpty(DefaultClass))
        {
            items.Add(new Cls { Tokens = DefaultClass });
        }
        Append(items, Class);
        return items;
    }

    private static void Append(List<IComposable> items, object? style)
    {
        if (style is null)
        {
            return;
        }
        if (style is string token)
        {
            if (token.Length > 0)
            {
                items.Add(new Cls { Tokens = token });
            }
            return;
        }
        if (style is IComposable fragment)
        {
            items.Add(fragment);
            return;
        }
        if (style is IEnumerable<IComposable> fragments)
        {
            items.AddRange(fragments);
            return;
        }
        if (style is IEnumerable<string> tokens)
        {
            foreach (var t in tokens)
            {
                if (!string.IsNullOrEmpty(t))
                {
                    items.Add(new Cls { Tokens = t });
                }
            }
        }
    }

    private object? BuildAttrs()
    {
        if (Attrs is null)
        {
            return null;
        }
        if (Attrs is RawHtml raw)
        {
            return Normalize(raw);
        }
        if (Attrs is IComposable fragment)
        {
            return fragment;
        }
        if (Attrs is IEnumerable<IComposable> fragments)
        {
            return new Sequence(fragments, "");
        }
        throw new MarkupSecurityException(
            "Element.Attrs must be an Attr, a sequence of Attr, or Markup.Raw(...) for trusted markup. A plain string is rejected to prevent attribute injection.");
    }

    private static RawHtml Normalize(RawHtml attrs)
    {
        string value = attrs.Value;
        if (value.Length == 0 || value[0] == ' ')
        {
            return attrs;
        }
        return Markup.Raw($" {value}");
    }
}

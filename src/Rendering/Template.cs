namespace Templar.Rendering;

public sealed class Template
{
    private readonly string _source;
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private RenderOptions _options = new();

    private Template(string source)
    {
        _source = source;
    }

    public static Template Parse(string text)
    {
        Validate(text);
        return new(text);
    }

    private static void Validate(string text)
    {
        int p = 0;
        int line = 1, col = 1;
        while (p < text.Length)
        {
            if (text[p] == '\\' && p + 1 < text.Length)
            {
                char n = text[p + 1];
                if (n == '\\')
                {
                    p += 2;
                    col += 2;
                    continue;
                }
                if (n == '{' && p + 2 < text.Length
                    && text[p + 2] == '{')
                {
                    p += 3;
                    col += 3;
                    continue;
                }
                if (n == '}' && p + 2 < text.Length
                    && text[p + 2] == '}')
                {
                    p += 3;
                    col += 3;
                    continue;
                }
            }

            if (p + 1 < text.Length && text[p] == '{'
                && text[p + 1] == '{')
            {
                int openLine = line, openCol = col;
                char marker = (p + 2 < text.Length) ? text[p + 2] : '\0';
                if (marker == '?' || marker == '&'
                    || marker == '>')
                {
                    throw new TemplateParseException($"Unsupported tag marker '{marker}' at line {openLine}, column {openCol}",
                                                    openLine,
                                                    openCol);
                }
                bool markerConsumed = marker == '#';
                int contentStart = markerConsumed ? p + 3 : p + 2;
                int close = text.IndexOf("}}",
                                         contentStart,
                                         StringComparison.Ordinal);
                if (close < 0)
                {
                    throw new TemplateParseException($"Unclosed tag starting at line {openLine}, column {openCol}",
                                                    openLine,
                                                    openCol);
                }
                if (!markerConsumed && text.IndexOf('|', contentStart, close - contentStart) >= 0)
                {
                    throw new TemplateParseException($"Unsupported '|' in tag at line {openLine}, column {openCol}",
                                                    openLine,
                                                    openCol);
                }

                int afterClose = close + 2;
                for (int i = p; i < afterClose; i++)
                {
                    if (text[i] == '\n')
                    {
                        line++;
                        col = 1;
                    }
                    else
                    {
                        col++;
                    }
                }
                p = afterClose;
                continue;
            }

            if (text[p] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
            p++;
        }
    }

    public object? this[string key]
    {
        get => _values.TryGetValue(key, out var val) ? val : null;
        set => _values[key] = value;
    }

    public Template Set(string key, object? value)
    {
        _values[key] = value;
        return this;
    }

    public Template WithOptions(RenderOptions options)
    {
        _options = options;
        return this;
    }

    public string Render()
    {
        return Renderer.Render(_source,
                               _values,
                               _options);
    }
}

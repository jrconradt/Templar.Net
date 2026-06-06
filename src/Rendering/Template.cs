using System.Collections.Concurrent;

namespace Templar.Rendering;

public sealed class Template
{
    private static readonly ConcurrentDictionary<string, bool> ValidatedSources = new(StringComparer.Ordinal);

    private readonly string _source;
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly FilterRegistry _filters = new();
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

    internal static Template ParseCached(string text)
    {
        if (!ValidatedSources.ContainsKey(text))
        {
            Validate(text);
            ValidatedSources.TryAdd(text, true);
        }
        return new(text);
    }

    private static void Validate(string text)
    {
        int depth = 0;
        (int Line, int Col) lastOpenerPos = (0, 0);
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
                bool markerConsumed = marker == '#' || marker == '?'
                    || marker == '&'
                    || marker == '>';
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

                if (marker == '?')
                {
                    string body = text[contentStart..close].Trim();
                    if (body.Length == 0)
                    {
                        if (depth == 0)
                        {
                            throw new TemplateParseException($"Unexpected '{{{{?}}}}' (no matching conditional) at line {openLine}, column {openCol}",
                                                            openLine,
                                                            openCol);
                        }
                        depth--;
                    }
                    else if (body == "else")
                    {
                        if (depth == 0)
                        {
                            throw new TemplateParseException($"Unexpected '{{{{?else}}}}' (no matching conditional) at line {openLine}, column {openCol}",
                                                            openLine,
                                                            openCol);
                        }
                    }
                    else
                    {
                        bool negated = body.StartsWith('!');
                        string name = (negated ? body[1..] : body).Trim();
                        if (name.Length == 0)
                        {
                            throw new TemplateParseException($"Empty conditional expression at line {openLine}, column {openCol}",
                                                            openLine,
                                                            openCol);
                        }
                        depth++;
                        lastOpenerPos = (openLine, openCol);
                    }
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

        if (depth != 0)
        {
            throw new TemplateParseException($"Unterminated conditional opened at line {lastOpenerPos.Line}, column {lastOpenerPos.Col} (expected '{{{{?}}}}')",
                                            lastOpenerPos.Line,
                                            lastOpenerPos.Col);
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

    public Template AddFilter(string name, Func<object?, string> filter)
    {
        _filters.Add(name, filter);
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
                               _filters,
                               _options);
    }

    internal IDictionary<string, object?> ValuesInternal => _values;
    internal FilterRegistry FiltersInternal => _filters;
    internal RenderOptions OptionsInternal => _options;
}

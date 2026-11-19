using System.Globalization;

namespace Templar.Rendering;

public sealed class TemplarWriter
{
    internal RenderOptions Options { get; }
    internal Stack<object> Frames { get; } = new();

    private string _output = "";
    private int _lineStart = 0;
    private bool _lineHadExpression = false;
    private bool _atLineStart = true;
    private string _currentIndent = "";

    internal TemplarWriter(RenderOptions options)
    {
        Options = options;
    }

    internal string Result => _output;

    public static string Render<T>(T model,
                                   Func<TemplarWriter, T, IEnumerator<object?>> steps,
                                   RenderOptions? options) where T : class
    {
        var writer = new TemplarWriter(options ?? new RenderOptions());
        writer.Frames.Push(new Renderer.CompiledFrame
        {
            Steps = steps(writer, model),
        });
        Renderer.Drive(writer);
        return writer.Result;
    }

    public void Literal(string text)
    {
        string nl = Options.Newline;
        foreach (char ch in text)
        {
            WriteLiteralChar(ch, nl);
        }
    }

    public void Text(string? value)
    {
        WriteValueString(value ?? "", Options.Newline);
    }

    public void Format<T>(T value)
    {
        if (value is null)
        {
            Text(null);
            return;
        }
        if (value is IFormattable formattable)
        {
            Text(formattable.ToString(null, CultureInfo.InvariantCulture));
            return;
        }
        Text(value.ToString());
    }

    public object? Compose<T>(T? model, Func<TemplarWriter, T, IEnumerator<object?>> steps) where T : class
    {
        MarkExpression();
        if (model is null)
        {
            return null;
        }
        return new Renderer.CompiledFrame
        {
            Steps = steps(this, model),
        };
    }

    public object? List<T>(Sequence<T>? items, Func<TemplarWriter, T, IEnumerator<object?>> steps)
    {
        MarkExpression();
        if (items is null)
        {
            return null;
        }
        return new Renderer.SequenceFrame
        {
            Items = Each(items.Items, steps),
            Separator = items.Separator,
        };
    }

    public object? List<T>(IEnumerable<T>? items, Func<TemplarWriter, T, IEnumerator<object?>> steps)
    {
        MarkExpression();
        if (items is null)
        {
            return null;
        }
        return new Renderer.SequenceFrame
        {
            Items = Each(items, steps),
            Separator = "\n",
        };
    }

    public static IEnumerator<object?> TextItem(TemplarWriter writer, string? value)
    {
        writer.Text(value);
        yield break;
    }

    public static IEnumerator<object?> FormatItem<T>(TemplarWriter writer, T value)
    {
        writer.Format(value);
        yield break;
    }

    public static IEnumerator<object?> ListItem<T>(TemplarWriter writer,
                                                   Sequence<T> items,
                                                   Func<TemplarWriter, T, IEnumerator<object?>> steps)
    {
        yield return writer.List(items, steps);
    }

    public static IEnumerator<object?> ListItem<T>(TemplarWriter writer,
                                                   IEnumerable<T> items,
                                                   Func<TemplarWriter, T, IEnumerator<object?>> steps)
    {
        yield return writer.List(items, steps);
    }

    public override string ToString()
    {
        return _output;
    }

    private IEnumerator<IEnumerator<object?>> Each<T>(IEnumerable<T> items,
                                                      Func<TemplarWriter, T, IEnumerator<object?>> steps)
    {
        foreach (var item in items)
        {
            if (item is null)
            {
                yield return Nothing();
                continue;
            }
            yield return steps(this, item);
        }
    }

    private static IEnumerator<object?> Nothing()
    {
        yield break;
    }

    internal void MarkExpression()
    {
        _lineHadExpression = true;
    }

    internal void WriteLiteralChar(char ch, string nl)
    {
        if (ch == '\n')
        {
            EmitNewline(nl);
            return;
        }
        if (ch == '\r')
        {
            return;
        }
        EnsureIndent();
        _output += ch;
    }

    internal void WriteValueString(string val, string nl)
    {
        _lineHadExpression = true;
        if (val.Length == 0)
        {
            return;
        }
        if (val.Contains('\r'))
        {
            val = val.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        if (!val.Contains('\n'))
        {
            EnsureIndent();
            _output += val;
            return;
        }

        EnsureIndent();
        int col = _output.Length - _lineStart;
        string extra = col > _currentIndent.Length
            ? BuildIndent(col - _currentIndent.Length)
            : "";
        _currentIndent += extra;

        int start = 0;
        while (true)
        {
            int next = val.IndexOf('\n', start);
            int lineEnd = next < 0 ? val.Length : next;
            if (start > 0 && lineEnd > start)
            {
                EnsureIndent();
            }
            if (lineEnd > start)
            {
                _output += val[start..lineEnd];
            }
            if (next < 0)
            {
                break;
            }
            EmitNewline(nl);
            _lineHadExpression = true;
            start = next + 1;
        }

        if (extra.Length > 0)
        {
            _currentIndent = _currentIndent[..^extra.Length];
        }
    }

    internal void WriteSeparator(string sep)
    {
        if (sep.Contains('\r'))
        {
            sep = sep.Replace("\r\n", "\n").Replace('\r', '\n');
        }
        _output += sep.Replace("\n", Options.Newline);
        if (sep.EndsWith('\n'))
        {
            _atLineStart = true;
            _lineStart = _output.Length;
            _lineHadExpression = false;
        }
    }

    internal string PushColumnIndent()
    {
        if (_atLineStart)
        {
            _output += _currentIndent;
            _atLineStart = false;
        }
        int col = _output.Length - _lineStart;
        string extra = col > _currentIndent.Length
            ? BuildIndent(col - _currentIndent.Length)
            : "";
        _currentIndent += extra;
        return extra;
    }

    internal void PopIndent(string extra)
    {
        if (extra.Length > 0)
        {
            _currentIndent = _currentIndent[..^extra.Length];
        }
    }

    private void EnsureIndent()
    {
        if (_atLineStart)
        {
            _output += _currentIndent;
            _atLineStart = false;
        }
    }

    private void EmitNewline(string nl)
    {
        if (_lineHadExpression && IsLineWsOnly())
        {
            _output = _output[.._lineStart];
        }
        else
        {
            _output += nl;
        }
        _atLineStart = true;
        _lineHadExpression = false;
        _lineStart = _output.Length;
    }

    private bool IsLineWsOnly()
    {
        for (int i = _lineStart; i < _output.Length; i++)
        {
            char ch = _output[i];
            if (ch != ' ' && ch != '\t')
            {
                return false;
            }
        }
        return true;
    }

    private string BuildIndent(int width)
    {
        if (width <= 0)
        {
            return "";
        }
        string unit = Options.IndentString;
        if (unit.Length == 0)
        {
            return new string(' ', width);
        }
        string indent = "";
        while (indent.Length + unit.Length <= width)
        {
            indent += unit;
        }
        if (indent.Length < width)
        {
            indent += new string(' ', width - indent.Length);
        }
        return indent;
    }
}

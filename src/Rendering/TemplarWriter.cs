using System.Collections;

namespace Templar.Rendering;

public sealed class TemplarWriter
{
    internal RenderOptions Options { get; }
    internal Stack<object> Frames { get; } = new();

    private readonly FilterRegistry _builtinFilters = new();
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

    public void Literal(string text)
    {
        string nl = Options.Newline;
        foreach (char ch in text)
        {
            WriteLiteralChar(ch, nl);
        }
    }

    public bool Truthy(object? value)
    {
        return IsTruthy(value);
    }

    public IComposable? Value(object? value, string? filter = null, bool raw = false)
    {
        MarkExpression();
        string nl = Options.Newline;
        var esc = Options.Escape;

        if (filter is not null)
        {
            string filtered = _builtinFilters.Apply(filter, value, null);
            if (!raw && esc is not null)
            {
                filtered = esc(filtered);
            }
            Append(filtered);
            return null;
        }
        if (value is null)
        {
            return null;
        }
        if (value is IPreformattedContent preformatted)
        {
            WriteVerbatim(preformatted.Value, nl);
            return null;
        }
        if (value is IIndentedContent indented)
        {
            WriteValueString(indented.Value, nl);
            return null;
        }
        if (value is string sval)
        {
            WriteValueString((!raw && esc is not null) ? esc(sval) : sval, nl);
            return null;
        }
        if (value is IComposable composable)
        {
            return composable;
        }
        if (value is IEnumerable<IComposable> comps)
        {
            return new Sequence(comps, nl);
        }
        if (value is IEnumerable<string> strs)
        {
            string joined = string.Join(nl, strs);
            WriteValueString((!raw && esc is not null) ? esc(joined) : joined, nl);
            return null;
        }
        string fallback = value.ToString() ?? "";
        WriteValueString((!raw && esc is not null) ? esc(fallback) : fallback, nl);
        return null;
    }

    public void Compiled(IEnumerator<IComposable> steps)
    {
        Frames.Push(new Renderer.CompiledFrame
        {
            Steps = steps,
        });
    }

    public override string ToString()
    {
        return _output;
    }

    internal void MarkExpression()
    {
        _lineHadExpression = true;
    }

    internal void Append(string s)
    {
        EnsureIndent();
        _output += s;
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

    internal void WriteVerbatim(string val, string nl)
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

        EnsureIndent();
        int start = 0;
        while (true)
        {
            int next = val.IndexOf('\n', start);
            int lineEnd = next < 0 ? val.Length : next;
            if (lineEnd > start)
            {
                _output += val[start..lineEnd];
            }
            if (next < 0)
            {
                break;
            }
            _output += nl;
            _atLineStart = false;
            _lineStart = _output.Length;
            start = next + 1;
        }
    }

    internal void WriteSeparator(string sep)
    {
        _output += sep;
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

    internal static bool IsTruthy(object? v)
    {
        return v switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            IEnumerable e => HasAny(e),
            _ => true,
        };
    }

    private static bool HasAny(IEnumerable e)
    {
        var en = e.GetEnumerator();
        try
        {
            return en.MoveNext();
        }
        finally
        {
            if (en is IDisposable d)
            {
                d.Dispose();
            }
        }
    }
}

namespace Templar.Rendering;

internal static class Renderer
{
    internal sealed class ScanFrame
    {
        public required string Source;
        public int Pos;
        public required IDictionary<string, object?> Values;
        public required FilterRegistry Filters;
        public required RenderOptions Options;
    }

    internal sealed class SequenceFrame
    {
        public required IEnumerator<IComposable> Items;
        public required string Separator;
        public bool First = true;
    }

    internal sealed class CompiledFrame
    {
        public required IEnumerator<IComposable> Steps;
    }

    internal sealed class CaptureFrame
    {
        public required IComposable Child;
        public required Func<string, string>? Transform;
        public required TemplarWriter Target;
        public TemplarWriter? Sub;
    }

    private sealed record IndentPop(string Extra);

    internal static string Render(string source,
                                  IDictionary<string, object?> values,
                                  FilterRegistry filters,
                                  RenderOptions options)
    {
        var writer = new TemplarWriter(options);
        writer.Frames.Push(new ScanFrame
        {
            Source = source,
            Pos = 0,
            Values = values,
            Filters = filters,
            Options = options,
        });
        Drive(writer);
        return writer.Result;
    }

    internal static void Drive(TemplarWriter root)
    {
        var writers = new Stack<TemplarWriter>();
        writers.Push(root);
        TemplarWriter writer = root;
        object? current = null;

        bool Advance()
        {
            while (writer.Frames.Count == 0)
            {
                if (writers.Count <= 1)
                {
                    return false;
                }
                writers.Pop();
                writer = writers.Peek();
            }
            current = writer.Frames.Pop();
            return true;
        }

        if (!Advance())
        {
            return;
        }

        while (true)
        {
            if (current is CaptureFrame capture)
            {
                if (capture.Sub is null)
                {
                    var sub = new TemplarWriter(writer.Options);
                    capture.Sub = sub;
                    capture.Child.RenderInto(sub);
                    capture.Target.Frames.Push(capture);
                    writers.Push(sub);
                    writer = sub;
                }
                else
                {
                    string captured = capture.Sub.Result;
                    if (capture.Transform is not null)
                    {
                        captured = capture.Transform(captured);
                    }
                    writer.WriteVerbatim(captured, writer.Options.Newline);
                }
                if (!Advance())
                {
                    return;
                }
                continue;
            }

            if (current is IndentPop pop)
            {
                writer.PopIndent(pop.Extra);
                if (!Advance())
                {
                    return;
                }
                continue;
            }

            if (current is ScanFrame scan)
            {
                if (scan.Pos >= scan.Source.Length)
                {
                    if (!Advance())
                    {
                        return;
                    }
                    continue;
                }

                string s = scan.Source;
                int p = scan.Pos;
                char c = s[p];
                string nl = scan.Options.Newline;

                if (c == '\\' && p + 1 < s.Length)
                {
                    char n = s[p + 1];
                    if (n == '\\')
                    {
                        writer.WriteLiteralChar('\\', nl);
                        scan.Pos += 2;
                        continue;
                    }
                    if (n == '{' && p + 2 < s.Length
                        && s[p + 2] == '{')
                    {
                        writer.WriteLiteralChar('{', nl);
                        writer.WriteLiteralChar('{', nl);
                        scan.Pos += 3;
                        continue;
                    }
                    if (n == '}' && p + 2 < s.Length
                        && s[p + 2] == '}')
                    {
                        writer.WriteLiteralChar('}', nl);
                        writer.WriteLiteralChar('}', nl);
                        scan.Pos += 3;
                        continue;
                    }
                }

                if (p + 1 < s.Length && c == '{'
                    && s[p + 1] == '{')
                {
                    char marker = (p + 2 < s.Length) ? s[p + 2] : '\0';
                    bool markerConsumed = marker == '#' || marker == '?'
                        || marker == '&'
                        || marker == '>';
                    int contentStart = markerConsumed ? p + 3 : p + 2;
                    int closePos = s.IndexOf("}}",
                                             contentStart,
                                             StringComparison.Ordinal);
                    string body = s[contentStart..closePos].Trim();
                    scan.Pos = closePos + 2;

                    if (marker == '#')
                    {
                        continue;
                    }

                    if (marker == '?')
                    {
                        if (body.Length == 0)
                        {
                            continue;
                        }
                        if (body == "else")
                        {
                            scan.Pos = SkipPastEnd(s, scan.Pos);
                            continue;
                        }
                        bool negated = body.StartsWith('!');
                        string name = (negated ? body[1..] : body).Trim();
                        scan.Values.TryGetValue(name, out var condV);
                        bool truthy = TemplarWriter.IsTruthy(condV);
                        if (negated)
                        {
                            truthy = !truthy;
                        }
                        if (!truthy)
                        {
                            var (jumpPos, _) = SkipPastEndOrElse(s, scan.Pos);
                            scan.Pos = jumpPos;
                        }
                        continue;
                    }

                    bool rawTag = marker == '&'
                        || marker == '>';
                    var esc = scan.Options.Escape;
                    string varName;
                    string? filterName = null;
                    int pipeIdx = body.IndexOf('|');
                    if (pipeIdx >= 0)
                    {
                        varName = body[..pipeIdx].Trim();
                        filterName = body[(pipeIdx + 1)..].Trim();
                    }
                    else
                    {
                        varName = body;
                    }

                    bool present = scan.Values.TryGetValue(varName, out var value);
                    if (scan.Options.StrictUndefined && !present)
                    {
                        throw new TemplateRenderException($"Undefined variable '{varName}' (strict mode)",
                                                         filterName: null,
                                                         variableName: varName);
                    }

                    writer.MarkExpression();

                    if (filterName is not null)
                    {
                        var filtered = scan.Filters.Apply(filterName, value, varName);
                        if (!rawTag && esc is not null)
                        {
                            filtered = esc(filtered);
                        }
                        writer.Append(filtered);
                        continue;
                    }

                    if (value is null)
                    {
                        continue;
                    }
                    if (value is CapturedRender captured)
                    {
                        writer.Frames.Push(scan);
                        writer.Frames.Push(new CaptureFrame
                        {
                            Child = captured.Child,
                            Transform = captured.Transform,
                            Target = writer,
                        });
                        current = writer.Frames.Pop();
                        continue;
                    }
                    if (value is IPreformattedContent preformattedContent)
                    {
                        writer.WriteVerbatim(preformattedContent.Value, nl);
                        continue;
                    }
                    if (value is IIndentedContent indentedContent)
                    {
                        writer.WriteValueString(indentedContent.Value, nl);
                        continue;
                    }
                    if (value is string sval)
                    {
                        writer.WriteValueString((!rawTag && esc is not null) ? esc(sval) : sval, nl);
                        continue;
                    }

                    if (value is IComposable composable)
                    {
                        string extra = writer.PushColumnIndent();
                        writer.Frames.Push(scan);
                        writer.Frames.Push(new IndentPop(extra));
                        composable.RenderInto(writer);
                        current = writer.Frames.Pop();
                        continue;
                    }

                    if (value is IEnumerable<IComposable> comps)
                    {
                        string extra = writer.PushColumnIndent();
                        writer.Frames.Push(scan);
                        writer.Frames.Push(new IndentPop(extra));
                        writer.Frames.Push(new SequenceFrame
                        {
                            Items = comps.GetEnumerator(),
                            Separator = nl,
                        });
                        current = writer.Frames.Pop();
                        continue;
                    }

                    if (value is IEnumerable<string> strs)
                    {
                        string joined = string.Join(nl, strs);
                        writer.WriteValueString((!rawTag && esc is not null) ? esc(joined) : joined, nl);
                        continue;
                    }

                    string fallback = value.ToString() ?? "";
                    writer.WriteValueString((!rawTag && esc is not null) ? esc(fallback) : fallback, nl);
                    continue;
                }

                writer.WriteLiteralChar(c, nl);
                scan.Pos++;
                continue;
            }

            if (current is SequenceFrame seq)
            {
                if (!seq.Items.MoveNext())
                {
                    seq.Items.Dispose();
                    if (!Advance())
                    {
                        return;
                    }
                    continue;
                }
                if (!seq.First)
                {
                    writer.WriteSeparator(seq.Separator);
                }
                seq.First = false;
                var child = seq.Items.Current;
                writer.Frames.Push(seq);
                child.RenderInto(writer);
                current = writer.Frames.Pop();
                continue;
            }

            if (current is CompiledFrame compiled)
            {
                if (compiled.Steps.MoveNext())
                {
                    var step = compiled.Steps.Current;
                    string extra = writer.PushColumnIndent();
                    writer.Frames.Push(compiled);
                    writer.Frames.Push(new IndentPop(extra));
                    step.RenderInto(writer);
                    current = writer.Frames.Pop();
                    continue;
                }
                compiled.Steps.Dispose();
                if (!Advance())
                {
                    return;
                }
                continue;
            }

            if (!Advance())
            {
                return;
            }
        }
    }

    private static int SkipPastEnd(string src, int from)
    {
        int depth = 0;
        int p = from;
        while (p < src.Length)
        {
            if (src[p] == '\\' && p + 1 < src.Length)
            {
                char n = src[p + 1];
                if (n == '\\')
                {
                    p += 2;
                    continue;
                }
                if (n == '{' && p + 2 < src.Length
                    && src[p + 2] == '{')
                {
                    p += 3;
                    continue;
                }
                if (n == '}' && p + 2 < src.Length
                    && src[p + 2] == '}')
                {
                    p += 3;
                    continue;
                }
            }
            if (p + 1 < src.Length && src[p] == '{'
                && src[p + 1] == '{')
            {
                if (p + 2 < src.Length && src[p + 2] == '?')
                {
                    int close = src.IndexOf("}}",
                                            p + 3,
                                            StringComparison.Ordinal);
                    string body = src[(p + 3)..close].Trim();
                    if (body.Length == 0)
                    {
                        if (depth == 0)
                        {
                            return close + 2;
                        }
                        depth--;
                    }
                    else if (body != "else")
                    {
                        depth++;
                    }
                    p = close + 2;
                    continue;
                }
                else
                {
                    int close = src.IndexOf("}}",
                                            p + 2,
                                            StringComparison.Ordinal);
                    p = close + 2;
                    continue;
                }
            }
            p++;
        }
        return src.Length;
    }

    private static (int Pos, bool LandedOnElse) SkipPastEndOrElse(string src, int from)
    {
        int depth = 0;
        int p = from;
        while (p < src.Length)
        {
            if (src[p] == '\\' && p + 1 < src.Length)
            {
                char n = src[p + 1];
                if (n == '\\')
                {
                    p += 2;
                    continue;
                }
                if (n == '{' && p + 2 < src.Length
                    && src[p + 2] == '{')
                {
                    p += 3;
                    continue;
                }
                if (n == '}' && p + 2 < src.Length
                    && src[p + 2] == '}')
                {
                    p += 3;
                    continue;
                }
            }
            if (p + 1 < src.Length && src[p] == '{'
                && src[p + 1] == '{')
            {
                if (p + 2 < src.Length && src[p + 2] == '?')
                {
                    int close = src.IndexOf("}}",
                                            p + 3,
                                            StringComparison.Ordinal);
                    string body = src[(p + 3)..close].Trim();
                    if (body.Length == 0)
                    {
                        if (depth == 0)
                        {
                            return (close + 2, false);
                        }
                        depth--;
                    }
                    else if (body == "else")
                    {
                        if (depth == 0)
                        {
                            return (close + 2, true);
                        }
                    }
                    else
                    {
                        depth++;
                    }
                    p = close + 2;
                    continue;
                }
                else
                {
                    int close = src.IndexOf("}}",
                                            p + 2,
                                            StringComparison.Ordinal);
                    p = close + 2;
                    continue;
                }
            }
            p++;
        }
        return (src.Length, false);
    }
}

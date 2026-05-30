using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Templar.UI.Generators;

[Generator]
public sealed class HtmlComponentGenerator : IIncrementalGenerator
{
    private const string Extension = ".html.tpl";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNs = context.AnalyzerConfigOptionsProvider.Select(
            (opts, _) => opts.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns) ? ns : null);

        var projDir = context.AnalyzerConfigOptionsProvider.Select(
            (opts, _) => opts.GlobalOptions.TryGetValue("build_property.ProjectDir", out var d) ? d : null);

        var templates = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .Select((at, ct) => new Input(at.Path, at.GetText(ct)?.ToString() ?? ""));

        var combined = templates.Combine(rootNs.Combine(projDir));

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (input, (ns, dir)) = tuple;
            if (string.IsNullOrEmpty(ns))
            {
                return;
            }

            var location = Location.From(input.Path, dir);
            var className = UpperFirst(location.LeafName);
            var fullNs = string.Join(".", new[] { ns! }.Concat(location.FolderSegments));

            var placeholders = Scan(input.Content);
            var source = Emit(fullNs,
                              className,
                              input.Content,
                              placeholders);

            var hint = string.Join(".", location.FolderSegments.Concat(new[] { className })) + ".g.cs";
            spc.AddSource(hint, SourceText.From(source, Encoding.UTF8));
        });
    }

    private enum Kind
    {
        Text,
        Raw,
        Child,
    }

    private static string Emit(string ns,
                               string className,
                               string content,
                               IReadOnlyList<(string Name, Kind Kind)> placeholders)
    {
        var props = new List<string>();
        foreach (var (name, kind) in placeholders)
        {
            var prop = UpperFirst(Sanitize(name));
            if (string.Equals(prop, "Children", StringComparison.Ordinal))
            {
                continue;
            }
            props.Add(kind switch
            {
                Kind.Raw => $"    public global::Templar.UI.RawHtml {prop} {{ get; init; }}",
                Kind.Child => $"    public global::Templar.Rendering.Compositor? {prop} {{ get; init; }}",
                _ => $"    public string {prop} {{ get; init; }} = \"\";",
            });
        }

        var body = props.Count > 0 ? string.Join("\n", props) + "\n" : "";

        return "#nullable enable\n\n"
            + $"namespace {ns};\n\n"
            + "[global::System.CodeDom.Compiler.GeneratedCode(\"Templar.UI.Generators\", \"1.0.0\")]\n"
            + $"public sealed class {className} : global::Templar.UI.UIComponent\n"
            + "{\n"
            + $"    protected override string Structure => \"{EscapeLiteral(content)}\";\n"
            + (body.Length > 0 ? "\n" + body : "")
            + "}\n";
    }

    private static IReadOnlyList<(string Name, Kind Kind)> Scan(string text)
    {
        var found = new List<(string, Kind)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pos = 0;
        while (pos < text.Length - 1)
        {
            var c = text[pos];

            if (c == '\\' && pos + 1 < text.Length)
            {
                var n = text[pos + 1];
                if (n == '\\')
                {
                    pos += 2;
                    continue;
                }
                if ((n == '{' || n == '}') && pos + 2 < text.Length
                    && text[pos + 2] == n)
                {
                    pos += 3;
                    continue;
                }
            }

            if (c != '{' || text[pos + 1] != '{')
            {
                pos++;
                continue;
            }

            var marker = pos + 2 < text.Length ? text[pos + 2] : '\0';
            if (marker == '#' || marker == '?')
            {
                var skipClose = text.IndexOf("}}", pos + 3, StringComparison.Ordinal);
                pos = skipClose < 0 ? text.Length : skipClose + 2;
                continue;
            }

            var kind = Kind.Text;
            var contentStart = pos + 2;
            if (marker == '&')
            {
                kind = Kind.Raw;
                contentStart = pos + 3;
            }
            else if (marker == '>')
            {
                kind = Kind.Child;
                contentStart = pos + 3;
            }

            var close = text.IndexOf("}}", contentStart, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            var inner = text.Substring(contentStart, close - contentStart).Trim();
            var pipe = inner.IndexOf('|');
            var name = (pipe >= 0 ? inner.Substring(0, pipe) : inner).Trim();
            if (IsIdentifier(name) && seen.Add(name))
            {
                found.Add((name, kind));
            }
            pos = close + 2;
        }
        return found;
    }

    private static string EscapeLiteral(string s)
    {
        return string.Concat(s.Select(EscapeChar));
    }

    private static string EscapeChar(char c)
    {
        return c switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => $"{c}",
        };
    }

    private static bool IsIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        if (!char.IsLetter(s[0]) && s[0] != '_')
        {
            return false;
        }
        for (var i = 1; i < s.Length; i++)
        {
            if (!char.IsLetterOrDigit(s[i]) && s[i] != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "_";
        }
        var safe = new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return char.IsDigit(safe[0]) ? "_" + safe : safe;
    }

    private static string UpperFirst(string s)
    {
        return char.IsLower(s[0]) ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    }

    private sealed class Location
    {
        public IReadOnlyList<string> FolderSegments { get; }
        public string LeafName { get; }

        private Location(IReadOnlyList<string> folderSegments, string leafName)
        {
            FolderSegments = folderSegments;
            LeafName = leafName;
        }

        public static Location From(string path, string? projectDir)
        {
            var normalized = path.Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');
            var leafWithExt = lastSlash < 0 ? normalized : normalized.Substring(lastSlash + 1);
            var leaf = leafWithExt.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? leafWithExt.Substring(0, leafWithExt.Length - Extension.Length)
                : leafWithExt;

            var folder = lastSlash < 0 ? "" : normalized.Substring(0, lastSlash);
            if (!string.IsNullOrEmpty(projectDir))
            {
                var dir = projectDir!.Replace('\\', '/').TrimEnd('/');
                if (folder.StartsWith($"{dir}/", StringComparison.OrdinalIgnoreCase))
                {
                    folder = folder.Substring(dir.Length + 1);
                }
                else if (string.Equals(folder, dir, StringComparison.OrdinalIgnoreCase))
                {
                    folder = "";
                }
            }

            var segments = folder
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Sanitize)
                .ToList();

            return new Location(segments, Sanitize(leaf));
        }
    }

    private readonly record struct Input(string Path, string Content);
}

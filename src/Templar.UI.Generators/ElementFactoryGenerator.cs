using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Templar.UI.Generators;

[Generator]
public sealed class ElementFactoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tables = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(".elements", System.StringComparison.OrdinalIgnoreCase))
            .Select((at, ct) => at.GetText(ct)?.ToString() ?? "")
            .Collect();

        context.RegisterSourceOutput(tables, (spc, texts) =>
        {
            var defs = new List<ElementDef>();
            foreach (var text in texts)
            {
                defs.AddRange(Parse(text));
            }
            if (defs.Count == 0)
            {
                return;
            }
            spc.AddSource("Markup.g.cs", SourceText.From(Emit(defs), Encoding.UTF8));
        });
    }

    private static IEnumerable<ElementDef> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal))
            {
                continue;
            }

            var segments = line.Split(':');
            if (segments.Length < 2)
            {
                continue;
            }

            var names = segments[0].Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
            var layout = segments[1].Trim().ToLowerInvariant();
            var defaultClasses = segments.Length >= 3 ? segments[2].Trim() : "";

            foreach (var name in names)
            {
                yield return new ElementDef(name, layout, defaultClasses);
            }
        }
    }

    private static string Emit(IReadOnlyList<ElementDef> defs)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var methods = new List<string>();
        foreach (var def in defs)
        {
            if (!seen.Add(def.Name))
            {
                continue;
            }
            methods.Add(EmitMethod(def));
        }

        var header = "#nullable enable\n\n"
            + "namespace Templar.UI;\n\n"
            + "[global::System.CodeDom.Compiler.GeneratedCode(\"Templar.UI.Generators\", \"1.0.0\")]\n"
            + "public static partial class Markup\n{\n";

        return header + string.Join("\n", methods) + "\n}\n";
    }

    private static string EmitMethod(ElementDef def)
    {
        var layout = def.Layout switch
        {
            "void" => "Void",
            "inline" => "Inline",
            "verbatim" => "Verbatim",
            _ => "Block",
        };
        var isVoid = def.Layout == "void";

        var paramList = isVoid
            ? "string? classes = null, object? attrs = null"
            : "object? children = null, string? classes = null, object? attrs = null";

        var init = "{ Tag = \"" + def.Name + "\""
            + ", Layout = global::Templar.UI.ElementLayout." + layout
            + (def.DefaultClasses.Length > 0 ? ", DefaultClass = \"" + def.DefaultClasses + "\"" : "")
            + (isVoid ? "" : ", Children = children")
            + ", Class = classes"
            + ", Attrs = attrs }";

        var method = Pascal(def.Name);
        return $"    public static global::Templar.UI.Element {method}({paramList}) => new global::Templar.UI.Element {init};";
    }

    private static string Pascal(string s)
    {
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    private sealed record ElementDef(string Name, string Layout, string DefaultClasses);
}

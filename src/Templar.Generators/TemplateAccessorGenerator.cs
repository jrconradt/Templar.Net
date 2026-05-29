using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Templar.Generators;

[Generator]
public sealed class TemplateAccessorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNs = context.AnalyzerConfigOptionsProvider.Select(
            (opts, _) => opts.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns) ? ns : null);

        var projDir = context.AnalyzerConfigOptionsProvider.Select(
            (opts, _) => opts.GlobalOptions.TryGetValue("build_property.ProjectDir", out var d) ? d : null);

        var templates = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
            .Select((at, ct) =>
            {
                var text = at.GetText(ct)?.ToString() ?? string.Empty;
                return new TemplateInput(at.Path, PlaceholderScanner.Scan(text));
            });

        var combined = templates.Combine(rootNs.Combine(projDir));

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (tpl, (ns, dir)) = tuple;
            if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(dir)) return;

            var emit = EmitFor(tpl, ns!, dir!);
            if (emit is null) return;

            spc.AddSource(emit.Value.FileName, SourceText.From(emit.Value.Source, Encoding.UTF8));
        });
    }

    private static (string FileName, string Source)? EmitFor(TemplateInput tpl, string rootNs, string projDir)
    {
        var location = TemplateLocation.From(tpl.Path, projDir);
        if (location is null) return null;

        var ns = string.Join(".", new[] { rootNs }.Concat(new[] { "Templates" }).Concat(location.FolderSegments));
        var className = Identifier.Sanitize(location.LeafName);

        var properties = string.Join("\n",
            tpl.Placeholders.Select(p => $"    public required object? {Identifier.Pascal(p)} {{ get; init; }}"));
        var body = properties.Length > 0 ? $"\n{properties}\n" : "\n";

        var source = "#nullable enable\n\n"
            + $"namespace {ns};\n\n"
            + "[global::System.CodeDom.Compiler.GeneratedCode(\"Templar.Generators\", \"1.0.0\")]\n"
            + $"public sealed class {className} : global::Templar.Rendering.Compositor\n"
            + "{" + body + "}\n";

        var fileName = "Templates." + string.Join(".", location.FolderSegments.Concat(new[] { className })) + ".g.cs";
        return (fileName, source);
    }

    private sealed record TemplateInput(string Path, IReadOnlyList<string> Placeholders);

    private sealed class TemplateLocation
    {
        public IReadOnlyList<string> FolderSegments { get; }
        public string LeafName { get; }
        private TemplateLocation(IReadOnlyList<string> folder, string leaf) { FolderSegments = folder; LeafName = leaf; }

        public static TemplateLocation? From(string absolutePath, string projectDir)
        {
            var full = Path.GetFullPath(absolutePath);
            var dir = Path.GetFullPath(projectDir);
            if (!full.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return null;

            var rel = full.Substring(dir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var noExt = rel.Substring(0, rel.Length - 4);
            var parts = noExt.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], "Templates", StringComparison.Ordinal)) return null;

            var folder = parts.Skip(1).Take(parts.Length - 2).ToList();
            return new TemplateLocation(folder, parts[parts.Length - 1]);
        }
    }
}

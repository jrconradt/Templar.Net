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
    public static readonly DiagnosticDescriptor MalformedTemplate = new(
        id: "TMPLR002",
        title: "Malformed .tpl template",
        messageFormat: "{0}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The .tpl file has unbalanced or malformed conditional tags and cannot be compiled into a typed accessor.");

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
                return new TemplateInput(at.Path, text);
            });

        var combined = templates.Combine(rootNs.Combine(projDir));

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (tpl, (ns, dir)) = tuple;
            if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(dir)) return;

            try
            {
                var location = TemplateLocation.From(tpl.Path, dir!);
                if (location is null) return;

                var error = TemplateCompiler.Validate(tpl.Text);
                if (error is not null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MalformedTemplate,
                                                           LocationFor(tpl.Path, error.Value.Line),
                                                           error.Value.Message));
                    return;
                }

                var emit = EmitFor(tpl, ns!, location);
                spc.AddSource(emit.FileName, SourceText.From(emit.Source, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MalformedTemplate,
                                                       LocationFor(tpl.Path, 1),
                                                       ex.Message));
            }
        });
    }

    private static Location LocationFor(string path, int line)
    {
        var zeroBased = line > 0 ? line - 1 : 0;
        var span = new LinePositionSpan(new LinePosition(zeroBased, 0),
                                        new LinePosition(zeroBased, 0));
        return Location.Create(path,
                               new TextSpan(0, 0),
                               span);
    }

    private static (string FileName, string Source) EmitFor(TemplateInput tpl, string rootNs, TemplateLocation location)
    {
        var ns = string.Join(".", new[] { rootNs }.Concat(new[] { "Templates" }).Concat(location.FolderSegments));
        var className = Identifier.Sanitize(location.LeafName);

        var source = TemplateCompiler.EmitClass(ns, className, tpl.Text);

        var fileName = "Templates." + string.Join(".", location.FolderSegments.Concat(new[] { className })) + ".g.cs";
        return (fileName, source);
    }

    private sealed record TemplateInput(string Path, string Text);

    private sealed class TemplateLocation
    {
        public IReadOnlyList<string> FolderSegments { get; }
        public string LeafName { get; }
        private TemplateLocation(IReadOnlyList<string> folder, string leaf) { FolderSegments = folder; LeafName = leaf; }

        public static TemplateLocation? From(string absolutePath, string projectDir)
        {
            var full = Path.GetFullPath(absolutePath);
            var dir = Path.GetFullPath(projectDir);
            var dirWithSep = dir.EndsWith($"{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                          || dir.EndsWith($"{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                ? dir
                : dir + Path.DirectorySeparatorChar;
            if (!full.StartsWith(dirWithSep, StringComparison.OrdinalIgnoreCase)) return null;

            var rel = full.Substring(dirWithSep.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var noExt = rel.Substring(0, rel.Length - 4);
            var parts = noExt.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], "Templates", StringComparison.Ordinal)) return null;

            var folder = parts.Skip(1).Take(parts.Length - 2).ToList();
            return new TemplateLocation(folder, parts[parts.Length - 1]);
        }
    }
}

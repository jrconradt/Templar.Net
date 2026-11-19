using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Templar.Generators;

[Generator]
public sealed class RendererGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settings = context.AnalyzerConfigOptionsProvider.Select(
            (opts, _) => new ProjectSettings(
                opts.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns) ? ns : null,
                opts.GlobalOptions.TryGetValue("build_property.ProjectDir", out var dir) ? dir : null));

        var templates = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
            .Select((at, ct) => new TemplateFile(at.Path, at.GetText(ct)?.ToString() ?? string.Empty));

        var outputs = templates.Collect()
            .Combine(settings)
            .Combine(context.CompilationProvider)
            .SelectMany((input, ct) => TemplateResolver.Resolve(input.Left.Left,
                                                                input.Left.Right,
                                                                input.Right,
                                                                ct))
            .WithTrackingName("Renderers");

        context.RegisterSourceOutput(outputs, (spc, output) =>
        {
            foreach (var finding in output.Findings)
            {
                spc.ReportDiagnostic(Diagnostic.Create(Descriptors.ById(finding.Id),
                                                       LocationFor(finding.Path, finding.Line),
                                                       finding.Arguments.Cast<object>().ToArray()));
            }
            if (output.Source.Length > 0)
            {
                spc.AddSource(output.HintName, SourceText.From(output.Source, Encoding.UTF8));
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
}

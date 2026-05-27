using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Templar.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawTemplateParseAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "TMPLR001",
        title: "Inline Template.Parse content belongs in a .tpl file",
        messageFormat: "Move this template literal into a .tpl file and call it via the generated Templates.* accessor",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Templar.Generators emits typed accessors for .tpl files. Inline string literals passed to Template.Parse bypass that pipeline and become unanalyzable.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member) return;
        if (member.Name.Identifier.Text != "Parse") return;

        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation, ctx.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method) return;
        if (method.ContainingType?.ToDisplayString() != "Templar.Template") return;
        if (invocation.ArgumentList.Arguments.Count == 0) return;

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsStringLiteralLike(arg)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsStringLiteralLike(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression) => true,
        InterpolatedStringExpressionSyntax => true,
        _ => false
    };
}

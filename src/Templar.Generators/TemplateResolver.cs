using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Templar.Generators;

internal static class TemplateResolver
{
    private const string Writer = "global::Templar.Rendering.TemplarWriter";
    private const string Registry = "global::Templar.Rendering.RenderRegistry";

    private static readonly SymbolDisplayFormat DottedName = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    private sealed class Candidate
    {
        public Candidate(TemplateFile file, INamedTypeSymbol type, List<TemplateCompiler.Node> nodes)
        {
            File = file;
            Type = type;
            Nodes = nodes;
        }

        public TemplateFile File { get; }
        public INamedTypeSymbol Type { get; }
        public List<TemplateCompiler.Node> Nodes { get; }
    }

    private sealed class Context
    {
        public Context(Compilation compilation, Dictionary<INamedTypeSymbol, Candidate> mapped)
        {
            Compilation = compilation;
            Mapped = mapped;
            Sequence = compilation.GetTypeByMetadataName("Templar.Rendering.Sequence`1");
            SourceTypes = new Lazy<List<INamedTypeSymbol>>(() => AllSourceTypes(compilation));
        }

        public Compilation Compilation { get; }
        public Dictionary<INamedTypeSymbol, Candidate> Mapped { get; }
        public INamedTypeSymbol? Sequence { get; }
        public Lazy<List<INamedTypeSymbol>> SourceTypes { get; }
    }

    public static ImmutableArray<TemplateOutput> Resolve(ImmutableArray<TemplateFile> files,
                                                         ProjectSettings settings,
                                                         Compilation compilation,
                                                         CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.RootNamespace) || string.IsNullOrEmpty(settings.ProjectDir))
        {
            return ImmutableArray<TemplateOutput>.Empty;
        }

        var outputs = ImmutableArray.CreateBuilder<TemplateOutput>();
        var mapped = new Dictionary<INamedTypeSymbol, Candidate>(SymbolEqualityComparer.Default);
        var context = new Context(compilation, mapped);

        foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();

            var location = TemplateLocation.From(file.Path, settings.ProjectDir!);
            if (location is null)
            {
                outputs.Add(Failed(file, "TMPLR003", 1, file.Path, TemplateLocation.Root(settings.ProjectDir!)));
                continue;
            }

            var error = TemplateCompiler.Validate(file.Text);
            if (error is not null)
            {
                outputs.Add(Failed(file, "TMPLR002", error.Value.Line, error.Value.Message));
                continue;
            }

            var fullName = string.Join(".",
                                       new[] { settings.RootNamespace! }
                                           .Concat(location.FolderSegments.Select(Identifier.Sanitize))
                                           .Concat(new[] { Identifier.Sanitize(location.LeafName) }));

            var type = compilation.Assembly.GetTypeByMetadataName(fullName);
            if (type is null)
            {
                var nearMiss = context.SourceTypes.Value
                    .FirstOrDefault(t => t.ToDisplayString(DottedName) == fullName);
                if (nearMiss is null)
                {
                    outputs.Add(Failed(file, "TMPLR004", 1, file.Path, fullName));
                    continue;
                }
                var reason = nearMiss.ContainingType is not null
                    ? $"it is nested in '{nearMiss.ContainingType.ToDisplayString()}'"
                    : "it is generic";
                outputs.Add(Failed(file, "TMPLR005", 1, nearMiss.ToDisplayString(), file.Path, reason));
                continue;
            }

            var shape = ShapeProblem(type);
            if (shape is null && mapped.TryGetValue(type, out var earlier))
            {
                shape = $"it is already the type of template '{earlier.File.Path}'";
            }
            if (shape is not null)
            {
                outputs.Add(Failed(file, "TMPLR005", 1, type.ToDisplayString(), file.Path, shape));
                continue;
            }

            mapped[type] = new Candidate(file, type, TemplateCompiler.Parse(file.Text));
        }

        foreach (var candidate in mapped.Values.OrderBy(c => c.File.Path, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            outputs.Add(Emit(candidate, context));
        }

        return outputs.ToImmutable();
    }

    private static string? ShapeProblem(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
        {
            return "it is not a class or record";
        }
        if (type.IsStatic)
        {
            return "it is static";
        }
        if (!type.IsSealed)
        {
            return "it is not sealed";
        }
        if (type.IsFileLocal)
        {
            return "it is file-local";
        }
        return null;
    }

    private static TemplateOutput Failed(TemplateFile file, string id, int line, params string[] arguments)
    {
        var finding = new Finding(id, file.Path, line, new EquatableArray<string>(arguments));
        return new TemplateOutput("", "", new EquatableArray<Finding>(new[] { finding }));
    }

    private static TemplateOutput Emit(Candidate candidate, Context context)
    {
        var type = candidate.Type;
        var file = candidate.File;
        var properties = ReadableProperties(type, context.Compilation);
        var findings = new List<Finding>();
        var body = new StringBuilder();

        foreach (var node in candidate.Nodes)
        {
            if (node.Op == TemplateCompiler.Op.Lit)
            {
                body.Append("        w.Literal(\"").Append(TemplateCompiler.EscapeLiteral(node.Text)).Append("\");\n");
                continue;
            }

            var matches = properties
                .Where(p => string.Equals(p.Name, node.Text, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0)
            {
                findings.Add(new Finding("TMPLR006",
                                         file.Path,
                                         node.Line,
                                         new EquatableArray<string>(new[] { node.Text, file.Path, type.ToDisplayString() })));
                continue;
            }
            if (matches.Count > 1)
            {
                findings.Add(new Finding("TMPLR007",
                                         file.Path,
                                         node.Line,
                                         new EquatableArray<string>(new[] { node.Text, file.Path, type.ToDisplayString(), string.Join(", ", matches.Select(p => p.Name)) })));
                continue;
            }

            var property = matches[0];
            var access = "model." + Identifier.Escape(property.Name);
            var write = HoleWrite(property.Type, access, context, out var reason);
            if (write is null)
            {
                findings.Add(new Finding("TMPLR008",
                                         file.Path,
                                         node.Line,
                                         new EquatableArray<string>(new[] { node.Text, file.Path, property.Name, property.Type.ToDisplayString(), reason })));
                continue;
            }
            body.Append("        ").Append(write).Append('\n');
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var accessibility = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var source = "// <auto-generated/>\n"
            + "#nullable enable\n\n"
            + $"namespace {type.ContainingNamespace.ToDisplayString()};\n\n"
            + $"{accessibility} static class {type.Name}Renderer\n"
            + "{\n"
            + $"    public static string Render(this {typeName} model, global::Templar.Rendering.RenderOptions? options = null)\n"
            + "    {\n"
            + $"        return {Writer}.Render(model, Steps, options);\n"
            + "    }\n\n"
            + $"    internal static global::System.Collections.Generic.IEnumerator<object?> Steps({Writer} w, {typeName} model)\n"
            + "    {\n"
            + body
            + "        yield break;\n"
            + "    }\n\n"
            + "    [global::System.Runtime.CompilerServices.ModuleInitializer]\n"
            + "    internal static void Register()\n"
            + "    {\n"
            + $"        {Registry}.Register<{typeName}>(Steps);\n"
            + "    }\n"
            + "}\n";

        var hintName = type.ToDisplayString() + ".g.cs";
        return new TemplateOutput(hintName, source, new EquatableArray<Finding>(findings));
    }

    private static string? HoleWrite(ITypeSymbol type, string access, Context context, out string reason)
    {
        reason = "";
        if (type.SpecialType == SpecialType.System_String)
        {
            return $"w.Text({access});";
        }
        if (IsScalar(type))
        {
            return $"w.Format({access});";
        }
        if (type is INamedTypeSymbol named && context.Mapped.ContainsKey(named))
        {
            return $"yield return w.Compose({access}, {RendererOf(named)}.Steps);";
        }
        if (TryElement(type, context, out var element, out reason))
        {
            var item = ItemSteps(element!, context, 1, out reason);
            if (item is null)
            {
                return null;
            }
            return $"yield return w.List({access}, {item});";
        }
        if (reason.Length > 0)
        {
            return null;
        }
        if (type.TypeKind == TypeKind.Interface)
        {
            if (!AllImplementationsMapped(type, context, out reason))
            {
                return null;
            }
            return $"yield return w.Compose({access}, {Registry}.Steps);";
        }
        reason = "it is not a string, primitive, enum, type with a template, Sequence<T>, IEnumerable<T> or interface";
        return null;
    }

    private static string? ItemSteps(ITypeSymbol element, Context context, int depth, out string reason)
    {
        reason = "";
        if (element.SpecialType == SpecialType.System_String)
        {
            return $"{Writer}.TextItem";
        }
        if (IsScalar(element))
        {
            return $"{Writer}.FormatItem<{element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
        }
        if (element is INamedTypeSymbol named && context.Mapped.ContainsKey(named))
        {
            return $"{RendererOf(named)}.Steps";
        }
        if (TryElement(element, context, out var inner, out reason))
        {
            var innerSteps = ItemSteps(inner!, context, depth + 1, out reason);
            if (innerSteps is null)
            {
                return null;
            }
            return $"(w{depth}, m{depth}) => {Writer}.ListItem(w{depth}, m{depth}, {innerSteps})";
        }
        if (reason.Length > 0)
        {
            reason = $"its items cannot render: {reason}";
            return null;
        }
        if (element.TypeKind == TypeKind.Interface)
        {
            if (!AllImplementationsMapped(element, context, out reason))
            {
                reason = $"its items cannot render: {reason}";
                return null;
            }
            return $"{Registry}.Steps";
        }
        reason = $"its items of type '{element.ToDisplayString()}' are not a string, primitive, enum, type with a template, Sequence<T>, IEnumerable<T> or interface";
        return null;
    }

    private static bool TryElement(ITypeSymbol type, Context context, out ITypeSymbol? element, out string reason)
    {
        element = null;
        reason = "";
        if (context.Sequence is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, context.Sequence))
        {
            element = ((INamedTypeSymbol)type).TypeArguments[0];
            return true;
        }
        if (type is IArrayTypeSymbol array)
        {
            element = array.ElementType;
            return true;
        }
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            element = ((INamedTypeSymbol)type).TypeArguments[0];
            return true;
        }
        var enumerables = type.AllInterfaces
            .Where(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            .ToList();
        if (enumerables.Count == 1)
        {
            element = enumerables[0].TypeArguments[0];
            return true;
        }
        if (enumerables.Count > 1)
        {
            reason = "it implements IEnumerable<T> more than once";
        }
        return false;
    }

    private static bool IsScalar(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = named.TypeArguments[0];
        }
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
                return true;
            default:
                return false;
        }
    }

    private static bool AllImplementationsMapped(ITypeSymbol iface, Context context, out string reason)
    {
        reason = "";
        foreach (var candidate in context.SourceTypes.Value)
        {
            if (candidate.TypeKind == TypeKind.Interface || candidate.IsAbstract)
            {
                continue;
            }
            if (!candidate.AllInterfaces.Contains((INamedTypeSymbol)iface, SymbolEqualityComparer.Default))
            {
                continue;
            }
            if (!context.Mapped.ContainsKey(candidate))
            {
                reason = $"its implementation '{candidate.ToDisplayString()}' has no template";
                return false;
            }
        }
        return true;
    }

    private static string RendererOf(INamedTypeSymbol type)
    {
        return $"global::{type.ContainingNamespace.ToDisplayString()}.{type.Name}Renderer";
    }

    private static List<IPropertySymbol> ReadableProperties(INamedTypeSymbol type, Compilation compilation)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<IPropertySymbol>();
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!seen.Add(property.Name))
                {
                    continue;
                }
                if (property.IsStatic || property.IsIndexer || property.GetMethod is null)
                {
                    continue;
                }
                if (!compilation.IsSymbolAccessibleWithin(property.GetMethod, compilation.Assembly))
                {
                    continue;
                }
                result.Add(property);
            }
        }
        return result;
    }

    private static List<INamedTypeSymbol> AllSourceTypes(Compilation compilation)
    {
        var result = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(compilation.Assembly.GlobalNamespace);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    pending.Push(ns);
                    continue;
                }
                if (member is INamedTypeSymbol type)
                {
                    result.Add(type);
                    pending.Push(type);
                }
            }
        }
        result.Sort((a, b) => string.CompareOrdinal(a.ToDisplayString(), b.ToDisplayString()));
        return result;
    }
}

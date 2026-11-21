using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Templar.Generators;

internal sealed class TemplateResolver
{
    private const string Writer = "global::Templar.Rendering.TemplarWriter";
    private const string InterceptorsHintName = "Templar.Interceptors.g.cs";
    private const string AttributeName = "Templar.Rendering.TemplateAttribute";
    private const string CannotCompose = "it is not a string, primitive, enum, Sequence<T>, IEnumerable<T> or type with [Template]";

    private sealed class Declared
    {
        public Declared(string name, string path, List<TemplateCompiler.Node> nodes, string rootNamespace)
        {
            Name = name;
            Path = path;
            Nodes = nodes;
            var segments = name.Split('/').Select(Identifier.Sanitize).ToList();
            Namespace = string.Join(".",
                                    rootNamespace.Split('.')
                                        .Select(s => Identifier.Escape(Identifier.Sanitize(s)))
                                        .Concat(new[] { "Templates" })
                                        .Concat(segments.Take(segments.Count - 1).Select(Identifier.Escape)));
            ClassName = segments[segments.Count - 1] + "Template";
            HintName = string.Join(".", segments) + ".g.cs";
        }

        public string Name { get; }
        public string Path { get; }
        public List<TemplateCompiler.Node> Nodes { get; }
        public string Namespace { get; }
        public string ClassName { get; }
        public string HintName { get; }
        public string Steps => $"global::{Namespace}.{ClassName}.Steps";
    }

    private sealed class Pair
    {
        public Pair(ITypeSymbol type, Declared template, Site origin)
        {
            Type = type;
            Template = template;
            Origin = origin;
        }

        public ITypeSymbol Type { get; }
        public Declared Template { get; }
        public Site Origin { get; }
        public StringBuilder Body { get; } = new();
        public List<InterceptableLocation> NamedCalls { get; } = new();
        public List<InterceptableLocation> DefaultCalls { get; } = new();
    }

    private readonly Compilation _compilation;
    private readonly string _rootNamespace;
    private readonly CancellationToken _ct;
    private readonly INamedTypeSymbol? _sequence;
    private readonly Dictionary<string, Declared> _templates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pair> _pairs = new(StringComparer.Ordinal);
    private readonly Queue<Pair> _pending = new();
    private readonly List<Finding> _findings = new();
    private List<INamedTypeSymbol>? _sourceTypes;

    private TemplateResolver(Compilation compilation, string rootNamespace, CancellationToken ct)
    {
        _compilation = compilation;
        _rootNamespace = rootNamespace;
        _ct = ct;
        _sequence = compilation.GetTypeByMetadataName("Templar.Rendering.Sequence`1");
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

        var resolver = new TemplateResolver(compilation, settings.RootNamespace!, ct);
        resolver.Declare(files, settings.ProjectDir!);
        resolver.SeedTypes();
        resolver.SeedCallSites();
        while (resolver._pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            resolver.Bind(resolver._pending.Dequeue());
        }
        return resolver.Outputs();
    }

    private void Declare(ImmutableArray<TemplateFile> files, string projectDir)
    {
        foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            _ct.ThrowIfCancellationRequested();

            var name = TemplateLocation.NameOf(file.Path, projectDir);
            if (name is null)
            {
                Report("TMPLR003", Site.Line(file.Path, 1), file.Path, TemplateLocation.Root(projectDir));
                continue;
            }

            var error = TemplateCompiler.Validate(file.Text);
            if (error is not null)
            {
                Report("TMPLR002", Site.Line(file.Path, error.Value.Line), error.Value.Message);
                continue;
            }

            _templates[name] = new Declared(name, file.Path, TemplateCompiler.Parse(file.Text), _rootNamespace);
        }
    }

    private void SeedTypes()
    {
        foreach (var type in SourceTypes())
        {
            var attribute = AttributeOf(type);
            if (attribute is null)
            {
                continue;
            }

            var site = attribute.ApplicationSyntaxReference is { } reference
                ? Site.Of(reference.GetSyntax(_ct).GetLocation())
                : Site.Of(type.Locations[0]);
            var name = NameIn(attribute);
            if (name is null || !_templates.TryGetValue(name, out var declared))
            {
                Report("TMPLR004", site, $"[Template] on '{type.ToDisplayString()}'", name ?? "");
                continue;
            }

            var problem = HostProblem(type);
            if (problem is not null)
            {
                Report("TMPLR008", site, type.ToDisplayString(), "by its [Template]", problem);
                continue;
            }

            Reach(type, declared, site);
        }
    }

    private void SeedCallSites()
    {
        var compositor = _compilation.GetTypeByMetadataName("Templar.Rendering.Compositor");
        if (compositor is null)
        {
            return;
        }

        foreach (var tree in _compilation.SyntaxTrees)
        {
            _ct.ThrowIfCancellationRequested();
            SemanticModel? model = null;
            foreach (var invocation in tree.GetRoot(_ct).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (CalledName(invocation.Expression) != "ResolveTemplate")
                {
                    continue;
                }
                model ??= _compilation.GetSemanticModel(tree);
                if (model.GetSymbolInfo(invocation, _ct).Symbol is not IMethodSymbol method
                    || !SymbolEqualityComparer.Default.Equals(method.ContainingType, compositor)
                    || method.TypeArguments.Length != 1)
                {
                    continue;
                }
                SeedCallSite(invocation, method, model);
            }
        }
    }

    private void SeedCallSite(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel model)
    {
        var type = method.TypeArguments[0];
        if (type.TypeKind == TypeKind.Error)
        {
            return;
        }
        var site = Site.Of(invocation.GetLocation());

        var parameter = method.Parameters.FirstOrDefault(p => p.Name == "template");
        if (parameter is null)
        {
            string? reason = "it has no [Template]";
            if (TemplateOf(type) is not null)
            {
                ComposeSteps(type, out reason);
            }
            if (reason is not null)
            {
                Report("TMPLR008", site, type.ToDisplayString(), "as the model of ResolveTemplate", reason);
                return;
            }
            var defaultName = TemplateOf(type);
            if (defaultName is not null
                && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly)
                && _pairs.TryGetValue(Key(type, defaultName), out var defaultPair))
            {
                Intercept(defaultPair.DefaultCalls, invocation, model);
            }
            return;
        }

        var argument = ArgumentFor(invocation, parameter);
        if (argument is null)
        {
            return;
        }
        var constant = model.GetConstantValue(argument.Expression, _ct);
        if (!constant.HasValue || constant.Value is not string name)
        {
            Report("TMPLR005", Site.Of(argument.GetLocation()));
            return;
        }
        if (!_templates.TryGetValue(name, out var declared))
        {
            Report("TMPLR004", Site.Of(argument.GetLocation()), "ResolveTemplate", name);
            return;
        }

        var problem = HostProblem(type);
        if (problem is not null)
        {
            Report("TMPLR008", site, type.ToDisplayString(), "as the model of ResolveTemplate", problem);
            return;
        }

        Intercept(Reach(type, declared, site).NamedCalls, invocation, model);
    }

    private void Intercept(List<InterceptableLocation> calls, InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var location = model.GetInterceptableLocation(invocation, _ct);
        if (location is not null)
        {
            calls.Add(location);
        }
    }

    private void Bind(Pair pair)
    {
        var properties = ReadableProperties(pair.Type);
        var template = pair.Template;

        foreach (var node in template.Nodes)
        {
            if (node.Op == TemplateCompiler.Op.Lit)
            {
                pair.Body.Append("        w.Literal(\"").Append(TemplateCompiler.EscapeLiteral(node.Text)).Append("\");\n");
                continue;
            }

            var line = node.Line.ToString(CultureInfo.InvariantCulture);
            var matches = properties
                .Where(p => string.Equals(p.Name, node.Text, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0)
            {
                Report("TMPLR006", pair.Origin, node.Text, line, template.Name, pair.Type.ToDisplayString());
                continue;
            }
            if (matches.Count > 1)
            {
                Report("TMPLR007", pair.Origin, node.Text, line, template.Name, pair.Type.ToDisplayString(), string.Join(", ", matches.Select(p => p.Name)));
                continue;
            }

            var property = matches[0];
            var access = "model." + Identifier.Escape(property.Name);
            var write = SpotWrite(property.Type, access, out var reason);
            if (write is null)
            {
                if (reason is not null)
                {
                    Report("TMPLR008",
                           pair.Origin,
                           property.Type.ToDisplayString(),
                           $"at spot '{node.Text}' (line {line} of template '{template.Name}', property '{property.Name}')",
                           reason);
                }
                continue;
            }
            pair.Body.Append("        ").Append(write).Append('\n');
        }
    }

    private string? SpotWrite(ITypeSymbol type, string access, out string? reason)
    {
        reason = null;
        if (type.TypeKind == TypeKind.Error)
        {
            return null;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return $"w.Text({access});";
        }
        if (IsScalar(type))
        {
            return $"w.Format({access});";
        }
        if (TemplateOf(type) is null)
        {
            if (TryElement(type, out var element, out reason))
            {
                var item = ItemSteps(element!, 1, out reason);
                return item is null ? null : $"yield return w.List({access}, {item});";
            }
            if (reason is not null)
            {
                return null;
            }
            reason = CannotCompose;
            return null;
        }

        var steps = ComposeSteps(type, out reason);
        return steps is null ? null : $"yield return w.Compose({access}, {steps});";
    }

    private string? ItemSteps(ITypeSymbol element, int depth, out string? reason)
    {
        reason = null;
        if (element.TypeKind == TypeKind.Error)
        {
            return null;
        }

        if (element.SpecialType == SpecialType.System_String)
        {
            return $"{Writer}.TextItem";
        }
        if (IsScalar(element))
        {
            return $"{Writer}.FormatItem<{Qualified(element)}>";
        }
        if (TemplateOf(element) is null)
        {
            if (TryElement(element, out var inner, out reason))
            {
                var innerSteps = ItemSteps(inner!, depth + 1, out reason);
                if (innerSteps is null)
                {
                    return null;
                }
                return $"(w{depth}, m{depth}) => {Writer}.ListItem(w{depth}, m{depth}, {innerSteps})";
            }
            if (reason is not null)
            {
                reason = $"its items cannot render: {reason}";
                return null;
            }
            reason = $"its items of type '{element.ToDisplayString()}' cannot render: {CannotCompose}";
            return null;
        }

        var steps = ComposeSteps(element, out reason);
        if (steps is null && reason is not null)
        {
            reason = $"its items cannot render: {reason}";
        }
        return steps;
    }

    private string? ComposeSteps(ITypeSymbol type, out string? reason)
    {
        reason = null;
        var name = TemplateOf(type);
        if (name is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
            {
                return _pairs.TryGetValue(Key(type, name), out var pair) ? pair.Template.Steps : null;
            }
            reason = "its [Template] is declared in another assembly";
            return null;
        }

        reason = "it has no [Template]";
        return null;
    }

    private Pair Reach(ITypeSymbol type, Declared template, Site origin)
    {
        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var key = Key(type, template.Name);
        if (!_pairs.TryGetValue(key, out var pair))
        {
            pair = new Pair(type, template, origin);
            _pairs[key] = pair;
            _pending.Enqueue(pair);
        }
        return pair;
    }

    private ImmutableArray<TemplateOutput> Outputs()
    {
        var outputs = ImmutableArray.CreateBuilder<TemplateOutput>();
        var none = new EquatableArray<Finding>(Array.Empty<Finding>());
        foreach (var group in _pairs.Values.GroupBy(p => p.Template).OrderBy(g => g.Key.Name, StringComparer.Ordinal))
        {
            var pairs = group.OrderBy(p => Qualified(p.Type), StringComparer.Ordinal).ToList();
            outputs.Add(new TemplateOutput(group.Key.HintName, Emit(group.Key, pairs), none));
        }
        var interceptors = EmitInterceptors(_pairs.Values
                                                .OrderBy(p => p.Template.Name, StringComparer.Ordinal)
                                                .ThenBy(p => Qualified(p.Type), StringComparer.Ordinal));
        if (interceptors.Length > 0)
        {
            outputs.Add(new TemplateOutput(InterceptorsHintName, interceptors, none));
        }
        if (_findings.Count > 0)
        {
            outputs.Add(new TemplateOutput("", "", new EquatableArray<Finding>(_findings)));
        }
        return outputs.ToImmutable();
    }

    private static string Emit(Declared template, List<Pair> pairs)
    {
        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n")
              .Append("#nullable enable\n\n")
              .Append("namespace ").Append(template.Namespace).Append(";\n\n")
              .Append("internal static class ").Append(template.ClassName).Append('\n')
              .Append("{\n");
        for (var i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                source.Append('\n');
            }
            source.Append("    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(")
                  .Append(Writer).Append(" w, ").Append(Qualified(pairs[i].Type)).Append(" model)\n")
                  .Append("    {\n")
                  .Append(pairs[i].Body)
                  .Append("        yield break;\n")
                  .Append("    }\n");
        }
        source.Append("}\n");
        return source.ToString();
    }

    private static string EmitInterceptors(IEnumerable<Pair> pairs)
    {
        var methods = new StringBuilder();
        var count = 0;
        foreach (var pair in pairs)
        {
            count = AppendInterceptor(methods, count, pair, pair.NamedCalls, "string template, ");
            count = AppendInterceptor(methods, count, pair, pair.DefaultCalls, "");
        }
        if (count == 0)
        {
            return "";
        }

        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n")
              .Append("#nullable enable\n\n")
              .Append("namespace System.Runtime.CompilerServices\n")
              .Append("{\n")
              .Append("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]\n")
              .Append("    file sealed class InterceptsLocationAttribute : global::System.Attribute\n")
              .Append("    {\n")
              .Append("        public InterceptsLocationAttribute(int version, string data)\n")
              .Append("        {\n")
              .Append("        }\n")
              .Append("    }\n")
              .Append("}\n\n")
              .Append("namespace Templar.Generated\n")
              .Append("{\n")
              .Append("    file static class Interceptors\n")
              .Append("    {\n")
              .Append(methods)
              .Append("    }\n")
              .Append("}\n");
        return source.ToString();
    }

    private static int AppendInterceptor(StringBuilder methods, int count, Pair pair, List<InterceptableLocation> calls, string template)
    {
        if (calls.Count == 0)
        {
            return count;
        }
        if (count > 0)
        {
            methods.Append('\n');
        }
        foreach (var call in calls)
        {
            methods.Append("        [global::System.Runtime.CompilerServices.InterceptsLocation(")
                   .Append(call.Version.ToString(CultureInfo.InvariantCulture))
                   .Append(", \"").Append(call.Data).Append("\")]\n");
        }
        methods.Append("        internal static string ResolveTemplate").Append(count.ToString(CultureInfo.InvariantCulture))
               .Append('(').Append(Qualified(pair.Type)).Append(" model, ").Append(template)
               .Append("global::Templar.Rendering.RenderOptions? options = null)\n")
               .Append("        {\n")
               .Append("            return ").Append(Writer).Append(".Render(model, ").Append(pair.Template.Steps).Append(", options);\n")
               .Append("        }\n");
        return count + 1;
    }

    private void Report(string id, Site site, params string[] arguments)
    {
        _findings.Add(new Finding(id, site, new EquatableArray<string>(arguments)));
    }

    private string? HostProblem(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return "it is a type parameter";
        }
        if (type.IsValueType)
        {
            return "it is a value type";
        }
        if (type is not INamedTypeSymbol named)
        {
            return "it is not a class, record or interface";
        }
        if (named.IsAnonymousType)
        {
            return "it is anonymous";
        }
        if (IsOpen(named))
        {
            return "it is generic";
        }
        if (!_compilation.IsSymbolAccessibleWithin(named, _compilation.Assembly))
        {
            return "it is not accessible to generated code";
        }
        return null;
    }

    private static bool IsOpen(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol)
        {
            return true;
        }
        if (type is IArrayTypeSymbol array)
        {
            return IsOpen(array.ElementType);
        }
        if (type is INamedTypeSymbol named)
        {
            return named.TypeArguments.Any(IsOpen) || (named.ContainingType is not null && IsOpen(named.ContainingType));
        }
        return false;
    }

    private static AttributeData? AttributeOf(ITypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == AttributeName)
            {
                return attribute;
            }
        }
        return null;
    }

    private static string? NameIn(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
    }

    private static string? TemplateOf(ITypeSymbol type)
    {
        var attribute = AttributeOf(type);
        return attribute is null ? null : NameIn(attribute) ?? "";
    }

    private static string? CalledName(ExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax member)
        {
            return member.Name.Identifier.ValueText;
        }
        if (expression is MemberBindingExpressionSyntax binding)
        {
            return binding.Name.Identifier.ValueText;
        }
        if (expression is SimpleNameSyntax simple)
        {
            return simple.Identifier.ValueText;
        }
        return null;
    }

    private static ArgumentSyntax? ArgumentFor(InvocationExpressionSyntax invocation, IParameterSymbol parameter)
    {
        var arguments = invocation.ArgumentList.Arguments;
        foreach (var argument in arguments)
        {
            if (argument.NameColon is not null && argument.NameColon.Name.Identifier.ValueText == parameter.Name)
            {
                return argument;
            }
        }
        if (parameter.Ordinal < arguments.Count && arguments[parameter.Ordinal].NameColon is null)
        {
            return arguments[parameter.Ordinal];
        }
        return null;
    }

    private static string Key(ITypeSymbol type, string template)
    {
        return Qualified(type) + "\n" + template;
    }

    private static string Qualified(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private bool TryElement(ITypeSymbol type, out ITypeSymbol? element, out string? reason)
    {
        element = null;
        reason = null;
        if (_sequence is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _sequence))
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

    private List<IPropertySymbol> ReadableProperties(ITypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<IPropertySymbol>();
        foreach (var current in Hierarchy(type))
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
                if (!_compilation.IsSymbolAccessibleWithin(property.GetMethod, _compilation.Assembly))
                {
                    continue;
                }
                result.Add(property);
            }
        }
        return result;
    }

    private static IEnumerable<ITypeSymbol> Hierarchy(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            yield return type;
            foreach (var inherited in type.AllInterfaces)
            {
                yield return inherited;
            }
            yield break;
        }
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            yield return current;
        }
    }

    private List<INamedTypeSymbol> SourceTypes()
    {
        if (_sourceTypes is not null)
        {
            return _sourceTypes;
        }
        var result = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(_compilation.Assembly.GlobalNamespace);
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
        _sourceTypes = result;
        return result;
    }
}

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
    private const string ResolversHintName = "Templar.Resolvers.g.cs";
    private const string TemplatesHintName = "Templar.Templates.g.cs";
    private const string AttributeName = "Templar.Rendering.TemplateAttribute";
    private const string TextAttributeName = "Templar.Rendering.TemplateTextAttribute";
    private const string CannotCompose = "it is not a string, primitive, enum, Sequence<T>, IEnumerable<T> or type with [Template]";

    private sealed class Declared
    {
        public Declared(string name, string path, string text, string rootNamespace, string? assembly = null)
        {
            Name = name;
            Path = path;
            Text = text;
            Nodes = TemplateCompiler.Parse(text);
            var prefix = assembly is null ? Array.Empty<string>() : assembly.Split('.');
            var segments = prefix
                .Concat(name.Split('/'))
                .Select(Identifier.Sanitize)
                .ToList();
            Namespace = string.Join(".",
                                    rootNamespace.Split('.')
                                        .Select(s => Identifier.Escape(Identifier.Sanitize(s)))
                                        .Concat(new[] { "Templates" })
                                        .Concat(segments.Take(segments.Count - 1).Select(Identifier.Escape)));
            var leaf = assembly is null ? segments[segments.Count - 1] : string.Join("_", segments);
            ClassName = leaf + "Template";
            HintName = string.Join(".", segments) + ".g.cs";
        }

        public string Name { get; }
        public string Path { get; }
        public string Text { get; }
        public List<TemplateCompiler.Node> Nodes { get; }
        public string Namespace { get; }
        public string ClassName { get; }
        public string HintName { get; }
        public string Steps => $"global::{Namespace}.{ClassName}.Steps";
    }

    private abstract class Target
    {
        protected Target(ITypeSymbol type)
        {
            Type = type;
        }

        public ITypeSymbol Type { get; }
        public List<InterceptableLocation> DefaultCalls { get; } = new();
        public abstract string Steps { get; }
    }

    private sealed class Pair : Target
    {
        public Pair(ITypeSymbol type, Declared template, Site origin)
            : base(type)
        {
            Template = template;
            Origin = origin;
        }

        public Declared Template { get; }
        public Site Origin { get; }
        public StringBuilder Body { get; } = new();
        public List<InterceptableLocation> NamedCalls { get; } = new();
        public override string Steps => Template.Steps;
    }

    private sealed class Resolver : Target
    {
        public Resolver(ITypeSymbol type, string ns, List<Pair> arms)
            : base(type)
        {
            Namespace = ns;
            ClassName = Identifier.Sanitize(type.ToDisplayString()) + "Resolver";
            Arms = arms;
        }

        public string Namespace { get; }
        public string ClassName { get; }
        public List<Pair> Arms { get; }
        public override string Steps => $"global::{Namespace}.{ClassName}.Steps";
    }

    private readonly Compilation _compilation;
    private readonly string _rootNamespace;
    private readonly string _resolverNamespace;
    private readonly CancellationToken _ct;
    private readonly INamedTypeSymbol? _sequence;
    private readonly Dictionary<string, Declared> _templates = new(StringComparer.Ordinal);
    private readonly HashSet<string> _named = new(StringComparer.Ordinal);
    private readonly Dictionary<IAssemblySymbol, Dictionary<string, Declared>> _carried = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<(string Type, Declared Template), Pair> _pairs = new();
    private readonly Dictionary<string, Resolver> _resolvers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<INamedTypeSymbol>> _candidates = new(StringComparer.Ordinal);
    private readonly Queue<Pair> _pending = new();
    private readonly List<Finding> _findings = new();
    private readonly Dictionary<IAssemblySymbol, List<INamedTypeSymbol>> _sourceTypes = new(SymbolEqualityComparer.Default);

    private TemplateResolver(Compilation compilation, string rootNamespace, CancellationToken ct)
    {
        _compilation = compilation;
        _rootNamespace = rootNamespace;
        _resolverNamespace = string.Join(".",
                                         rootNamespace.Split('.')
                                             .Select(s => Identifier.Escape(Identifier.Sanitize(s)))
                                             .Concat(new[] { "Templates", "Resolvers" }));
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

            _templates[name] = new Declared(name, file.Path, file.Text, _rootNamespace);
        }
    }

    private void SeedTypes()
    {
        foreach (var type in SourceTypes(_compilation.Assembly))
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

            _named.Add(name);

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
            Target? target = null;
            if (Composes(type))
            {
                target = Resolve(type, site, out reason);
            }
            if (reason is not null)
            {
                Report("TMPLR008", site, type.ToDisplayString(), "as the model of ResolveTemplate", reason);
                return;
            }
            if (target is not null)
            {
                Intercept(target.DefaultCalls, invocation, model);
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
            var write = SpotWrite(property.Type, access, pair.Origin, out var reason);
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

    private string? SpotWrite(ITypeSymbol type, string access, Site origin, out string? reason)
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
        if (!Composes(type))
        {
            if (TryElement(type, out var element, out reason))
            {
                var item = ItemSteps(element!, 1, origin, out reason);
                return item is null ? null : $"yield return w.List({access}, {item});";
            }
            if (reason is not null)
            {
                return null;
            }
            reason = CannotCompose;
            return null;
        }

        var target = Resolve(type, origin, out reason);
        return target is null ? null : $"yield return w.Compose({access}, {target.Steps});";
    }

    private string? ItemSteps(ITypeSymbol element, int depth, Site origin, out string? reason)
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
        if (!Composes(element))
        {
            if (TryElement(element, out var inner, out reason))
            {
                var innerSteps = ItemSteps(inner!, depth + 1, origin, out reason);
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

        var target = Resolve(element, origin, out reason);
        if (target is null)
        {
            if (reason is not null)
            {
                reason = $"its items cannot render: {reason}";
            }
            return null;
        }
        return target.Steps;
    }

    private bool Composes(ITypeSymbol type)
    {
        return Owner(type) is not null || Candidates(type).Any(c => Owner(c) is not null);
    }

    private Target? Resolve(ITypeSymbol type, Site origin, out string? reason)
    {
        reason = null;
        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var own = Owner(type);
        var candidates = Candidates(type);
        if (own is not null && candidates.All(c => SymbolEqualityComparer.Default.Equals(Owner(c), own)))
        {
            return PairFor(type, own, origin, out reason);
        }

        if (_resolvers.TryGetValue(Qualified(type), out var resolver))
        {
            return resolver;
        }
        reason = HostProblem(type);
        if (reason is not null)
        {
            return null;
        }
        var arms = new List<Pair>();
        foreach (var candidate in candidates)
        {
            var self = SymbolEqualityComparer.Default.Equals(candidate, type);
            var owner = Owner(candidate);
            if (owner is null)
            {
                reason = self ? "it has no [Template]" : $"its subtype '{candidate.ToDisplayString()}' has no [Template]";
                return null;
            }
            var pair = PairFor(candidate, owner, origin, out var problem);
            if (pair is null)
            {
                reason = problem;
                if (problem is not null && !self)
                {
                    reason = $"its subtype '{candidate.ToDisplayString()}' cannot render: {problem}";
                }
                return null;
            }
            arms.Add(pair);
        }
        resolver = new Resolver(type, _resolverNamespace, arms);
        _resolvers[Qualified(type)] = resolver;
        return resolver;
    }

    private Pair? PairFor(ITypeSymbol type, INamedTypeSymbol owner, Site origin, out string? problem)
    {
        problem = null;
        var name = TemplateOf(owner)!;
        var assembly = owner.ContainingAssembly;
        if (!SymbolEqualityComparer.Default.Equals(assembly, _compilation.Assembly))
        {
            if (!Carried(assembly).TryGetValue(name, out var carried))
            {
                problem = $"its [Template] is declared in assembly '{assembly.Name}', which carries no text for template '{name}'";
                return null;
            }
            problem = HostProblem(type);
            return problem is null ? Reach(type, carried, origin) : null;
        }
        if (!_templates.TryGetValue(name, out var declared))
        {
            return null;
        }
        if (SymbolEqualityComparer.Default.Equals(type, owner))
        {
            return _pairs.TryGetValue(Key(type, declared), out var pair) ? pair : null;
        }
        problem = HostProblem(type);
        return problem is null ? Reach(type, declared, origin) : null;
    }

    private Pair Reach(ITypeSymbol type, Declared template, Site origin)
    {
        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var key = Key(type, template);
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
        var groups = _pairs.Values
            .GroupBy(p => p.Template)
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal)
            .ThenBy(g => g.Key.HintName, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var pairs = group.OrderBy(p => Qualified(p.Type), StringComparer.Ordinal).ToList();
            outputs.Add(new TemplateOutput(group.Key.HintName, Emit(group.Key, pairs), none));
        }
        var resolvers = _resolvers.Values.OrderBy(r => Qualified(r.Type), StringComparer.Ordinal).ToList();
        if (resolvers.Count > 0)
        {
            outputs.Add(new TemplateOutput(ResolversHintName, EmitResolvers(_resolverNamespace, resolvers), none));
        }
        var interceptors = EmitInterceptors(_pairs.Values
                                                .OrderBy(p => p.Template.Name, StringComparer.Ordinal)
                                                .ThenBy(p => p.Template.HintName, StringComparer.Ordinal)
                                                .ThenBy(p => Qualified(p.Type), StringComparer.Ordinal),
                                            resolvers);
        if (interceptors.Length > 0)
        {
            outputs.Add(new TemplateOutput(InterceptorsHintName, interceptors, none));
        }
        var named = _named
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => _templates[n])
            .ToList();
        if (named.Count > 0)
        {
            outputs.Add(new TemplateOutput(TemplatesHintName, EmitTexts(named), none));
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

    private static string EmitResolvers(string ns, List<Resolver> resolvers)
    {
        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n")
              .Append("#nullable enable\n\n")
              .Append("namespace ").Append(ns).Append(";\n\n");
        for (var i = 0; i < resolvers.Count; i++)
        {
            if (i > 0)
            {
                source.Append('\n');
            }
            var resolver = resolvers[i];
            source.Append("internal static class ").Append(resolver.ClassName).Append('\n')
                  .Append("{\n")
                  .Append("    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(")
                  .Append(Writer).Append(" w, ").Append(Qualified(resolver.Type)).Append(" model)\n")
                  .Append("    {\n")
                  .Append("        return model switch\n")
                  .Append("        {\n");
            foreach (var arm in resolver.Arms)
            {
                source.Append("            ").Append(Qualified(arm.Type)).Append(" m => ").Append(arm.Steps).Append("(w, m),\n");
            }
            source.Append("            _ => throw new global::System.InvalidOperationException(\"Type '\" + model.GetType() + \"' was not resolved for '")
                  .Append(TemplateCompiler.EscapeLiteral(resolver.Type.ToDisplayString()))
                  .Append("' at compile time.\"),\n")
                  .Append("        };\n")
                  .Append("    }\n")
                  .Append("}\n");
        }
        return source.ToString();
    }

    private static string EmitInterceptors(IEnumerable<Pair> pairs, IEnumerable<Resolver> resolvers)
    {
        var methods = new StringBuilder();
        var count = 0;
        foreach (var pair in pairs)
        {
            count = AppendInterceptor(methods, count, pair, pair.NamedCalls, "string template, ");
            count = AppendInterceptor(methods, count, pair, pair.DefaultCalls, "");
        }
        foreach (var resolver in resolvers)
        {
            count = AppendInterceptor(methods, count, resolver, resolver.DefaultCalls, "");
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

    private static int AppendInterceptor(StringBuilder methods, int count, Target target, List<InterceptableLocation> calls, string template)
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
               .Append('(').Append(Qualified(target.Type)).Append(" model, ").Append(template)
               .Append("global::Templar.Rendering.RenderOptions? options = null)\n")
               .Append("        {\n")
               .Append("            return ").Append(Writer).Append(".Render(model, ").Append(target.Steps).Append(", options);\n")
               .Append("        }\n");
        return count + 1;
    }

    private static string EmitTexts(List<Declared> templates)
    {
        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n")
              .Append("#nullable enable\n\n");
        foreach (var template in templates)
        {
            source.Append("[assembly: global::Templar.Rendering.TemplateTextAttribute(\"")
                  .Append(TemplateCompiler.EscapeLiteral(template.Name))
                  .Append("\", \"")
                  .Append(TemplateCompiler.EscapeLiteral(template.Text))
                  .Append("\")]\n");
        }
        return source.ToString();
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

    private Dictionary<string, Declared> Carried(IAssemblySymbol assembly)
    {
        if (_carried.TryGetValue(assembly, out var cached))
        {
            return cached;
        }
        var result = new Dictionary<string, Declared>(StringComparer.Ordinal);
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != TextAttributeName
                || attribute.ConstructorArguments.Length != 2
                || attribute.ConstructorArguments[0].Value is not string name
                || attribute.ConstructorArguments[1].Value is not string text)
            {
                continue;
            }
            result[name] = new Declared(name, "", text, _rootNamespace, assembly.Name);
        }
        _carried[assembly] = result;
        return result;
    }

    private static INamedTypeSymbol? Owner(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return null;
        }
        for (INamedTypeSymbol? current = named; current is not null; current = current.BaseType)
        {
            if (AttributeOf(current) is not null)
            {
                return current;
            }
        }
        foreach (var inherited in named.AllInterfaces)
        {
            if (AttributeOf(inherited) is not null)
            {
                return inherited;
            }
        }
        return null;
    }

    private List<INamedTypeSymbol> Candidates(ITypeSymbol type)
    {
        var key = Qualified(type);
        if (_candidates.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var result = new List<INamedTypeSymbol>();
        if (type.WithNullableAnnotation(NullableAnnotation.NotAnnotated) is INamedTypeSymbol named)
        {
            if (IsConcrete(named))
            {
                result.Add(named);
            }
            if (named.ContainingAssembly is { } assembly)
            {
                var here = SymbolEqualityComparer.Default.Equals(assembly, _compilation.Assembly);
                var carries = !here && Carried(assembly).Count > 0;
                if (here || carries)
                {
                    var subtypes = SourceTypes(_compilation.Assembly)
                        .Where(t => IsConcrete(t) && Derives(t, named));
                    result.AddRange(subtypes);
                }
                if (carries)
                {
                    var subtypes = SourceTypes(assembly)
                        .Where(t => IsConcrete(t) && Derives(t, named));
                    result.AddRange(subtypes);
                }
            }
        }
        result.Sort(MostDerivedFirst);
        _candidates[key] = result;
        return result;
    }

    private static bool IsConcrete(INamedTypeSymbol type)
    {
        return (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && !type.IsAbstract && !type.IsStatic;
    }

    private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol from)
    {
        if (from.TypeKind == TypeKind.Interface)
        {
            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, from));
        }
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, from))
            {
                return true;
            }
        }
        return false;
    }

    private static int MostDerivedFirst(INamedTypeSymbol a, INamedTypeSymbol b)
    {
        var depth = Depth(b) - Depth(a);
        if (depth != 0)
        {
            return depth;
        }
        return string.CompareOrdinal(Qualified(a), Qualified(b));
    }

    private static int Depth(INamedTypeSymbol type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }
        return depth;
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

    private static (string Type, Declared Template) Key(ITypeSymbol type, Declared template)
    {
        return (Qualified(type), template);
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

    private List<INamedTypeSymbol> SourceTypes(IAssemblySymbol assembly)
    {
        if (_sourceTypes.TryGetValue(assembly, out var cached))
        {
            return cached;
        }
        var result = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(assembly.GlobalNamespace);
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
        _sourceTypes[assembly] = result;
        return result;
    }
}

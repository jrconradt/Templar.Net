using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Templar.Rendering;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
public abstract class Compositor
{
    private static readonly ConditionalWeakTable<Type, BindablePropertyInfo[]> _bindCache = new();
    private static readonly ConditionalWeakTable<Type, string> _structureCache = new();

    private RenderOptions _options = new();

    protected virtual string Structure => _structureCache.GetValue(GetType(), LoadStructureFromConvention);

    protected virtual void Populate(Template template)
    {
        var key = GetType();
        if (!_bindCache.TryGetValue(key, out var bindables))
        {
            bindables = BuildBindables(key);
            _bindCache.AddOrUpdate(key, bindables);
        }
        foreach (var b in bindables)
        {
            template[b.VariableName] = b.Property.GetValue(this);
        }
    }

    public Compositor WithOptions(RenderOptions options)
    {
        _options = options;
        return this;
    }

    public virtual string Render()
    {
        Compile(out var source,
                out var values,
                out var filters,
                out var options);
        return Renderer.Render(source,
                               values,
                               filters,
                               options);
    }

    protected virtual void Validate()
    {
    }

    protected virtual RenderOptions ResolveOptions() => _options;

    internal virtual void Compile(out string source,
                                  out IDictionary<string, object?> values,
                                  out FilterRegistry filters,
                                  out RenderOptions options)
    {
        Validate();
        source = Structure;
        var template = Template.Parse(source).WithOptions(ResolveOptions());
        Populate(template);
        values = template.ValuesInternal;
        filters = template.FiltersInternal;
        options = template.OptionsInternal;
    }

    public override string ToString() => Render();

    private static string LoadStructureFromConvention(Type type)
    {
        var current = type;
        while (current is not null && current != typeof(Compositor) && current != typeof(object))
        {
            var resourceName = (current.FullName ?? current.Name).Replace('+', '.') + ".tpl";
            var stream = current.Assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using (stream)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            current = current.BaseType;
        }
        throw new InvalidOperationException(
            $"Compositor '{type.FullName}' did not override Structure and no embedded template resource was found in the inheritance chain.");
    }

    private static BindablePropertyInfo[] BuildBindables(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        var list = new List<BindablePropertyInfo>();
        foreach (var prop in type.GetProperties(flags))
        {
            if (!prop.CanRead)
            {
                continue;
            }
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }
            if (prop.GetCustomAttribute<TemplateIgnoreAttribute>() is not null)
            {
                continue;
            }
            if (prop.Name == nameof(Structure))
            {
                continue;
            }

            var name = prop.GetCustomAttribute<TemplateBindAttribute>()?.Name ?? prop.Name;
            list.Add(new BindablePropertyInfo(prop, name));
        }
        return list.ToArray();
    }

    private readonly record struct BindablePropertyInfo(PropertyInfo Property, string VariableName);
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class TemplateBindAttribute : Attribute
{
    public string Name { get; }
    public TemplateBindAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class TemplateIgnoreAttribute : Attribute
{
}

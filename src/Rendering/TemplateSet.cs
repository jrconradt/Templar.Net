using System.Reflection;

namespace Templar.Rendering;

public sealed class TemplateSet
{
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly FilterRegistry _filters = new();
    private RenderOptions _options = new();

    public TemplateSet Add(string name, string text)
    {
        _sources[name] = text;
        return this;
    }

    public TemplateSet AddDirectory(string path, string extension = ".tpl")
    {
        foreach (var file in Directory.GetFiles(path, $"*{extension}"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var text = File.ReadAllText(file);
            _sources[name] = text;
        }
        return this;
    }

    public TemplateSet AddEmbeddedResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var name = resourceName;
        var lastDot = name.LastIndexOf('.');
        if (lastDot > 0)
        {
            var secondLastDot = name.LastIndexOf('.', lastDot - 1);
            if (secondLastDot > 0)
                name = name[(secondLastDot + 1)..lastDot];
            else
                name = name[..lastDot];
        }

        _sources[name] = text;
        return this;
    }

    public TemplateSet AddEmbeddedTemplates(Assembly assembly, string prefix, string extension = ".tpl")
    {
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix, StringComparison.Ordinal) ||
                !resourceName.EndsWith(extension, StringComparison.Ordinal))
            {
                continue;
            }

            var keyStart = prefix.Length;
            var keyLength = resourceName.Length - prefix.Length - extension.Length;
            var key = resourceName.Substring(keyStart, keyLength);

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new ArgumentException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'");
            
            using var reader = new StreamReader(stream);
            _sources[key] = reader.ReadToEnd();
        }
        return this;
    }

    public TemplateSet AddFilter(string name, Func<object?, string> filter)
    {
        _filters.Add(name, filter);
        return this;
    }

    public TemplateSet WithOptions(RenderOptions options)
    {
        _options = options;
        return this;
    }

    public bool Contains(string name) => _sources.ContainsKey(name);

    public Template Get(string name)
    {
        if (!_sources.TryGetValue(name, out var source))
        {
            throw new KeyNotFoundException($"Template '{name}' not found in set");
        }
        
        var template = Template.Parse(source);

        foreach (var filter in _filters.All)
        {
            template.AddFilter(filter.Key, filter.Value);
        }

        return template.WithOptions(_options);
    }
}

namespace Templar.Rendering;

internal sealed class FilterRegistry
{
    private readonly Dictionary<string, Func<object?, string>> _filters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["upper"] = v => v?.ToString()?.ToUpperInvariant() ?? "",
        ["lower"] = v => v?.ToString()?.ToLowerInvariant() ?? "",
        ["pascal"] = v => ToPascalCase(v?.ToString() ?? ""),
        ["camel"] = v => ToCamelCase(v?.ToString() ?? ""),
    };

    internal void Add(string name, Func<object?, string> filter)
    {
        _filters[name] = filter;
    }

    internal string Apply(string filterName, object? value, string? variableName = null)
    {
        if (_filters.TryGetValue(filterName, out var filter))
            return filter(value);

        throw new TemplateRenderException(
            $"Unknown filter '{filterName}'" + (variableName is null ? "" : $" on variable '{variableName}'"),
            filterName,
            variableName);
    }

    internal IReadOnlyDictionary<string, Func<object?, string>> All => _filters;

    internal FilterRegistry Clone()
    {
        var clone = new FilterRegistry();
        foreach (var kvp in _filters)
            clone._filters[kvp.Key] = kvp.Value;
        return clone;
    }

    private static string ToPascalCase(string input)
    {
        if (input.Length == 0) return input;

        var parts = input.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return input;

        return string.Concat(parts.Select(Capitalize));
    }

    private static string ToCamelCase(string input)
    {
        var pascal = ToPascalCase(input);
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string Capitalize(string s)
    {
        if (s.Length == 0) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}

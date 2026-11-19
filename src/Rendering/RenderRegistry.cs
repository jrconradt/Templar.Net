using System.Collections.Concurrent;

namespace Templar.Rendering;

public static class RenderRegistry
{
    private static readonly ConcurrentDictionary<(Type Type, string Template), Func<TemplarWriter, object, IEnumerator<object?>>> Renderers = new();
    private static readonly ConcurrentDictionary<Type, string> Defaults = new();

    public static void Register<T>(string template, Func<TemplarWriter, T, IEnumerator<object?>> steps) where T : class
    {
        Renderers[(typeof(T), template)] = (writer, model) => steps(writer, (T)model);
    }

    public static void RegisterDefault<T>(string template) where T : class
    {
        Defaults[typeof(T)] = template;
    }

    public static Func<TemplarWriter, T, IEnumerator<object?>> Find<T>(string template) where T : class
    {
        var steps = Lookup(typeof(T), template);
        return (writer, model) => steps(writer, model);
    }

    public static IEnumerator<object?> Steps(TemplarWriter writer, object model)
    {
        var type = model.GetType();
        if (!Defaults.TryGetValue(type, out var template))
        {
            throw new InvalidOperationException($"No template is registered for type '{type.FullName}'.");
        }
        return Lookup(type, template)(writer, model);
    }

    internal static Func<TemplarWriter, T, IEnumerator<object?>> Default<T>() where T : class
    {
        if (Defaults.TryGetValue(typeof(T), out var template))
        {
            return Find<T>(template);
        }
        return Steps;
    }

    private static Func<TemplarWriter, object, IEnumerator<object?>> Lookup(Type type, string template)
    {
        if (!Renderers.TryGetValue((type, template), out var steps))
        {
            throw new InvalidOperationException($"No template '{template}' is registered for type '{type.FullName}'.");
        }
        return steps;
    }
}

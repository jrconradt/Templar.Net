using System.Collections.Concurrent;

namespace Templar.Rendering;

public static class RenderRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<TemplarWriter, object, IEnumerator<object?>>> Renderers = new();

    public static void Register<T>(Func<TemplarWriter, T, IEnumerator<object?>> steps) where T : class
    {
        Renderers[typeof(T)] = (writer, model) => steps(writer, (T)model);
    }

    public static IEnumerator<object?> Steps(TemplarWriter writer, object model)
    {
        var type = model.GetType();
        if (!Renderers.TryGetValue(type, out var steps))
        {
            throw new InvalidOperationException($"No template is registered for type '{type.FullName}'.");
        }
        return steps(writer, model);
    }
}

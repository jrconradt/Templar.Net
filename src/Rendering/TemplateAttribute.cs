namespace Templar.Rendering;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class TemplateAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

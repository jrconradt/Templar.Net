using Microsoft.CodeAnalysis;

namespace Templar.Generators;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor MalformedTemplate = new(
        id: "TMPLR002",
        title: "Malformed .tpl template",
        messageFormat: "{0}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The .tpl file has an unclosed tag or an unsupported tag marker and cannot be compiled.");

    public static readonly DiagnosticDescriptor OutsideTemplates = new(
        id: "TMPLR003",
        title: "Template outside the Templates folder",
        messageFormat: "Template '{0}' is outside the Templates folder '{1}', so no type can be resolved for it",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Templates live under <ProjectDir>/Templates/: a .tpl's path after Templates/ is its type's namespace path below RootNamespace. A .tpl outside Templates/ has no namespace path.");

    public static readonly DiagnosticDescriptor NoType = new(
        id: "TMPLR004",
        title: "Template with no type",
        messageFormat: "Template '{0}' maps to type '{1}', which is not declared in this project",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A .tpl under Templates/ is the template of the type named RootNamespace + its folder path after Templates/ + its file name; declare that type in the project.");

    public static readonly DiagnosticDescriptor TypeShape = new(
        id: "TMPLR005",
        title: "Template type is not a sealed, non-generic, top-level class",
        messageFormat: "Type '{0}' of template '{1}' must be a sealed, non-generic, top-level class or record: {2}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type with a template is a sealed, non-generic, top-level class or record.");

    public static readonly DiagnosticDescriptor NoProperty = new(
        id: "TMPLR006",
        title: "Hole with no property",
        messageFormat: "Hole '{0}' in template '{1}' matches no readable instance property of '{2}'",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each hole names an accessible, readable instance property of the template's type, matched case-insensitively.");

    public static readonly DiagnosticDescriptor AmbiguousProperty = new(
        id: "TMPLR007",
        title: "Hole matches more than one property",
        messageFormat: "Hole '{0}' in template '{1}' matches more than one property of '{2}': {3}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Holes match properties case-insensitively; two properties differing only in case cannot both be holes.");

    public static readonly DiagnosticDescriptor CannotRender = new(
        id: "TMPLR008",
        title: "Hole whose type cannot render",
        messageFormat: "Hole '{0}' in template '{1}' is property '{2}' of type '{3}', which cannot render: {4}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A hole's type is a string, a primitive, an enum, a type with a template, a Sequence<T> or IEnumerable<T> of one of these, or an interface whose implementations all have templates.");

    public static DiagnosticDescriptor ById(string id)
    {
        return id switch
        {
            "TMPLR002" => MalformedTemplate,
            "TMPLR003" => OutsideTemplates,
            "TMPLR004" => NoType,
            "TMPLR005" => TypeShape,
            "TMPLR006" => NoProperty,
            "TMPLR007" => AmbiguousProperty,
            "TMPLR008" => CannotRender,
            _ => throw new System.ArgumentOutOfRangeException(nameof(id), id, "Unknown Templar diagnostic id"),
        };
    }
}

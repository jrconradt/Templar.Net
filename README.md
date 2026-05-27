# Templar

An indentation-aware template engine for C# code generation. Templar's key feature: when a multi-line value is injected into a template, continuation lines automatically inherit the column position of the placeholder — generated code stays correctly formatted at any nesting depth.

## The Problem

Standard template engines break indentation when substituting multi-line values:

```
// Template:          // Output (broken):
class Foo             class Foo
{                     {
    {{ body }}            public void A()
}                     {
                          // ...
                      }
                      }
```

## Templar's Solution

Continuation lines are padded to the placeholder's column:

```
// Template:          // Output (correct):
class Foo             class Foo
{                     {
    {{ body }}            public void A()
}                         {
                              // ...
                          }
                      }
```

This works at arbitrary nesting depths and composes — inner templates render into outer templates with indentation preserved across boundaries.

## Quick Start

Given a template file `Templates/Calculator.tpl`:

```
public static class {{ name }}
{
    {{ body }}
}
```

Load it, fill it, render it:

```csharp
using Templar.Rendering;

var set = new TemplateSet().AddDirectory("Templates");
var template = set.Get("Calculator");

template["name"] = "Calculator";
template["body"] = "public int Add(int a, int b)\n{\n    return a + b;\n}";

Console.WriteLine(template.Render());
```

The multi-line `body` is injected at the `{{ body }}` column, so the rendered method body lines up one indent inside the class — no manual padding.

## Syntax

| Form | Example | Effect |
|------|---------|--------|
| Variable | `{{ name }}` | Replaced with value (empty if unset) |
| Filter | `{{ name \| pascal }}` | Value transformed by filter |
| Comment | `{{# ignored }}` | Stripped entirely from output |
| Conditional | `{{? flag }}…{{?}}` | Body rendered when `flag` is truthy |
| Else branch | `{{? flag }}…{{?else}}…{{?}}` | Second arm rendered when `flag` is falsy |
| Negation | `{{?!flag }}…{{?}}` | Body rendered when `flag` is falsy |
| Escape | `\{{` · `\}}` · `\\` | Literal `{{`, `}}`, `\` |

Variable and filter names are case-insensitive. A conditional tests one variable by name — `null`, `""`, `false`, and an empty `IEnumerable` are falsy; everything else is truthy. Conditional tags are inline: keep `{{? }}`…`{{?}}` on the same line as the text they guard. A line containing only value placeholders that all render empty is dropped entirely, trailing newline included.

### Built-in Filters

| Filter | Input | Output |
|--------|-------|--------|
| `upper` | `hello world` | `HELLO WORLD` |
| `lower` | `Hello World` | `hello world` |
| `pascal` | `hello_world` | `HelloWorld` |
| `camel` | `hello_world` | `helloWorld` |

Custom filters: `template.AddFilter("myFilter", val => ...)`.

## Composition

The `Compositor` base class lets a class **be** a template: define `Structure` (the template text) and Templar auto-binds every readable instance property to a template variable matching the property name. The built-in `CSharpFile` preset provides a five-section structure for generating `.cs` files: header, pragmas, usings, file-scoped namespace, and body.

```csharp
using Templar.Presets;

var file = new CSharpFile
{
    Namespace = "MyApp.Generated",
    Usings    = new[] { "System", "System.Linq" },
    Body      = "public sealed class Foo;"
};

string output = file.Render();
```

See [_docs/composition.md](_docs/composition.md) for the full pattern, including `[TemplateBind]`, `[TemplateIgnore]`, inheritance, and overriding `Populate`.

### Sequences

A `Sequence` is a `Compositor` whose `Items` are themselves compositors, rendered and joined by a separator with the placeholder's indentation preserved on every line of every item. Three are built in — `Lines` (newline), `BlankLines` (blank line between items), and `CommaList` (`, `) — and any plain `IEnumerable<Compositor>` value is joined by newline. `CSharpFile` uses one internally: its `UsingsBlock` is a `Lines` over `Using` compositors.

## Render Options

`RenderOptions`, applied via `template.WithOptions(...)` or `TemplateSet.WithOptions(...)`, controls rendering:

| Option | Default | Effect |
|--------|---------|--------|
| `IndentString` | four spaces | Unit of indentation for nested substitution |
| `Newline` | `\n` | Output line ending; injected `\r\n` / `\r` are normalized to it |
| `StrictUndefined` | `false` | When `true`, rendering a variable that was never set throws `TemplateRenderException` instead of emitting empty |

Templar is trim- and AOT-safe (`IsTrimmable` / `IsAotCompatible`), so it can be embedded in published, trimmed, or NativeAOT code generators.

## Documentation

Long-form docs live in [`_docs/`](_docs/):

| Page | Covers |
|------|--------|
| [syntax.md](_docs/syntax.md) | Placeholder, filter, comment, and indentation rules. |
| [templates.md](_docs/templates.md) | Constructing, filling, and rendering a single template. |
| [composition.md](_docs/composition.md) | `Compositor`, `Sequence`, attributes, the `CSharpFile` preset. |
| [integration.md](_docs/integration.md) | `TemplateSet`, embedded resources, filter/options propagation, generators. |

## Build & Test

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). Zero external dependencies.

```bash
dotnet build Templar.slnx
dotnet test Templar.slnx
```

## License

Apache 2.0 — see [LICENSE](LICENSE).

# Templar

[![CI](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) [![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

Templates are views; your objects are their view models. A template names the properties it uses, any object that carries them can fill it, and a source generator checks every pairing and compiles it at build time. Multi-line values inherit the placeholder's column. Trim/AOT-safe, zero dependencies.

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

This works at arbitrary nesting depths and composes — a value rendered into a placeholder keeps that placeholder's column, however deep the nesting.

## Quick Start

Install from NuGet:

```bash
dotnet add package Templar.Net
```

List your templates for the generator — one line in the `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates/**/*.tpl" />
</ItemGroup>
```

A template is any text under `Templates/`; its name is its path there without `.tpl`. Each `{{ spot }}` names a property:

`Templates/Orders/Requisite.tpl`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

`Templates/Orders/Line.tpl`:

```
line {{ sku }}
    qty {{ quantity }}
```

The objects are whatever you already have. A type that holds a part of the output names that part's template with `[Template]`:

```csharp
using Templar.Rendering;

[Template("Orders/Line")]
public sealed record Line(string Sku, int Quantity);

public sealed record Requisite(string Name, Sequence<Line> Lines);
```

Resolve the root:

```csharp
var requisite = new Requisite("R-1", Sequence.Lines([new Line("A-100", 2), new Line("B-200", 5)]));

Console.WriteLine(Compositor.ResolveTemplate(requisite, "Orders/Requisite"));
```

```
requisite R-1
{
    line A-100
        qty 2
    line B-200
        qty 5
}
```

At build time the generator sees that `Requisite` is rendered by `Orders/Requisite`, binds `{{ name }}` and `{{ lines }}` to its properties, follows `Sequence<Line>` to `Line`'s `[Template]`, and compiles one flat renderer per (type, template) pair it reached, with the template text baked in. The call is replaced in place by one that runs that renderer. Nothing reads a `.tpl` at run time, and a template nothing reaches is not compiled in. A missing property, a template name that isn't declared, or a value that cannot render is a compile error at the call site, the attribute, or the spot.

The template is the contract: it names the properties it uses and does not change for the object. Any type that carries them can fill it, and each such type gets its own renderer.

## Syntax

| Form | Example | Effect |
|------|---------|--------|
| Spot | `{{ name }}` | The property's value, written by its declared type |
| Comment | `{{# ignored }}` | Stripped entirely from output |
| Escape | `\{{` · `\}}` · `\\` | Literal `{{`, `}}`, `\` |

Property names are case-insensitive. A line containing only spots that all render empty is dropped entirely, trailing newline included.

## Composition

| A spot whose property is | Writes |
|--------------------------|--------|
| `string` | the text |
| `bool`, a number, `char`, an enum (or nullable) | invariant-formatted text |
| a class or interface with `[Template]` in this project | that template, by the property's declared type; the type must carry what it uses |
| `Sequence<T>` | each item by `T`'s write, joined by the sequence's separator |
| `IEnumerable<T>` | each item by `T`'s write, one per line |
| `null` or empty | nothing; a line left blank is dropped |

Anything else is a compile error. A string value is never rescanned for spots. The generated renderers hold no conditional and the engine never recurses.

See [_docs/composition.md](_docs/composition.md) for the model, and [_docs/integration.md](_docs/integration.md) for names, wiring, what the generator emits, and its diagnostics.

## Render Options

`RenderOptions`, passed to `Compositor.ResolveTemplate(model, options)` or `Compositor.ResolveTemplate(model, "Name", options)`, controls rendering:

| Option | Default | Effect |
|--------|---------|--------|
| `IndentString` | four spaces | Unit of indentation for nested substitution |
| `Newline` | `\n` | Output line ending; injected `\r\n` / `\r` and list separators are normalized to it |

Templar is trim- and AOT-safe (`IsTrimmable` / `IsAotCompatible`), so it can be embedded in published, trimmed, or NativeAOT code generators — including Roslyn source generators ([_docs/integration.md](_docs/integration.md#inside-a-source-generator)).

## Documentation

Long-form docs live in [`_docs/`](_docs/):

| Page | Covers |
|------|--------|
| [composition.md](_docs/composition.md) | The model: templates are views, objects their view models; how each spot is written, lists, the engine. |
| [integration.md](_docs/integration.md) | Template names; wiring; the pairs the generator compiles and what it emits; diagnostics; Templar inside a source generator; exceptions. |
| [syntax.md](_docs/syntax.md) | Spot, comment, escape, and indentation rules. |

## Build & Test

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). Zero external dependencies.

```bash
dotnet build Templar.slnx
dotnet test Templar.slnx
```

## Status

Active development; the core rendering API is stable.

## License

Apache-2.0. Copyright 2026 Infalligence Labs LLC — see [LICENSE](LICENSE).

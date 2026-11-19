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

`Templates/Orders/Stocked.tpl` is `stocked {{ sku }}`; `Templates/Orders/Backordered.tpl`:

```
backordered {{ sku }}
    eta {{ eta }}
```

The objects are whatever you already have. A type may name the template it renders by default with `[Template]`:

```csharp
using Templar.Rendering;

public interface ILine
{
    string Sku { get; }
}

[Template("Orders/Stocked")]
public sealed record Stocked(string Sku) : ILine;

[Template("Orders/Backordered")]
public sealed record Backordered(string Sku, string Eta) : ILine;

public sealed record Requisite(string Name, Sequence<ILine> Lines);
```

Resolve the root:

```csharp
var requisite = new Requisite("R-1", Sequence.Lines<ILine>([new Stocked("A-100"), new Backordered("B-200", "May")]));

Console.WriteLine(Compositor.ResolveTemplate(requisite, "Orders/Requisite"));
```

```
requisite R-1
{
    stocked A-100
    backordered B-200
        eta May
}
```

At build time the generator sees that `Requisite` is rendered by `Orders/Requisite`, binds `{{ name }}` and `{{ lines }}` to its properties, follows `Sequence<ILine>` to every `ILine` implementation's `[Template]`, and compiles one flat renderer per (type, template) pair it reached, with the template text baked in. Nothing reads a `.tpl` at run time, and a template nothing reaches is not compiled in. A missing property, a template name that isn't declared, or a value that cannot render is a compile error at the call site, the attribute, or the spot.

The same template renders any type that carries what it uses, and a spot can name the template its value renders by:

```
public {{ type }}({{ parameters : Injection/Parameter }})
```

## Syntax

| Form | Example | Effect |
|------|---------|--------|
| Spot | `{{ name }}` | The property's value, written by its declared type |
| Spot naming a template | `{{ parameters : Injection/Parameter }}` | The value (or each item) rendered by that template |
| Comment | `{{# ignored }}` | Stripped entirely from output |
| Escape | `\{{` · `\}}` · `\\` | Literal `{{`, `}}`, `\` |

Property names are case-insensitive. A line containing only spots that all render empty is dropped entirely, trailing newline included.

## Composition

| A spot whose property is | Writes |
|--------------------------|--------|
| `string` | the text |
| `bool`, a number, `char`, an enum (or nullable) | invariant-formatted text |
| a type with `[Template]` | that template |
| anything, with `{{ x : T }}` | template `T`; the value's type must carry what `T` uses |
| `Sequence<T>` | each item by `T`'s write (or `: T`), joined by the sequence's separator |
| `IEnumerable<T>` | each item, one per line |
| an interface or abstract class | the value's runtime type's `[Template]`; every implementation must have one |
| `null` or empty | nothing; a line left blank is dropped |

Anything else is a compile error. A string value is never rescanned for spots. The generated renderers hold no conditional and the engine never recurses.

The `CSharpFile` preset (`[Template("Presets/CSharpFile")]`) renders a `.cs` file's header, pragmas, usings, file-scoped namespace and body: `Compositor.ResolveTemplate(file)`.

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
| [composition.md](_docs/composition.md) | The model: templates are views, objects their view models; how each spot is written, variants, lists, the engine, the `CSharpFile` preset. |
| [integration.md](_docs/integration.md) | Template names; wiring; the pairs the generator compiles and what it emits; diagnostics; Templar inside a source generator; exceptions. |
| [syntax.md](_docs/syntax.md) | Spot, comment, escape, and indentation rules. |

## Build & Test

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). Zero external dependencies.

```bash
dotnet build Templar.slnx
dotnet test Templar.slnx
```

## Status

Active development; the core rendering API is stable, while the preset surface is still evolving.

## License

Apache-2.0. Copyright 2026 Infalligence Labs LLC — see [LICENSE](LICENSE).

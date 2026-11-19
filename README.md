# Templar

[![CI](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) [![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

Type-resolved template composition for C# codegen — a plain type is its template, values compose by their own type, multi-line values inherit the placeholder's column, and a source generator checks it all at compile time. Trim/AOT-safe, zero dependencies.

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
  <AdditionalFiles Include="**/*.tpl" />
</ItemGroup>
```

Model what you generate as types. Each type is a plain sealed class or record with its template beside it, named for the type; its properties are the template's holes. In project `Shop`:

```
Orders/
  Requisite.cs    Requisite.tpl
  ILine.cs
  Stocked.cs      Stocked.tpl
  Backordered.cs  Backordered.tpl
```

```csharp
using Templar.Rendering;

namespace Shop.Orders;

public sealed class Requisite
{
    public required string Name { get; init; }
    public required Sequence<ILine> Lines { get; init; }
}

public interface ILine
{
}

public sealed class Stocked : ILine
{
    public required string Sku { get; init; }
}

public sealed class Backordered : ILine
{
    public required string Sku { get; init; }
    public required string Eta { get; init; }
}
```

`Requisite.tpl`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

`Stocked.tpl` is `stocked {{ sku }}`; `Backordered.tpl`:

```
backordered {{ sku }}
    eta {{ eta }}
```

Build the model, render the root:

```csharp
var requisite = new Requisite
{
    Name  = "R-1",
    Lines = Sequence.Lines<ILine>([new Stocked { Sku = "A-100" }, new Backordered { Sku = "B-200", Eta = "May" }]),
};

Console.WriteLine(requisite.Render());
```

```
requisite R-1
{
    stocked A-100
    backordered B-200
        eta May
}
```

The caller selects no template and fills no hole. At build time the generator finds each template's type by name (`Orders/Stocked.tpl` → `Shop.Orders.Stocked`), binds each hole to the property of the same name, and compiles the template into that type's `Render()`, choosing each hole's write from the property's declared type. Each line renders by its own type — `Stocked` and `Backordered` each by their own template at the `{{ lines }}` column. An empty list renders nothing and its line is dropped. A missing type, a hole with no property, or a property that cannot render is a compile error.

## Syntax

| Form | Example | Effect |
|------|---------|--------|
| Variable | `{{ name }}` | Replaced with value (empty if unset) |
| Comment | `{{# ignored }}` | Stripped entirely from output |
| Escape | `\{{` · `\}}` · `\\` | Literal `{{`, `}}`, `\` |

Variable names are case-insensitive. A line containing only value placeholders that all render empty is dropped entirely, trailing newline included.

## Composition

| You have | You write |
|----------|-----------|
| A thing to generate | A sealed class or record, with `<TypeName>.tpl` beside it |
| A hole in its template | A property named for the hole |
| A variant of a thing | An interface, with one sealed type per variant, each with its own `.tpl` |
| Many of a thing | A `Sequence<T>` or an `IEnumerable<T>` |

Each hole is written by its property's declared type: a `string` is text; a primitive or enum is formatted text; a type with a template renders by that template at the hole's column; a `Sequence<T>` renders each item by its own type, joined by the sequence's separator (`Sequence.Lines`, `Sequence.BlankLines`, `Sequence.CommaList`, or `new Sequence<T>(items, separator)`); any other `IEnumerable<T>` renders one item per line; an interface renders the value by its runtime type's template. `null` or an empty list renders nothing. A string value is never rescanned for placeholders. The generated renderers hold no conditional and the engine never recurses.

The `CSharpFile` preset renders a `.cs` file's header, pragmas, usings, file-scoped namespace, and body; its `Body` is a `string`, placed as text.

See [_docs/composition.md](_docs/composition.md) for the model, and [_docs/integration.md](_docs/integration.md) for how a template finds its type, what the generator emits, and its diagnostics.

## Templates without a type

`Template` and `TemplateSet` are the lower-level, string-keyed API: parse or load a template by name and fill its holes by key.

```csharp
var t = new TemplateSet().AddDirectory("Templates").Get("Calculator");
t["name"] = "Calculator";
t["body"] = "public int Add(int a, int b)\n{\n    return a + b;\n}";
Console.WriteLine(t.Render());
```

See [_docs/templates.md](_docs/templates.md).

## Render Options

`RenderOptions`, passed to a type's `Render(options)` or applied via `WithOptions(...)` on a `Template` or `TemplateSet`, controls rendering:

| Option | Default | Effect |
|--------|---------|--------|
| `IndentString` | four spaces | Unit of indentation for nested substitution |
| `Newline` | `\n` | Output line ending; injected `\r\n` / `\r` and list separators are normalized to it |

Templar is trim- and AOT-safe (`IsTrimmable` / `IsAotCompatible`), so it can be embedded in published, trimmed, or NativeAOT code generators — including Roslyn source generators ([_docs/integration.md](_docs/integration.md#inside-a-source-generator)).

## Documentation

Long-form docs live in [`_docs/`](_docs/):

| Page | Covers |
|------|--------|
| [composition.md](_docs/composition.md) | The model: a type is its template, how each hole is written, variants, lists, the engine, the `CSharpFile` preset. |
| [integration.md](_docs/integration.md) | How a template finds its type; wiring; the generated code; diagnostics; Templar inside a source generator; `TemplateSet`; exceptions. |
| [syntax.md](_docs/syntax.md) | Placeholder, comment, and indentation rules. |
| [templates.md](_docs/templates.md) | The string-keyed `Template` API. |

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

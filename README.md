# Templar

[![CI](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/jrconradt/Templar.Net/actions/workflows/ci.yml) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) [![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

Type-resolved template composition for C# codegen — a type is its template, values compose by their own type, multi-line values inherit the placeholder's column. Trim/AOT-safe, zero dependencies.

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

Model what you generate as types. Each type is a `Compositor` with its template beside it, named for the type; its properties are the template's holes. In project `Shop`:

```
Orders/
  Requisite.cs    Requisite.tpl
  Line.cs
  Stocked.cs      Stocked.tpl
  Backordered.cs  Backordered.tpl
```

```csharp
using Templar.Rendering;

namespace Shop.Orders;

public sealed class Requisite : Compositor
{
    public required string Name { get; init; }
    public required IReadOnlyList<Line> Lines { get; init; }
}

public abstract class Line : Compositor
{
    public required string Sku { get; init; }
}

public sealed class Stocked : Line
{
}

public sealed class Backordered : Line
{
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

Embed the templates — one line in the `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="**/*.tpl" />
</ItemGroup>
```

Build the model, render the root:

```csharp
var requisite = new Requisite
{
    Name  = "R-1",
    Lines = [new Stocked { Sku = "A-100" }, new Backordered { Sku = "B-200", Eta = "May" }],
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

The caller selects no template and fills no hole. Each type's template is found by its `FullName` (`Shop.Orders.Stocked` → resource `Shop.Orders.Stocked.tpl`, the SDK's default name for `Orders/Stocked.tpl` when the namespace follows the folder); each property binds to the hole of the same name; each value composes by its own type — `Stocked` and `Backordered` each by their own template at the `{{ lines }}` column. An empty list renders nothing and its line is dropped.

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
| A thing to generate | A `Compositor` subclass, with `<TypeName>.tpl` beside it |
| A hole in its template | A property named for the hole |
| A variant of a thing | A subtype, with its own `.tpl` |
| Many of a thing | A list of the base type |

Values compose by their runtime type: a `string` is text; an `IComposable` (`Compositor`, `Sequence`) renders by its own type's template at the hole's column; an `IEnumerable<IComposable>` renders each item by its own type's template, one per line; `null` or an empty list renders nothing. A string value is never rescanned for placeholders.

A `Sequence` joins composed values by another separator — `Sequence.CommaList`, `Sequence.BlankLines`, or `new Sequence(items, separator)`. The `CSharpFile` preset renders a `.cs` file's header, pragmas, usings, file-scoped namespace, and body; its `Body` is a `string`, placed as text.

See [_docs/composition.md](_docs/composition.md) for the model, and [_docs/integration.md](_docs/integration.md) for how a template is found for a type.

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

`RenderOptions`, applied via `WithOptions(...)` on a `Compositor`, `Template`, or `TemplateSet`, controls rendering:

| Option | Default | Effect |
|--------|---------|--------|
| `IndentString` | four spaces | Unit of indentation for nested substitution |
| `Newline` | `\n` | Output line ending; injected `\r\n` / `\r` are normalized to it |

Templar is trim- and AOT-safe (`IsTrimmable` / `IsAotCompatible`), so it can be embedded in published, trimmed, or NativeAOT code generators.

## Documentation

Long-form docs live in [`_docs/`](_docs/):

| Page | Covers |
|------|--------|
| [composition.md](_docs/composition.md) | The model: a type is its template, values compose by type, variants, lists, the `CSharpFile` preset. |
| [integration.md](_docs/integration.md) | How a template is found for a type; the `.tpl` accessor generator and its limits; `TemplateSet`; exceptions. |
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

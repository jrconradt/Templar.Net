# Composition

A type is its template. A plain, sealed class or record `X` with `X.tpl` under
`Templates/`, at `X`'s namespace path below the project's root namespace, is
rendered by that template; `X`'s `.cs` lives wherever the code lives. Each hole
names a property of `X`, and the Templar generator compiles the template into
`X`'s renderer at build time, choosing each hole's write from the property's
declared type. You model what you generate
as types, build the model, and call `Render()` on the root. The caller never
selects a template, never fills a hole, never counts, and never assembles code as
strings; the generated code holds no conditional.

For placeholder rules see [syntax.md](syntax.md); for how a template finds its type
and how the generator is wired see [integration.md](integration.md); for the
string-keyed `Template` API underneath see [templates.md](templates.md).

## The model

| You have                | You write                                                                      |
|-------------------------|--------------------------------------------------------------------------------|
| A thing to generate     | A sealed class or record, with `Templates/<namespace path>/<TypeName>.tpl`     |
| A hole in its template  | A property named for the hole                                                  |
| A variant of a thing    | An interface, implemented by one sealed type per variant, each with its `.tpl` |
| Many of a thing         | A `Sequence<T>` (its separator travels with it) or any `IEnumerable<T>`        |
| Nothing there           | `null` or an empty list — renders nothing; a line left blank is dropped        |

No base class, no attribute, no `partial`: the type is plain.

## The smallest example

In a project whose root namespace is `Shop`, the types sit in `Orders/` and their
templates in `Templates/Orders/`:

```csharp
// Orders/Requisite.cs
using Templar.Rendering;

namespace Shop.Orders;

public sealed class Requisite
{
    public required string Name { get; init; }
    public required Sequence<ILine> Lines { get; init; }
}
```

`Templates/Orders/Requisite.tpl`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

The variants of a line implement one interface, each with its own template:

```csharp
// Orders/ILine.cs
public interface ILine
{
}

// Orders/Stocked.cs — template Templates/Orders/Stocked.tpl: "stocked {{ sku }}"
public sealed class Stocked : ILine
{
    public required string Sku { get; init; }
}

// Orders/Backordered.cs — template Templates/Orders/Backordered.tpl:
//   "backordered {{ sku }}\n    eta {{ eta }}"
public sealed class Backordered : ILine
{
    public required string Sku { get; init; }
    public required string Eta { get; init; }
}
```

Build the model, render the root:

```csharp
var requisite = new Requisite
{
    Name  = "R-1",
    Lines = Sequence.Lines<ILine>([new Stocked { Sku = "A-100" }, new Backordered { Sku = "B-200", Eta = "May" }]),
};

requisite.Render();
```

```
requisite R-1
{
    stocked A-100
    backordered B-200
        eta May
}
```

With an empty list the `{{ lines }}` line renders empty and is dropped, leaving
`requisite R-1\n{\n}`. Nothing at the call site picks `Stocked.tpl` over
`Backordered.tpl`, writes `template["lines"]`, or joins strings: the type of each
value decides.

## How a hole is written

The generator matches each hole to an accessible, readable instance property of
the type, case-insensitively (`{{ name }}` → `Name`), and picks the write from the
property's **declared** type, once, at compile time:

| Declared type                                    | Write                                                                  |
|--------------------------------------------------|------------------------------------------------------------------------|
| `string`                                         | Text; continuation lines take the hole's column. Never rescanned — a `{{ x }}` inside a value is output as written. |
| `bool`, `char`, a numeric type, an enum, or one of these nullable | Text, formatted with the invariant culture (`ToString()` for `bool` and `char`). |
| A type with a template                           | That type's renderer, named in the generated code, at the hole's column. |
| `Sequence<T>`                                    | Each item by `T`'s write, joined by the sequence's separator, at the hole's column. |
| `IEnumerable<T>` (a list, an array, …)           | Each item by `T`'s write, one per line, at the hole's column.          |
| An interface whose implementations all have templates | The renderer registered for the value's runtime type.             |

A `null` value, or an empty list, writes nothing. Anything else — `object`, an
abstract class, a type with no template — is a compile-time error (`TMPLR008`).
A property no hole names is simply not rendered.

A line whose holes all rendered empty and that holds only whitespace is dropped
with its newline ([syntax.md](syntax.md)).

## Variants are types

A choice between shapes is a choice of type, made when the model is built — not a
condition at render time. Type the hole as an interface and give each implementing
type its own `.tpl`. Every implementation in the project must have a template, or
the hole is reported (`TMPLR008`). Each generated renderer registers itself for its
type when its assembly loads; an interface-typed hole looks up the value's runtime
type there. That lookup is the only decision made at render time, and a value whose
type has no registered renderer throws `InvalidOperationException` naming the type.

## Lists

A list is a type with the list template. `Sequence<T>` holds the items and the
separator:

| Factory                          | Separator  |
|----------------------------------|------------|
| `Sequence.Lines(items)`          | newline    |
| `Sequence.BlankLines(items)`     | blank line |
| `Sequence.CommaList(items)`      | `", "`     |
| `new Sequence<T>(items, sep)`    | `sep`      |

Each item renders by its own type's write; a `\n` in the separator becomes
`RenderOptions.Newline`. A property typed as any other `IEnumerable<T>` renders one
item per line. Lists nest: a `Sequence<Sequence<Item>>` renders each inner list by
its own separator.

```csharp
public required IReadOnlyList<Parameter> Parameters { get; init; }

public Sequence<Parameter> Arguments => Sequence.CommaList(Parameters);   // {{ arguments }}
```

## Surface

For a type `X` with `X.tpl`, the generator emits, in `X`'s namespace:

| Member                                                         | Role                                                       |
|----------------------------------------------------------------|------------------------------------------------------------|
| `XRenderer.Render(this X model, RenderOptions? options = null)` | The render entry: `model.Render()` renders `model` and everything composed into it, to a string. |
| `XRenderer.Steps(TemplarWriter w, X model)` (internal)          | The flat renderer: one `w.Literal(…)` per run of text, one write per hole. |
| `XRenderer.Register()` (module initializer, internal)           | Registers `Steps` for `X`, for interface-typed holes.       |

`XRenderer` has the type's accessibility (public or internal). See
[integration.md](integration.md#what-it-generates) for a real emission.

## The engine

Rendering never recurses. A renderer is an iterator: it writes literal text and
text holes straight into the `TemplarWriter`, and yields a frame for each nested
type or list. One loop (`Renderer.Drive`) runs an explicit stack of frames, pushing
the hole's column as indentation for each yielded frame and popping it when the
frame finishes, so a model nested thousands deep renders without growing the call
stack.

## Worked preset — `CSharpFile`

`Templar.Presets.CSharpFile` is a sealed plain type for `.cs` files with a
**five-section** template, `src/Templates/Presets/CSharpFile.tpl` (the type itself
is in `src/Presets/`):

```
{{ header }}
{{ pragmas }}
{{ usingsBlock }}

namespace {{ namespace }};

{{ body }}
```

### Properties

| Property        | Hole               | Default                                              | Notes                                          |
|-----------------|--------------------|------------------------------------------------------|------------------------------------------------|
| `Header`        | `{{ header }}`     | `#nullable enable`                                   | The library emits no comment banner of its own. |
| `Pragmas`       | `{{ pragmas }}`    | `""`                                                 | Optional, e.g. `#pragma warning disable CS8618`.|
| `Usings`        | *(no hole)*        | empty                                                | `IEnumerable<string>` of using **names**, no `;`. |
| `UsingsBlock`   | `{{ usingsBlock }}`| `Sequence.Lines(Usings.Select(u => new Using { Name = u }))` | A `Sequence<Using>`; each name renders by `Using.tpl` (`using {{ name }};`). |
| `Namespace`     | `{{ namespace }}`  | `""`                                                 | File-scoped namespace, required for valid output.|
| `Body`          | `{{ body }}`       | `""`                                                 | A `string`: placed as text, not composed by type. |

### Usage

```csharp
using Templar.Presets;

var file = new CSharpFile
{
    Namespace = "Acme.Generated",
    Usings    = new[] { "System", "System.Linq" },
    Body      = "public sealed class Foo;"
};

string source = file.Render();
```

`Body` is text, so a placeholder inside it is not substituted. To compose a model
type as the body of a file, give the file its own type and template — the preset is
sealed, so a file type of your own carries its own holes:

```csharp
public sealed class RecordFile
{
    public required string Namespace { get; init; }
    public required Record Record { get; init; }
}
```

`RecordFile.tpl`, under `Templates/` at `RecordFile`'s namespace path:

```
#nullable enable

namespace {{ namespace }};

{{ record }}
```

## Pattern checklist

A type-resolved model:

- One sealed class or record per thing generated, its `.cs` wherever the code lives, its template at `Templates/<namespace path below the root namespace>/<TypeName>.tpl`.
- One readable property per hole — `required` where the hole must be filled.
- Variants as sealed implementations of an interface, each with its own `.tpl`; holes typed as the interface.
- Collections as `Sequence<T>` (separator chosen when the model is built) or `IEnumerable<T>`.
- No base class, no string keys, no `if`/`switch` choosing a template.
- Property getters free of side effects — the renderer reads each once per hole.
- Render the root: `model.Render()`.

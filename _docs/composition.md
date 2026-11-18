# Composition

A type is its template. A `Compositor` subclass finds its template by its own
type's `FullName`, binds each property to the hole of the same name, and
composes each value by that value's own type. You model what you generate as
types, build the model, and render the root. The caller never selects a
template, never fills a hole, never counts, and never assembles code as strings.

For placeholder rules see [syntax.md](syntax.md); for how a type's template is
found and embedded see [integration.md](integration.md#finding-a-template-for-a-type);
for the string-keyed `Template` API underneath see [templates.md](templates.md).

## The model

| You have                | You write                                                                  |
|-------------------------|----------------------------------------------------------------------------|
| A thing to generate     | A `Compositor` subclass, with `<TypeName>.tpl` beside it                    |
| A hole in its template  | A property named for the hole                                              |
| A variant of a thing    | A subtype, with its own `.tpl`                                              |
| Many of a thing         | A list of the base type — `IReadOnlyList<Line>`                             |
| Nothing there           | An empty list or `null` — renders nothing; a line left blank is dropped     |

## The smallest example

In a project whose root namespace is `Shop`, each type sits beside its template
in `Orders/`:

```csharp
// Orders/Requisite.cs
using Templar.Rendering;

namespace Shop.Orders;

public sealed class Requisite : Compositor
{
    public required string Name { get; init; }
    public required IReadOnlyList<Line> Lines { get; init; }
}
```

`Orders/Requisite.tpl`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

The variants of `Line` are subtypes, each with its own template:

```csharp
// Orders/Line.cs
public abstract class Line : Compositor
{
    public required string Sku { get; init; }
}

// Orders/Stocked.cs — template Orders/Stocked.tpl: "stocked {{ sku }}"
public sealed class Stocked : Line
{
}

// Orders/Backordered.cs — template Orders/Backordered.tpl:
//   "backordered {{ sku }}\n    eta {{ eta }}"
public sealed class Backordered : Line
{
    public required string Eta { get; init; }
}
```

Build the model, render the root:

```csharp
var requisite = new Requisite
{
    Name  = "R-1",
    Lines = [new Stocked { Sku = "A-100" }, new Backordered { Sku = "B-200", Eta = "May" }],
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

With `Lines = []` the `{{ lines }}` line renders empty and is dropped, leaving
`requisite R-1\n{\n}`. Nothing at the call site picks `Stocked.tpl` over
`Backordered.tpl`, writes `template["lines"]`, or joins strings: the type of
each value decides.

## How a type finds its template

`Structure` (protected virtual) defaults to the embedded resource named
`<FullName>.tpl` in the type's own assembly — `Shop.Orders.Stocked.tpl` for
`Shop.Orders.Stocked` (a nested type's `+` becomes `.`). If the type has none,
its base type's is tried, and so on up to `Compositor`, so a subtype without a
template of its own renders by its nearest ancestor's. The resolved text is
cached per type; nothing found throws `InvalidOperationException`.

The SDK's default resource name for `Orders/Stocked.tpl` in project `Shop` is
`Shop.Orders.Stocked.tpl`, so a template beside its type is found whenever the
namespace follows the folder; the `.csproj` needs one line. See
[integration.md](integration.md#finding-a-template-for-a-type).

## How values compose

Each bound value is placed at its hole and dispatched on its runtime type:

| Value                                    | Composes as                                                                 |
|------------------------------------------|-----------------------------------------------------------------------------|
| `null`                                   | Nothing.                                                                    |
| `string`                                 | Text; continuation lines take the hole's column. Never rescanned — a `{{ x }}` inside a value is output as written. |
| `IComposable` (`Compositor`, `Sequence`) | Rendered by its own type's template at the hole's column.                   |
| `IEnumerable<IComposable>`               | Each item by its own type's template, one per line, at the hole's column. Empty renders nothing. |
| `IEnumerable<string>`                    | Joined by the configured newline, as text.                                  |
| Anything else                            | `ToString()`, as text.                                                      |

A line whose holes all rendered empty and that holds only whitespace is dropped
with its newline ([syntax.md](syntax.md)).

## Variants are subtypes

A choice between shapes is a choice of type, made when the model is built — not
a condition at render time. Type the hole as the base (`Line`,
`IReadOnlyList<Line>`) and give each subtype its own `.tpl`. A subtype that adds
data but keeps its parent's shape needs no `.tpl` of its own; it renders by the
nearest base type's template with its own properties bound.

## Lists of types

A property typed as a list of a base type composes each item by its own type,
one per line. To join with something other than a newline, expose the list as
a `Sequence` — `Sequence.CommaList`, `Sequence.BlankLines`, or
`new Sequence(items, separator)`:

```csharp
[TemplateIgnore]
public required IReadOnlyList<Parameter> Parameters { get; init; }

public Sequence Arguments => Sequence.CommaList(Parameters);   // {{ arguments }}
```

## Surface

| Member                                        | Role                                                                                |
|-----------------------------------------------|-------------------------------------------------------------------------------------|
| `protected virtual string Structure`          | Template text. Default: the embedded `<FullName>.tpl`, walking base types; cached per type. Override only for [inline text](#inline-structure). |
| `protected virtual void Populate(Template t)` | Default: bind every readable instance property by name.                             |
| `WithOptions(RenderOptions options)`          | Fluent — applies render options for this compositor.                                |
| `Render()`                                    | Renders this value and everything composed into it, to a string.                   |
| `void RenderInto(TemplarWriter)`              | The `IComposable` composition primitive — writes this compositor into a shared sink. `Render()` seeds a sink, calls this, and returns the result. |

`Compositor` implements `IComposable`, the engine's composition primitive: any
value that writes itself into a `TemplarWriter` composes at a hole with
indentation preserved.

## Auto-binding rules

When `Populate` runs (on every `Render`), the default implementation writes
each bindable property into the template under its name. The scan happens
**once per concrete type**; the resulting `PropertyInfo[]` is cached on the type.

A property is **bindable** when all of these hold:

- It is readable (`CanRead`).
- It is an instance property (public **or** non-public, inherited too).
- It has no indexer parameters.
- It is not marked `[TemplateIgnore]`.
- It is not `Structure` itself.

The bound variable name is the property name, **unless** `[TemplateBind("…")]`
overrides it. All lookup is case-insensitive (`Name` → `{{ name }}`).

### Renaming a binding

Use `[TemplateBind]` when the property name and the hole name must differ —
e.g. when the template uses a noun that conflicts with a C# keyword or
stylistic convention.

```csharp
// Letter.tpl: "{{ greeting }} {{ recipient }},"
public sealed class Letter : Compositor
{
    [TemplateBind("greeting")]
    public string Salutation { get; init; } = "Dear";

    public string Recipient { get; init; } = "";
}
```

### Skipping a property

Use `[TemplateIgnore]` for computed helpers and inputs folded into other
bindings.

```csharp
// Sum.tpl: "{{ display }}"
public sealed class Sum : Compositor
{
    public IEnumerable<int> Inputs { get; init; } = Array.Empty<int>();

    [TemplateIgnore]
    public int Total => Inputs.Sum();                   // helper, not bound

    public string Display => $"sum = {Total}";          // bound as {{ display }}
}
```

### Inheritance

Inherited properties bind too (`BindingFlags.FlattenHierarchy`). A subtype with
its own `.tpl` renders by it; one without renders by its nearest base type's.

### Extending Populate

`Populate` is an escape hatch for a value that cannot be a property. It is
string-keyed; prefer a property. Call `base.Populate(template)` first to keep
the auto-bind layer.

```csharp
protected override void Populate(Template template)
{
    base.Populate(template);
    template["timestamp"] = DateTime.UtcNow.ToString("O");
}
```

## Inline structure

Overriding `Structure` puts the template text in code instead of a `.tpl`
beside the type, bypassing the resource lookup. It suits one-line templates and
tests.

```csharp
public sealed class Greeting : Compositor
{
    public string Name { get; init; } = "";
    protected override string Structure => "Hello, {{ name }}!";
}

new Greeting { Name = "World" }.Render();   // → "Hello, World!"
```

## How caching works

Two `ConditionalWeakTable` caches are keyed by `GetType()`:

| Cache              | Computed once per type from                                       | Used in    |
|--------------------|-------------------------------------------------------------------|------------|
| `_structureCache`  | Resolve `Structure` — the embedded `.tpl` resource lookup         | `Render`   |
| `_bindCache`       | Reflect bindable instance properties                              | `Populate` |

So the second and later instances of a type skip both the template lookup and
the reflection walk.

A third cache lives in `Template`: a process-wide set of source strings that
have already passed validation, keyed by the source text. A template validated
once is never re-scanned for syntax errors, so repeated renders pay only the
property reads and the render walk.

## Worked preset — `CSharpFile`

`Templar.Presets.CSharpFile` is a `Compositor` for `.cs` files with a
**five-section** template, `CSharpFile.tpl` beside it:

```
{{ header }}
{{ pragmas }}
{{ usingsBlock }}

namespace {{ namespace }};

{{ body }}
```

### Properties

| Property        | Auto-bound as      | Default                                              | Notes                                          |
|-----------------|--------------------|------------------------------------------------------|------------------------------------------------|
| `Header`        | `{{ header }}`     | `#nullable enable`                                   | `virtual`, override to customize. The library emits no comment banner of its own. |
| `Pragmas`       | `{{ pragmas }}`    | `""`                                                 | Optional, e.g. `#pragma warning disable CS8618`.|
| `Usings`        | *(not bound)*      | empty                                                | `IEnumerable<string>` of using **names**, no `;`. Marked `[TemplateIgnore]`. |
| `UsingsBlock`   | `{{ usingsBlock }}`| `Sequence.Lines(Usings.Select(u => new Using { Name = u }))` — a `Sequence` of `Using` compositors | Each name composes as a `Using`, by `Using.tpl`. |
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

`Body` is text, so a placeholder inside it is not substituted. To compose a
model type as the body, give the file its own type and template: a subclass
with its own `.tpl` renders by it (the inheritance walk finds it first) and
keeps the inherited `Header`, `Pragmas`, `UsingsBlock`, and `Namespace`
bindings.

```csharp
public sealed class RecordFile : CSharpFile
{
    public required Record Record { get; init; }
}
```

`RecordFile.tpl`, beside `RecordFile.cs`:

```
{{ header }}
{{ pragmas }}
{{ usingsBlock }}

namespace {{ namespace }};

{{ record }}
```

## Pattern checklist

A type-resolved model:

- One `Compositor` subclass per thing generated, its `<TypeName>.tpl` beside it.
- One `{ get; init; }` property per hole — `required` where the hole must be filled.
- Variants as subtypes, each with its own `.tpl`; holes typed as the base type.
- Collections as lists of the base type.
- No `Structure` override, no `Populate` override, no string keys, no `if`/`switch` choosing a template.
- Property getters free of side effects — `Populate` reads them once per render.
- Render the root: `model.Render()`.

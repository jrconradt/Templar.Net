# Composition

A `Compositor` is a class that **is** a template. You define the template text
once as a `Structure` property; the public properties of your subclass
auto-bind into the template's variables by name. Render hundreds of files of
the same shape with zero string-keyed boilerplate and zero re-parsing.

For low-level placeholder rules see [syntax.md](syntax.md); for the
`Template`-based equivalent see [templates.md](templates.md); for filter and
loading patterns see [integration.md](integration.md).

## The smallest example

```csharp
using Templar.Rendering;

public sealed class Greeting : Compositor
{
    public string Verb { get; init; } = "Hello";
    public string Name { get; init; } = "";
    protected override string Structure => "{{ verb }}, {{ name }}!";
}

new Greeting { Name = "World" }.Render();   // → "Hello, World!"
```

Two ingredients:

1. **`Structure`** (`protected virtual string`) — the template text. Either
   override the property, or place an embedded `.tpl` resource whose logical
   name matches the type's `FullName` and let the default implementation load
   it. The resolved structure string is cached per concrete type.
2. **Auto-bound properties** — every readable instance property on the subclass
   is exposed to the template under its own name (case-insensitive). Set them
   with the object-initializer pattern and call `Render()`.

## Surface

| Member                                | Role                                                                                |
|---------------------------------------|-------------------------------------------------------------------------------------|
| `protected virtual string Structure`  | Template text. Override, or place an embedded `.tpl` resource matching the type's `FullName`. |
| `protected virtual void Populate(Template t)` | Default: auto-bind every readable instance property by name. Override to extend.|
| `WithOptions(RenderOptions options)`  | Fluent — applies render options for this compositor.                                |
| `Render()`                            | Resolves `Structure` (cached) and renders to a string.                              |
| `void RenderInto(TemplarWriter)`      | The `IComposable` composition primitive — writes this compositor into a shared sink. `Render()` seeds a sink, calls this, and returns the result. |

`Compositor` implements `IComposable`, the engine's composition primitive: any
value that knows how to write itself into a `TemplarWriter` can be injected as a
template variable and is rendered structurally (with indentation preserved). The
`.tpl` accessor generator emits this method directly — see
[integration.md](integration.md#the-tpl-accessor-generator) — so a generated
accessor compiles its template to code instead of carrying a `Structure` string.

## Auto-binding rules

When `Populate` runs (which it does on every `Render`), the default
implementation iterates the subclass's bindable properties and writes each one
into the template under its name. The scan happens **once per concrete type**;
the resulting `PropertyInfo[]` is cached on the type.

A property is **bindable** when all of these hold:

- It is readable (`CanRead`).
- It is an instance property (public **or** non-public, inherited too).
- It has no indexer parameters.
- It is not marked `[TemplateIgnore]`.
- It is not `Structure` itself.

The bound variable name is the property name, **unless** `[TemplateBind("…")]`
overrides it. All lookup is case-insensitive (`Name` → `{{ name }}`).

### Renaming a binding

Use `[TemplateBind]` when the property name and the variable name should
differ — e.g. when the template uses a noun that conflicts with a C# keyword
or stylistic convention.

```csharp
public sealed class Letter : Compositor
{
    [TemplateBind("greeting")]
    public string Salutation { get; init; } = "Dear";

    public string Recipient { get; init; } = "";

    protected override string Structure => "{{ greeting }} {{ recipient }},";
}
```

### Skipping a property

Use `[TemplateIgnore]` for computed helpers, internal state, or properties
whose value type should not appear in rendered output.

```csharp
public sealed class Sum : Compositor
{
    public IEnumerable<int> Inputs { get; init; } = Array.Empty<int>();

    [TemplateIgnore]
    public int Total => Inputs.Sum();                   // helper, not bound

    public string Display => $"sum = {Total}";          // bound as {{ display }}

    protected override string Structure => "{{ display }}";
}
```

### Inheritance

Inherited properties are picked up (`BindingFlags.FlattenHierarchy`). A
subclass can reuse a base `Structure` and add new properties to bind, or
override `Structure` to swap the template entirely.

### Extending Populate

Override `Populate` to add bindings the default scan can't produce — anything
keyed by string, or computed from external state. Call `base.Populate(template)`
first to keep the auto-bind layer.

```csharp
protected override void Populate(Template template)
{
    base.Populate(template);
    template["timestamp"] = DateTime.UtcNow.ToString("O");
}
```

## How caching works

Two `ConditionalWeakTable` caches are keyed by `GetType()`:

| Cache              | Computed once per type from                                       | Used in    |
|--------------------|-------------------------------------------------------------------|------------|
| `_structureCache`  | Resolve `Structure` — e.g. embedded `.tpl` resource lookup        | `Render`   |
| `_bindCache`       | Reflect bindable instance properties                              | `Populate` |

So the second and later instances of a given `Compositor` subclass skip both
the structure resolution and the reflection walk. Emitting many files of the
same shape costs essentially the cost of the property reads plus the render
walk.

A third cache lives in `Template`: a process-wide set of source strings that
have already passed validation, keyed by the source text. A `Structure` whose
text was validated once is never re-scanned for syntax errors on later renders,
so repeated renders pay only the property reads and the render walk — not a
fresh parse-time validation pass.

This is the structural reason to prefer a `Compositor` over a hand-rolled
`Template.Parse` loop when generating many files of the same shape.

## Worked preset — `CSharpFile`

`Templar.Presets.CSharpFile` is a `Compositor` for `.cs` files with a
**five-section** structure:

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
| `UsingsBlock`   | `{{ usingsBlock }}`| `Sequence.Lines(Usings.Select(u => new Using { Name = u }))` — a `Sequence` of `Using` compositors | Rendered by the engine's nested-composition pathway, so line endings track `RenderOptions.Newline`. |
| `Namespace`     | `{{ namespace }}`  | `""`                                                 | File-scoped namespace, required for valid output.|
| `Body`          | `{{ body }}`       | `""`                                                 | Everything below the namespace.                |

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

Subclass it to add your own bindings — they auto-bind alongside the inherited
ones:

```csharp
public sealed class RecordFile : CSharpFile
{
    public string TypeName { get; init; } = "";

    public override string Body { get; init; } =
        "public sealed record {{ typeName }};";
}

new RecordFile { Namespace = "Acme", TypeName = "User" }.Render();
```

The body template substitutes through the same auto-bind layer because
`TypeName` is bindable on the subclass.

## Pattern checklist

A well-shaped `Compositor`:

- Declares one `Structure` (raw or interpolated literal — your call).
- Holds each substituted value as a `{ get; init; }` property.
- Uses `[TemplateBind]` only when the C# name and the template name must differ.
- Uses `[TemplateIgnore]` for helpers/inputs that are folded into other bindings.
- Stays free of side effects in property getters — `Populate` reads them once per render.
- Renders by `new MyComposite { … }.Render()`. No string keys at the call site.

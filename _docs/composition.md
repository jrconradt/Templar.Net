# Composition

Templates are views; objects are their view models.

- **A template is a view.** It is declarative text in any language (C#, YAML, a
  Dockerfile, HTML) under `Templates/`, written before anything is compiled. It names
  the properties that fill its spots. Its contract: *your properties are the
  properties I use — I do not change for you or reinterpret.* It knows nothing of your
  data model or your logic.
- **An object is a view model.** It is any type you already have, in the shape your
  own logic gives it. Many types can fulfill one template, because each is checked to
  carry the properties the template uses, and one type can be rendered by many
  templates.
- **The compiler pairs them.** At build time the Templar generator works out every
  (type, template) pair your code reaches, checks each one, and compiles it into a
  flat renderer with the template text baked in. No `.tpl` is read at run time, and a
  template nothing reaches is not compiled in.

It is MVVM without the boilerplate: no `INotifyPropertyChanged`, no converters, no
template selectors, no binding paths, no code-behind. A spot binds a property by
name; a value's declared type decides how it is written; a choice between shapes is a
choice of type.

For spot rules see [syntax.md](syntax.md); for names, wiring, what the generator
emits and its diagnostics see [integration.md](integration.md).

## The model

| You have | You write |
|----------|-----------|
| Something to render | A `.tpl` under `Templates/`; its name is its path there without `.tpl` |
| Data to render it with | Nothing new: any type whose readable properties the template's spots name |
| A root to render | `Compositor.ResolveTemplate(obj, "Template/Name")`, or `Compositor.ResolveTemplate(obj)` when `obj`'s type names its default |
| A type that usually renders one way | `[Template("Template/Name")]` on the type |
| A spot that should use a particular template | `{{ property : Template/Name }}` |
| A variant | An interface or abstract class, with `[Template]` on each implementation |
| Many of a thing | A `Sequence<T>` (its separator travels with it) or any `IEnumerable<T>` |
| Nothing there | `null` or an empty list — renders nothing; a line left blank is dropped |

## The smallest example

`Templates/Orders/Requisite.tpl`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

`Templates/Orders/Stocked.tpl` is `stocked {{ sku }}`; `Templates/Orders/Backordered.tpl`
is `backordered {{ sku }}` and `    eta {{ eta }}` on two lines.

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

var requisite = new Requisite("R-1", Sequence.Lines<ILine>([new Stocked("A-100"), new Backordered("B-200", "May")]));

Compositor.ResolveTemplate(requisite, "Orders/Requisite");
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
`requisite R-1\n{\n}`. Nothing picks `Stocked.tpl` over `Backordered.tpl`: the type of
each value decides.

## Many types, one template

A template names properties, not a type. Any type carrying them fills it:

```csharp
public sealed record Book(string Title, string Author);
public sealed class Film { public required string Title { get; init; } public int Year { get; init; } }

Compositor.ResolveTemplate(new Book("Dune", "Herbert"), "Card");   // Templates/Card.tpl: card {{ title }}
Compositor.ResolveTemplate(new Film { Title = "Alien" }, "Card");
```

Each call site is checked against its object's static type: `Book` and `Film` each get
their own compiled renderer for `Card`. A type missing `title` is an error at the call
site that names the template line.

## Naming the template a spot uses

A spot renders its value by its declared type, unless it names a template:

```
public {{ type }}({{ parameters : Injection/Parameter }})
{
    {{ lead : Injection/Assignment }}
}
```

```csharp
public sealed record Member(string Type, string Name);
public sealed record Ctor(string Type, Sequence<Member> Parameters, Member Lead);
```

`Member` has no `[Template]`; the two spots render it by two different templates. On a
list, the named template renders each item. The value's type must carry what the
named template uses.

## A type's default template

`[Template("Name")]` on a type names the template it renders by when nothing else
does: at a spot whose property has that type, in a list of it, behind an interface it
implements, and at `Compositor.ResolveTemplate(obj)`. The name is searched only among
this project's templates. The type stays plain otherwise: no base class, no interface,
no `partial`.

## How a spot is written

The generator matches each spot to an accessible, readable instance property of the
type, case-insensitively (`{{ name }}` → `Name`), and picks the write from the
property's **declared** type, once, at compile time:

| Declared type | Write |
|---------------|-------|
| `string` | Text; continuation lines take the spot's column. Never rescanned — a `{{ x }}` inside a value is output as written. |
| `bool`, `char`, a numeric type, an enum, or one of these nullable | Text, formatted with the invariant culture (`ToString()` for `bool` and `char`). |
| A type with `[Template]` | That template's renderer for the type, at the spot's column. |
| Anything, with `{{ x : T }}` | Template `T`'s renderer for the declared type. |
| `Sequence<T>` | Each item by `T`'s write (or `: T`), joined by the sequence's separator. |
| `IEnumerable<T>` (a list, an array, …) | Each item by `T`'s write (or `: T`), one per line. |
| An interface or abstract class | The value's runtime type's `[Template]`. Every implementation in the compilation must have one. |
| A `[Template]` type from a referenced assembly | The renderer that assembly registered as the type's default. |

A `null` value, or an empty list, writes nothing. Anything else — `object`, a type
with no `[Template]` and no `: T` — is a compile-time error (`TMPLR008`). A property no
spot names is not rendered.

## Variants are types

A choice between shapes is a choice of type, made when the object is built — not a
condition at render time. Type the property as an interface or abstract class and put
`[Template]` on each implementation. Every implementation in the project must have
one, or the spot is reported (`TMPLR008`). Each generated module registers its
defaults when its assembly loads; an interface-typed spot looks up the value's runtime
type there. That lookup is the only decision made at render time, and a value whose
type has no registered default throws `InvalidOperationException` naming the type.

## Lists

`Sequence<T>` holds the items and the separator:

| Factory | Separator |
|---------|-----------|
| `Sequence.Lines(items)` | newline |
| `Sequence.BlankLines(items)` | blank line |
| `Sequence.CommaList(items)` | `", "` |
| `new Sequence<T>(items, sep)` | `sep` |

Each item renders by its own write; a `\n` in the separator becomes
`RenderOptions.Newline`. A property typed as any other `IEnumerable<T>` renders one
item per line. Lists nest: a `Sequence<Sequence<Item>>` renders each inner list by its
own separator.

## What is compiled

The generator starts from every `[Template]` type and every
`Compositor.ResolveTemplate` call (the static type of the object, and the constant
template name), and follows each spot to the pairs it reaches. Each reached pair
becomes one `Steps` overload in the template's generated class: one `w.Literal(…)` per
run of text and one write per spot, with no `if` and no loop. Templates no pair
reaches are syntax-checked and left out. See
[integration.md](integration.md#what-it-generates) for an emission.

## The engine

Rendering never recurses. A renderer is an iterator: it writes literal text and text
spots straight into the `TemplarWriter`, and yields a frame for each nested template
or list. One loop (`Renderer.Drive`) runs an explicit stack of frames, pushing the
spot's column as indentation for each yielded frame and popping it when the frame
finishes, so an object graph nested thousands deep renders without growing the call
stack.

## Worked preset — `CSharpFile`

`Templar.Presets.CSharpFile` is `[Template("Presets/CSharpFile")]`, a **five-section**
template, `src/Templates/Presets/CSharpFile.tpl`:

```
{{ header }}
{{ pragmas }}
{{ usingsBlock }}

namespace {{ namespace }};

{{ body }}
```

### Properties

| Property | Spot | Default | Notes |
|----------|------|---------|-------|
| `Header` | `{{ header }}` | `#nullable enable` | The library emits no comment banner of its own. |
| `Pragmas` | `{{ pragmas }}` | `""` | Optional, e.g. `#pragma warning disable CS8618`. |
| `Usings` | *(no spot)* | empty | `IEnumerable<string>` of using **names**, no `;`. |
| `UsingsBlock` | `{{ usingsBlock }}` | `Sequence.Lines(Usings.Select(u => new Using { Name = u }))` | A `Sequence<Using>`; `Using` is `[Template("Presets/Using")]` (`using {{ name }};`). |
| `Namespace` | `{{ namespace }}` | `""` | File-scoped namespace, required for valid output. |
| `Body` | `{{ body }}` | `""` | A `string`: placed as text. |

### Usage

```csharp
using Templar.Presets;
using Templar.Rendering;

var file = new CSharpFile
{
    Namespace = "Acme.Generated",
    Usings    = new[] { "System", "System.Linq" },
    Body      = "public sealed class Foo;"
};

string source = Compositor.ResolveTemplate(file);
```

`Body` is text, so a spot inside it is not substituted. To compose an object as the
body of a file, give the file a template of your own:

```
#nullable enable

namespace {{ namespace }};

{{ record : Records/Record }}
```

## Pattern checklist

- Templates under `Templates/`, written as the output you want, naming the properties they use.
- Objects as they already are; `[Template]` on a type only where it has a usual template.
- Roots rendered with `Compositor.ResolveTemplate(obj, "Name")` or `Compositor.ResolveTemplate(obj)`, the name a constant.
- `{{ x : Name }}` where a spot should use a template other than its value's default.
- Variants as implementations of an interface or abstract class, each with `[Template]`.
- Collections as `Sequence<T>` (separator chosen when the object is built) or `IEnumerable<T>`.
- No string keys filled by hand, no `if`/`switch` choosing a template.
- Property getters free of side effects — the renderer reads each once per spot.

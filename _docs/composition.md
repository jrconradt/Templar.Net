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
  flat renderer with the template text baked in. Each `ResolveTemplate` call is
  replaced in place by one that runs its pair's renderer. No `.tpl` is read at run
  time, and a template nothing reaches is not compiled in.

It is MVVM without the boilerplate: no `INotifyPropertyChanged`, no converters, no
template selectors, no binding paths, no code-behind. A spot binds a property by
name, and the property's declared type decides how it is written.

For spot rules see [syntax.md](syntax.md); for names, wiring, what the generator
emits and its diagnostics see [integration.md](integration.md).

## The model

| You have | You write |
|----------|-----------|
| Something to render | A `.tpl` under `Templates/`; its name is its path there without `.tpl` |
| Data to render it with | Nothing new: any type whose readable properties the template's spots name |
| A root to render | `Compositor.ResolveTemplate(obj, "Template/Name")`, or `Compositor.ResolveTemplate(obj)` when `obj`'s type names its template |
| A type that holds a part of the output | `[Template("Template/Name")]` on the type (a class or an interface) |
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

`Templates/Orders/Line.tpl` is `line {{ sku }}` and `    qty {{ quantity }}` on two
lines.

```csharp
using Templar.Rendering;

[Template("Orders/Line")]
public sealed record Line(string Sku, int Quantity);

public sealed record Requisite(string Name, Sequence<Line> Lines);

var requisite = new Requisite("R-1", Sequence.Lines([new Line("A-100", 2), new Line("B-200", 5)]));

Compositor.ResolveTemplate(requisite, "Orders/Requisite");
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

With an empty list the `{{ lines }}` line renders empty and is dropped, leaving
`requisite R-1\n{\n}`.

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

## A type's template

`[Template("Name")]` on a class or an interface associates the type with a template. A
spot whose property has that declared type renders the property's value by it, a list
of the type renders each item by it, and `Compositor.ResolveTemplate(obj)` renders by
it. The type must carry what the template uses; that is checked at compile time, as
for a root. The name is searched only among this project's templates, and a
`[Template]` type declared in another assembly cannot render here (`TMPLR008`). The
type stays plain otherwise: no base class, no interface, no `partial`.

The template is bound by the property's declared type, at compile time. On an
interface it binds the interface's own properties, so a property typed as that
interface renders by the interface's template whatever object it holds. Nothing is
chosen at run time.

## How a spot is written

The generator matches each spot to an accessible, readable instance property of the
type, case-insensitively (`{{ name }}` → `Name`), and picks the write from the
property's **declared** type, once, at compile time:

| Declared type | Write |
|---------------|-------|
| `string` | Text; continuation lines take the spot's column. Never rescanned — a `{{ x }}` inside a value is output as written. |
| `bool`, `char`, a numeric type, an enum, or one of these nullable | Text, formatted with the invariant culture (`ToString()` for `bool` and `char`). |
| A class or interface with `[Template]`, declared in this project | That template's renderer for the type, at the spot's column. |
| `Sequence<T>` | Each item by `T`'s write, joined by the sequence's separator. |
| `IEnumerable<T>` (a list, an array, …) | Each item by `T`'s write, one per line. |

A `null` value, or an empty list, writes nothing. Anything else — `object`, a type
with no `[Template]`, a `[Template]` type from another assembly — is a compile-time
error (`TMPLR008`). A property no spot names is not rendered.

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

## Pattern checklist

- Templates under `Templates/`, written as the output you want, naming the properties they use.
- Objects as they already are; `[Template]` on a type that holds a part of the output.
- Roots rendered with `Compositor.ResolveTemplate(obj, "Name")` or `Compositor.ResolveTemplate(obj)`, the name a constant.
- Collections as `Sequence<T>` (separator chosen when the object is built) or `IEnumerable<T>`.
- No string keys filled by hand, no `if`/`switch` choosing a template.
- Property getters free of side effects — the renderer reads each once per spot.

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
name, and the property's declared type, through its hierarchy, decides how it is
written.

For spot rules see [syntax.md](syntax.md); for names, wiring, what the generator
emits and its diagnostics see [integration.md](integration.md).

## The model

| You have | You write |
|----------|-----------|
| Something to render | A `.tpl` under `Templates/`; its name is its path there without `.tpl` |
| Data to render it with | Nothing new: any type whose readable properties the template's spots name |
| A root to render | `Compositor.ResolveTemplate(obj, "Template/Name")`, or `Compositor.ResolveTemplate(obj)` when `obj`'s type names its template |
| A type that holds a part of the output | `[Template("Template/Name")]` on the type (a class or an interface) |
| Several kinds of a thing | A base type and its subtypes, each kind with its own `[Template]` (or its base's) |
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
for a root. The name is searched only among the templates of the project that declares
the type. A `[Template]` type of a referenced assembly renders here too: that assembly
carries the text of each template its `[Template]` types name, and the generator
compiles and checks the pair in this project like any other
([integration.md](integration.md#pairs)). A type whose `[Template]` is declared in an
assembly that carries no text for its template cannot render here (`TMPLR008`). The
type stays plain otherwise: no base class, no interface, no `partial`.

## The hierarchy

A property or list declared as a base type renders each value by the template of the
value's own type. At compile time the generator collects, for the declared type, the
type itself if it is concrete and, when it is declared in this project or in a
referenced assembly that carries template text, every concrete class or record that
derives from it or implements it, of this project and, for the latter, of that assembly.
Each one renders by the most-derived `[Template]` in its own chain — its own, else its
base class's, else an interface's — so a subtype without one inherits its base's, and an
abstract class or interface in between lends its `[Template]` to the types below it.

`Templates/Orders/Stocked.tpl` is `stocked {{ sku }}`; `Templates/Orders/Backordered.tpl`
is `backordered {{ sku }}` and `    eta {{ eta }}` on two lines.

```csharp
public interface ILine { string Sku { get; } }

[Template("Orders/Stocked")]
public sealed record Stocked(string Sku) : ILine;

[Template("Orders/Backordered")]
public sealed record Backordered(string Sku, string Eta) : ILine;

public sealed record Requisite(string Name, Sequence<ILine> Lines);

ILine[] lines = [new Stocked("A-100"), new Backordered("B-200", "May")];

Compositor.ResolveTemplate(new Requisite("R-1", Sequence.Lines(lines)), "Orders/Requisite");
```

```
requisite R-1
{
    stocked A-100
    backordered B-200
        eta May
}
```

When every type a value can be resolves to the declared type's own template — a type
with `[Template]` and no subtypes, or a base whose subtypes add no `[Template]` of their
own — the spot is a direct call to that pair's renderer, binding the declared type's
properties. Otherwise the generator emits a **resolver** for the declared type: a type
switch with one arm per type, each calling that type's renderer, and each type is
checked against its template like any pair. The resolver is the one predicate Templar
performs: type resolution over a closed set the compiler knows, in its own generated
file, with no `if` on values, no loop and no lookup. `Compositor.ResolveTemplate(obj)`
on a base-typed variable resolves the same way; `Compositor.ResolveTemplate(obj,
"Name")` renders by the named template, checked against the static type.

A concrete type in the set with no `[Template]` in its chain is a compile-time error
(`TMPLR008`) at the spot, naming that type. Only a declared type of this project, or of
a referenced assembly that carries template text, collects subtypes: from this project
and, for the latter, from that assembly too. A subtype a third assembly declares is not
collected: it has no arm of its own in a resolver, which throws for a value no arm
matches. A property typed `object` is still an error.

## How a spot is written

The generator matches each spot to an accessible, readable instance property of the
type, case-insensitively (`{{ name }}` → `Name`), and picks the write from the
property's **declared** type, once, at compile time:

| Declared type | Write |
|---------------|-------|
| `string` | Text; continuation lines take the spot's column. Never rescanned — a `{{ x }}` inside a value is output as written. |
| `bool`, `char`, a numeric type, an enum, or one of these nullable | Text, formatted with the invariant culture (`ToString()` for `bool` and `char`). |
| A class or interface with a `[Template]` in its chain or, when it is of this project or of a referenced assembly that carries template text, in the chain of a subtype collected for it | Each value by its own type's template ([the hierarchy](#the-hierarchy)), at the spot's column. |
| `Sequence<T>` | Each item by `T`'s write, joined by the sequence's separator. |
| `IEnumerable<T>` (a list, an array, …) | Each item by `T`'s write, one per line. |

A `null` value, or an empty list, writes nothing. Anything else — `object`, a type
with no `[Template]` in its hierarchy, a type whose `[Template]` is declared in an
assembly that carries no text for its template, a type with a concrete subtype that has
no `[Template]` in its chain — is a compile-time error (`TMPLR008`). A property no spot
names is not rendered.

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
run of text and one write per spot, with no `if` and no loop. A declared type that
resolves to several templates gets one resolver, its type switch, in
`Templar.Resolvers.g.cs`. Templates no pair reaches are syntax-checked and left out.
See [integration.md](integration.md#what-it-generates) for an emission.

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

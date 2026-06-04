# Templates

A `Template` is a single parsed string with a bag of variables, a filter table,
and render options. This page covers the **single-template workflow**: parse
once, set variables, render. For loading many templates from disk or embedded
resources see [integration.md](integration.md); for declarative
class-as-template authoring see [composition.md](composition.md).

## The smallest example

```csharp
using Templar.Rendering;

var t = Template.Parse("Hello, {{ who }}!");
t["who"] = "World";
Console.WriteLine(t.Render());   // → "Hello, World!"
```

That is the entire mental model: `Parse` → assign → `Render`.

## Surface

| Member                           | Returns           | Effect                                                          |
|----------------------------------|-------------------|-----------------------------------------------------------------|
| `Template.Parse(string text)`    | `Template`        | Tokenize + parse once. Throws `TemplateParseException` on error.|
| `template[string key]`           | `object?`         | Get/set a variable. Names are case-insensitive.                 |
| `template.Set(key, value)`       | `Template` (self) | Fluent setter — same as the indexer, returns `this`.            |
| `template.AddFilter(name, fn)`   | `Template` (self) | Register or override a filter for this template instance.       |
| `template.WithOptions(options)`  | `Template` (self) | Set newline + indent. Returns `this`.                           |
| `template.Render()`              | `string`          | Walk the template, substitute variables, run filters, emit text.|

All fluent methods return the same `Template`, so a full setup can be one
expression:

```csharp
var output = Template
    .Parse("class {{ name | pascal }} {}")
    .Set("name", "user_profile")
    .Render();
// → "class UserProfile {}"
```

## Setting variables

Three equivalent ways. Pick whichever reads well in context.

```csharp
t["body"] = "..."                          // indexer
t.Set("body", "...")                       // fluent
t.Set("count", 42).Set("kind", "Foo")      // chained
```

Variable lookup is **case-insensitive** — `t["Body"]` and `t["body"]` write to
the same slot. Unset (or `null`) variables render as the empty string.

Values can be any `object?`. The renderer materializes them as follows:

| Value type                | Treated as                                                       |
|---------------------------|------------------------------------------------------------------|
| `null` / unset            | Empty string.                                                    |
| `string`                  | The string itself.                                               |
| `IEnumerable<string>`     | Joined with the configured newline; the join behaves as a multi-line value (each item gets indented to the placeholder column). |
| `Compositor` / `Sequence` (any `IComposable`) | Rendered **structurally** into the placeholder's column — nested composition with indentation preserved across the boundary. |
| `IEnumerable<IComposable>` | Each item rendered and joined by the configured newline, indentation preserved. |
| Anything else             | `value.ToString() ?? ""`.                                        |

## Filters

Inside the template, the pipe applies one filter to the value:

```
{{ name | pascal }}
```

Only one filter per placeholder. Filter names are case-insensitive. The four
built-ins (`upper`, `lower`, `pascal`, `camel`) are described in
[syntax.md](syntax.md#built-in-filters). Register a custom filter with
`AddFilter`:

```csharp
var t = Template.Parse("{{ tag | shout }}")
    .AddFilter("shout", v => (v?.ToString() ?? "").ToUpperInvariant() + "!!!");
t["tag"] = "ship it";
t.Render();   // → "SHIP IT!!!"
```

Filters registered on a `Template` instance only apply to that instance.
To share filters across many templates, register them on a `TemplateSet` and
they will propagate (see [integration.md](integration.md#filters-and-options-propagate)).

## Render options

`RenderOptions` controls newline and indent string:

```csharp
public sealed class RenderOptions
{
    public string IndentString { get; init; } = "    ";
    public string Newline { get; init; } = "\n";
    public bool StrictUndefined { get; init; } = false;
    public Func<string, string>? Escape { get; init; } = null;
}
```

`StrictUndefined`, when `true`, makes rendering a never-set variable throw
`TemplateRenderException` instead of emitting empty. `Escape` is an optional
function applied to interpolated text (off by default; `Templar.UI` sets it for
HTML escaping — see [ui.md](ui.md)). `IndentString` is the unit of indentation the renderer applies when a multi-line
value inherits the column of its placeholder: the continuation lines are padded
with whole `IndentString` units to reach the placeholder's indent depth. The
default is four spaces; set it to `"\t"` to emit tab-indented output, or to any
other unit you prefer. Set the options per-template with `WithOptions`.

CRLF and lone CR in injected values are normalized to the configured newline, so
output is consistent regardless of the input's line endings.

```csharp
var t = Template.Parse("a\n{{ x }}\nb")
    .WithOptions(new RenderOptions { Newline = "\r\n" });
t["x"] = "MID";
t.Render();   // → "a\r\nMID\r\nb"
```

## Exceptions

Two exception types, both with diagnostic context:

| Exception                    | Carries                                          | Thrown by                                  |
|------------------------------|--------------------------------------------------|--------------------------------------------|
| `TemplateParseException`     | `Line`, `Column`                                 | `Template.Parse` on malformed template text|
| `TemplateRenderException`    | `FilterName`, `VariableName`, `TemplateName?`    | `Render()` on unknown filter, etc.         |

```csharp
try { Template.Parse("oops {{ unclosed"); }
catch (TemplateParseException ex)
{
    // ex.Line, ex.Column point at the offending position
}
```

## When to reach for a template (vs. a Compositor)

Use `Template` directly when:
- The template text is one-off or constructed at runtime.
- You only need a handful of variables.
- You want the most direct control over filter registration and options.

Reach for a `Compositor` ([composition.md](composition.md)) when:
- You're rendering the same template shape many times with different data.
- You want properties on a class to drive the bindings (no string keys).
- You want the parsing + reflection done once per type instead of per call.

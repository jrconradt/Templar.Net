# Template Syntax

## Expressions

Placeholders are delimited by double braces:

```
{{ variableName }}
```

Whitespace inside the braces is trimmed, so `{{name}}`, `{{ name }}`, and `{{  name  }}` are equivalent.

Variable names are **case-insensitive** -- `{{ Name }}` and `{{ name }}` resolve to the same value.

If a variable is not set or is `null`, it renders as an empty string.

## Filters

Apply a filter to a value with the pipe operator:

```
{{ variableName | filterName }}
```

Only one filter per expression. The filter name is also case-insensitive.

### Built-in Filters

| Filter   | Input           | Output          |
|----------|-----------------|-----------------|
| `upper`  | `hello world`   | `HELLO WORLD`   |
| `lower`  | `Hello World`   | `hello world`   |
| `pascal` | `hello_world`   | `HelloWorld`     |
| `camel`  | `hello_world`   | `helloWorld`     |

`pascal` and `camel` split on `_`, `-`, and space, then rejoin with casing applied. An empty string passes through unchanged.

Custom filters can be registered via `Template.AddFilter()` or `TemplateSet.AddFilter()`.

## Raw Output

A leading `&` marks an expression as **raw** — `{{& name }}` emits the value without applying the configured escape function:

```
{{& trustedMarkup }}
```

With no escape function set (the default for the core engine), `{{ x }}` and `{{& x }}` are identical. The distinction only matters when `RenderOptions.Escape` is configured — for example the HTML escaping in `Templar.UI`, where `{{ x }}` is escaped and `{{& x }}` is verbatim. The `{{> name }}` marker is the same raw path used as a child-component slot (see [ui.md](ui.md)).

## Comments

```
{{# This is a comment }}
```

Comments are stripped entirely from the output. They produce no whitespace, no newlines -- nothing.

## Conditionals

A conditional renders its body only when a variable is truthy. The opening tag `{{? name }}` names a single variable; the closing tag is the bare `{{?}}`:

```
{{? isPublic }}public {{?}}class Foo
```

With `isPublic` truthy the output is `public class Foo`; otherwise `class Foo`.

Add an else arm with `{{?else}}`:

```
class Foo{{? args }}<{{ args }}>{{?else}}/* non-generic */{{?}}
```

Negate the test with a leading `!` -- `{{?!name}}` renders its body when `name` is **falsy**:

```
{{?!isSealed }}// open for extension{{?}}
```

Conditionals nest; each `{{? }}` pairs with the next unmatched `{{?}}`. The body may contain placeholders, filters, and further conditionals, and a multi-line value substituted inside a conditional keeps the indentation of its placeholder.

### Truthiness

The condition tests the named variable's value:

| Value | Truthy? |
|-------|---------|
| unset / `null` | falsy |
| `""` (empty string) | falsy |
| `false` | falsy |
| empty `IEnumerable` | falsy |
| everything else | truthy |

Conditional tags are **inline** constructs. The `{{? }}` and `{{?}}` markers do not consume the line they sit on, so a tag placed alone on its own line leaves that line's newline in the output. Keep a conditional on the same line as the text it guards.

## Literal Text

Everything outside `{{ }}` is literal text, emitted as-is. This includes whitespace and newlines.

## Escapes

To emit a literal delimiter or backslash, prefix it with `\`:

| Sequence | Output |
|----------|--------|
| `\{{` | `{{` |
| `\}}` | `}}` |
| `\\` | `\` |

A backslash before any other character is emitted as-is, so ordinary Windows paths and regex escapes in literal text need no special handling.

## Indentation Behavior

This is the core feature that makes Templar useful for code generation.

When a **single-line** value is substituted, it replaces the placeholder inline:

```
Input:    Hello {{ name }}!
name =    "World"
Output:   Hello World!
```

When a **multi-line** value is substituted, the first line replaces the placeholder inline, and all subsequent lines are indented to the **column position** where the placeholder started:

```
Input:
    public class Foo
    {
        {{ body }}
    }

body =
    public void Bar()
    {
        // ...
    }

Output:
    public class Foo
    {
        public void Bar()
        {
            // ...
        }
    }
```

The continuation lines of `body` are padded to the indent depth where `{{ body }}` started, preserving the relative indentation within the value itself. The padding is built from whole `RenderOptions.IndentString` units (four spaces by default; set it to `"\t"` for tab-indented output), so the inherited indentation is rendered in the same unit as the rest of the document. This works at arbitrary nesting depths.

## Newline Handling

`\r\n` (Windows) and `\r` are normalized to `\n` internally. Output newlines are controlled by `RenderOptions.Newline` (defaults to `\n`).

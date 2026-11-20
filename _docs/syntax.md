# Template Syntax

A template is any text. Spots, comments and escapes are the only markup.

## Spots

A spot names a property of the object being rendered:

```
{{ propertyName }}
```

Whitespace inside the braces is trimmed, so `{{name}}`, `{{ name }}`, and `{{  name  }}` are equivalent.

Property names are **case-insensitive** -- `{{ Name }}` and `{{ name }}` bind the same property.

A `null` value renders as an empty string.

### Naming the template a spot uses

```
{{ propertyName : Template/Name }}
```

The value is rendered by the named template — the template's path under `Templates/` without `.tpl` — instead of by its type's default. On a list, each item is rendered by it. The value's type must carry the properties the named template uses.

## Comments

```
{{# This is a comment }}
```

Comments are stripped entirely from the output. They produce no whitespace, no newlines -- nothing.

## Unsupported tags

A tag that opens with `{{?`, `{{&`, or `{{>`, that contains a `|`, or whose content is not a property name optionally followed by `: Template/Name`, is rejected at compile time: the generator reports `TMPLR002` at the tag's line. Templates are compiled, never parsed at run time.

## Literal Text

Everything outside `{{ }}` is literal text, emitted as-is. This includes whitespace and newlines; a carriage return in the template text is dropped.

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

When a **single-line** value is substituted, it replaces the spot inline:

```
Input:    Hello {{ name }}!
name =    "World"
Output:   Hello World!
```

When a **multi-line** value is substituted, the first line replaces the spot inline, and all subsequent lines are indented to the **column position** where the spot started:

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

The continuation lines of `body` are padded to the indent depth where `{{ body }}` started, preserving the relative indentation within the value itself. The same holds for a value rendered by a template and for each item of a list. The padding is built from whole `RenderOptions.IndentString` units (four spaces by default; set it to `"\t"` for tab-indented output), so the inherited indentation is rendered in the same unit as the rest of the document. This works at arbitrary nesting depths.

A line whose spots all rendered empty and that holds only whitespace is dropped, newline included.

## Newline Handling

`\r\n` (Windows) and `\r` are normalized to `\n` internally. Output newlines are controlled by `RenderOptions.Newline` (defaults to `\n`).

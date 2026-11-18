# Template Syntax

## Expressions

Placeholders are delimited by double braces:

```
{{ variableName }}
```

Whitespace inside the braces is trimmed, so `{{name}}`, `{{ name }}`, and `{{  name  }}` are equivalent.

Variable names are **case-insensitive** -- `{{ Name }}` and `{{ name }}` resolve to the same value.

If a variable is not set or is `null`, it renders as an empty string.

## Comments

```
{{# This is a comment }}
```

Comments are stripped entirely from the output. They produce no whitespace, no newlines -- nothing.

## Unsupported tags

A tag that opens with `{{?`, `{{&`, or `{{>`, or that contains a `|`, is rejected when the template is parsed: `Template.Parse` throws `TemplateParseException` with the line and column, and the `.tpl` accessor generator reports `TMPLR002`.

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

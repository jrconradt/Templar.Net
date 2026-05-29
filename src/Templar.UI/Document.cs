namespace Templar.UI;

public sealed class Document : UIComponent
{
    public string Lang { get; init; } = "en";

    public string Title { get; init; } = "";

    public object? Head { get; init; }

    public object? Body { get; init; }

    protected override string Structure => """
        <!DOCTYPE html>
        <html lang="{{ lang }}">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{ title }}</title>
            {{ head }}
        </head>
        <body>
            {{ body }}
        </body>
        </html>
        """;
}

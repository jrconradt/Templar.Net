using System.Text;
using Fixture.Generator.Orders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Templar.Rendering;

namespace Fixture.Generator;

[Generator]
public sealed class FixtureGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(output =>
        {
            Stocked[] lines = [new Stocked("A-100"), new Stocked("B-200")];
            var order = new Order("R-1",
                                  Sequence.Lines(lines),
                                  lines.Length,
                                  Sequence.CommaList([new Tag("rush"), new Tag("fragile")]));
            var constant = new ConstantClass("Emitted", SymbolDisplay.FormatLiteral(Compositor.ResolveTemplate(order), quote: true));
            var file = new SourceFile("Fixture.Consumer", Compositor.ResolveTemplate(constant, "ConstantClass"));
            output.AddSource("Emitted.g.cs", SourceText.From(Compositor.ResolveTemplate(file), Encoding.UTF8));
        });
    }
}

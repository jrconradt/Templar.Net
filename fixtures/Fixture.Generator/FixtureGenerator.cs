using System.Text;
using Fixture.Generator.Orders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Templar.Presets;
using Templar.Rendering;

namespace Fixture.Generator;

[Generator]
public sealed class FixtureGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(output =>
        {
            ILine[] lines = [new Stocked { Sku = "A-100" }, new Backordered { Sku = "B-200", Eta = "May" }];
            var order = new Order
            {
                Id = "R-1",
                Lines = Sequence.Lines(lines),
                Count = lines.Length,
                Tags = Sequence.CommaList([new Tag("rush"), new Tag("fragile")]),
            };
            var constant = new ConstantClass
            {
                Name = "Emitted",
                Literal = SymbolDisplay.FormatLiteral(order.Render(), quote: true),
            };
            var file = new CSharpFile
            {
                Namespace = "Fixture.Consumer",
                Body = constant.Render(),
            };
            output.AddSource("Emitted.g.cs", SourceText.From(file.Render(), Encoding.UTF8));
        });
    }
}

using System.Collections;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Templar.Generators.Tests;

public class IncrementalTests
{
    private const string Source = "namespace App; public sealed class Card { public required string Who { get; init; } }";

    [Fact]
    public void Unrelated_edit_leaves_the_generated_output_cached()
    {
        var driver = GeneratorHarness.CreateDriver(new[] { ("/proj/Card.tpl", "Hello {{ who }}") });
        var compilation = GeneratorHarness.Compile(new[] { Source });
        var first = GeneratorHarness.Advance(driver, compilation);

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace App; public sealed class Unrelated { }",
                                                                          GeneratorHarness.ParseOptions));
        var second = GeneratorHarness.Advance(first.Driver, edited);

        var result = second.Driver.GetRunResult().Results.Single();
        var renderers = result.TrackedSteps["Renderers"].SelectMany(step => step.Outputs).ToList();
        var sourceOutputs = result.TrackedOutputSteps.SelectMany(step => step.Value).SelectMany(step => step.Outputs).ToList();
        Assert.NotEmpty(renderers);
        Assert.NotEmpty(sourceOutputs);
        Assert.All(renderers, output => Assert.Equal(IncrementalStepRunReason.Unchanged, output.Reason));
        Assert.All(sourceOutputs, output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));
    }

    [Fact]
    public void Pipeline_outputs_hold_no_symbol_compilation_or_location()
    {
        var driver = GeneratorHarness.CreateDriver(new[] { ("/proj/Card.tpl", "Hello {{ who }} {{ missing }}") });
        var run = GeneratorHarness.Advance(driver, GeneratorHarness.Compile(new[] { Source }));

        var result = run.Driver.GetRunResult().Results.Single();
        var values = result.TrackedSteps["Renderers"]
            .Concat(result.TrackedOutputSteps.SelectMany(step => step.Value))
            .SelectMany(step => step.Outputs)
            .Select(output => output.Value)
            .ToList();
        Assert.NotEmpty(values);
        foreach (var value in values)
        {
            AssertPlain(value);
        }
    }

    private static void AssertPlain(object? value)
    {
        Assert.False(value is ISymbol, $"ISymbol in pipeline output: {value}");
        Assert.False(value is Compilation, $"Compilation in pipeline output: {value}");
        Assert.False(value is Location, $"Location in pipeline output: {value}");
        if (value is System.Runtime.CompilerServices.ITuple tuple)
        {
            for (int i = 0; i < tuple.Length; i++)
            {
                AssertPlain(tuple[i]);
            }
            return;
        }
        if (value is IEnumerable sequence and not string)
        {
            foreach (var item in sequence)
            {
                AssertPlain(item);
            }
            return;
        }
        if (value is not null && value.GetType().Namespace == "Templar.Generators")
        {
            foreach (var property in value.GetType().GetProperties())
            {
                if (property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract")
                {
                    AssertPlain(property.GetValue(value));
                }
            }
        }
    }
}

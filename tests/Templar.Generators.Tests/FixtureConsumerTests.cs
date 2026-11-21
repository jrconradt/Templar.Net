using Xunit;

namespace Templar.Generators.Tests;

// Fixture.Generator is a source generator whose own model types have their .tpl files under Templates/ and
// are compiled by Templar.Generators; Fixture.Consumer loads it through the Roslyn analyzer loader with
// Templar.dll beside it (GetDependencyTargetPaths), and the constant it emits is the rendered model.
public class FixtureConsumerTests
{
    [Fact]
    public void Generator_loaded_as_an_analyzer_renders_its_model_into_the_consumer()
    {
        var expected = "order R-1\n"
            + "{\n"
            + "    stocked A-100\n"
            + "    backordered B-200\n"
            + "        eta May\n"
            + "}\n"
            + "total 2: #rush, #fragile";

        var emitted = Fixture.Consumer.Emitted.Text;
        Assert.Equal(expected, emitted);
    }
}

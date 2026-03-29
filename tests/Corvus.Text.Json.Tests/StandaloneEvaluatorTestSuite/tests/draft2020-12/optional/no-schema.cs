using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.NoSchema;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationWithoutSchema : IClassFixture<SuiteValidationWithoutSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationWithoutSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestA3CharacterStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestA1CharacterStringIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\no-schema.json",
                "{\r\n            \"minLength\": 2\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.NoSchema",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

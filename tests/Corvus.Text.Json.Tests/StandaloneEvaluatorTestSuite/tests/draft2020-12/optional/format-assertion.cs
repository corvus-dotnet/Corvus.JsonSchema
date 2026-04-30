using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFormatAssertionFalseValidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFormatAssertionFalseInvalidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not-an-ipv4\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/false\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-false.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFormatAssertionTrueValidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFormatAssertionTrueInvalidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not-an-ipv4\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/true\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-true.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

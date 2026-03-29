using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Default;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteInvalidTypeForDefault : IClassFixture<SuiteInvalidTypeForDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInvalidTypeForDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenPropertyIsSpecified()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 13}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"type\": \"integer\",\r\n                    \"default\": []\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteInvalidStringValueForDefault : IClassFixture<SuiteInvalidStringValueForDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInvalidStringValueForDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenPropertyIsSpecified()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": \"good\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"bar\": {\r\n                    \"type\": \"string\",\r\n                    \"minLength\": 4,\r\n                    \"default\": \"bad\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing : IClassFixture<SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumPassing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumFailing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 5 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingPropertiesAreNotFilledInWithTheDefault()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"alpha\": {\r\n                    \"type\": \"number\",\r\n                    \"maximum\": 3,\r\n                    \"default\": 5\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

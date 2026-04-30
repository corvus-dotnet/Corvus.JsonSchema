using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Required;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRequiredValidation : IClassFixture<SuiteRequiredValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPresentRequiredPropertyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonPresentRequiredPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresBoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\required.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {},\r\n                \"bar\": {}\r\n            },\r\n            \"required\": [\"foo\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Required",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRequiredDefaultValidation : IClassFixture<SuiteRequiredDefaultValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredDefaultValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNotRequiredByDefault()
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
                "tests\\draft4\\required.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Required",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRequiredWithEscapedCharacters : IClassFixture<SuiteRequiredWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithAllPropertiesPresentIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\\"bar\": 1,\r\n                    \"foo\\\\bar\": 1,\r\n                    \"foo\\rbar\": 1,\r\n                    \"foo\\tbar\": 1,\r\n                    \"foo\\fbar\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithSomePropertiesMissingIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": \"1\",\r\n                    \"foo\\\"bar\": \"1\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\required.json",
                "{\r\n            \"required\": [\r\n                \"foo\\nbar\",\r\n                \"foo\\\"bar\",\r\n                \"foo\\\\bar\",\r\n                \"foo\\rbar\",\r\n                \"foo\\tbar\",\r\n                \"foo\\fbar\"\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Required",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames : IClassFixture<SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoneOfThePropertiesMentioned()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestProtoPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"__proto__\": \"foo\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestToStringPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"toString\": { \"length\": 37 } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestConstructorPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"constructor\": { \"length\": 37 } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \r\n                    \"__proto__\": 12,\r\n                    \"toString\": { \"length\": \"foo\" },\r\n                    \"constructor\": 37\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\required.json",
                "{ \"required\": [\"__proto__\", \"toString\", \"constructor\"] }",
                "StandaloneEvaluatorTestSuite.Draft4.Required",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

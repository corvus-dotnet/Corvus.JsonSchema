using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Properties;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteObjectPropertiesValidation : IClassFixture<SuiteObjectPropertiesValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteObjectPropertiesValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBothPropertiesPresentAndValidIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": \"baz\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnePropertyInvalidIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": {}}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothPropertiesInvalidIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [], \"bar\": {}}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTInvalidateOtherProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": []}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
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

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"integer\"},\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction : IClassFixture<SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyValidatesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [1, 2]}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPropertyInvalidatesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [1, 2, 3, 4]}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPatternPropertyInvalidatesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": []}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPatternPropertyValidatesNonproperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"fxo\": [1, 2]}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPatternPropertyInvalidatesNonproperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"fxo\": []}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalPropertyIgnoresProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": []}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalPropertyValidatesOthers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": 3}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalPropertyInvalidatesOthers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": \"foo\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"array\", \"maxItems\": 3},\r\n                \"bar\": {\"type\": \"array\"}\r\n            },\r\n            \"patternProperties\": {\"f.o\": {\"minItems\": 2}},\r\n            \"additionalProperties\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertiesWithBooleanSchema : IClassFixture<SuitePropertiesWithBooleanSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWithBooleanSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoPropertyPresentIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyTruePropertyPresentIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyFalsePropertyPresentIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothPropertiesPresentIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": true,\r\n                \"bar\": false\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertiesWithEscapedCharacters : IClassFixture<SuitePropertiesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithAllNumbersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\\"bar\": 1,\r\n                    \"foo\\\\bar\": 1,\r\n                    \"foo\\rbar\": 1,\r\n                    \"foo\\tbar\": 1,\r\n                    \"foo\\fbar\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": \"1\",\r\n                    \"foo\\\"bar\": \"1\",\r\n                    \"foo\\\\bar\": \"1\",\r\n                    \"foo\\rbar\": \"1\",\r\n                    \"foo\\tbar\": \"1\",\r\n                    \"foo\\fbar\": \"1\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\\nbar\": {\"type\": \"number\"},\r\n                \"foo\\\"bar\": {\"type\": \"number\"},\r\n                \"foo\\\\bar\": {\"type\": \"number\"},\r\n                \"foo\\rbar\": {\"type\": \"number\"},\r\n                \"foo\\tbar\": {\"type\": \"number\"},\r\n                \"foo\\fbar\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertiesWithNullValuedInstanceProperties : IClassFixture<SuitePropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWithNullValuedInstanceProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": null}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"null\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames : IClassFixture<SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames(Fixture fixture)
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
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestProtoNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"__proto__\": \"foo\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestToStringNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"toString\": { \"length\": 37 } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestConstructorNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"constructor\": { \"length\": 37 } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllPresentAndValid()
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
                "tests\\draft2020-12\\properties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"__proto__\": {\"type\": \"number\"},\r\n                \"toString\": {\r\n                    \"properties\": { \"length\": { \"type\": \"string\" } }\r\n                },\r\n                \"constructor\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

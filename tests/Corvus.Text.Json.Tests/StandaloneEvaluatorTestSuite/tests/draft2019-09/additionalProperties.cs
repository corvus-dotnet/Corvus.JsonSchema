using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties : IClassFixture<SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoAdditionalPropertiesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnAdditionalPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : 1, \"bar\" : 2, \"quux\" : \"boom\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobarbaz\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPatternPropertiesAreNotAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":1, \"vroom\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}},\r\n            \"patternProperties\": { \"^v\": {} },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNonAsciiPatternWithAdditionalProperties : IClassFixture<SuiteNonAsciiPatternWithAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonAsciiPatternWithAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchingThePatternIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"ármányos\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotMatchingThePatternIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"élmény\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"patternProperties\": {\"^á\": {}},\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesWithSchema : IClassFixture<SuiteAdditionalPropertiesWithSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoAdditionalPropertiesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnAdditionalValidPropertyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : 1, \"bar\" : 2, \"quux\" : true}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnAdditionalInvalidPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : 1, \"bar\" : 2, \"quux\" : 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}},\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesCanExistByItself : IClassFixture<SuiteAdditionalPropertiesCanExistByItself.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesCanExistByItself(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnAdditionalValidPropertyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : true}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnAdditionalInvalidPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : 1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesAreAllowedByDefault : IClassFixture<SuiteAdditionalPropertiesAreAllowedByDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesAreAllowedByDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalPropertiesAreAllowed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2, \"quux\": true}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesDoesNotLookInApplicators : IClassFixture<SuiteAdditionalPropertiesDoesNotLookInApplicators.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesDoesNotLookInApplicators(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertiesDefinedInAllOfAreNotExamined()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": true}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\"properties\": {\"foo\": {}}}\r\n            ],\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesWithNullValuedInstanceProperties : IClassFixture<SuiteAdditionalPropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithNullValuedInstanceProperties(Fixture fixture)
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
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalProperties\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesWithPropertyNames : IClassFixture<SuiteAdditionalPropertiesWithPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithPropertyNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidAgainstBothKeywords()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"apple\": 4 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidAgainstPropertyNamesButNotAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"fig\": 2, \"pear\": \"available\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"propertyNames\": {\r\n                \"maxLength\": 5\r\n            },\r\n            \"additionalProperties\": {\r\n                \"type\": \"number\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteDependentSchemasWithAdditionalProperties : IClassFixture<SuiteDependentSchemasWithAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependentSchemasWithAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalPropertiesDoesnTConsiderDependentSchemas()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalPropertiesCanTSeeBar()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": \"\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalPropertiesCanTSeeBarEvenWhenFoo2IsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo2\": \"\", \"bar\": \"\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo2\": {}},\r\n            \"dependentSchemas\": {\r\n                \"foo\" : {},\r\n                \"foo2\": {\r\n                    \"properties\": {\r\n                        \"bar\":{}\r\n                    }\r\n                }\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

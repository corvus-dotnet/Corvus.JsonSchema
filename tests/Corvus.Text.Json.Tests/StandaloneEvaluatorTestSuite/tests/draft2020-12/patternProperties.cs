using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.PatternProperties;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePatternPropertiesValidatesPropertiesMatchingARegex : IClassFixture<SuitePatternPropertiesValidatesPropertiesMatchingARegex.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternPropertiesValidatesPropertiesMatchingARegex(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestASingleValidMatchIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMultipleValidMatchesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"foooooo\" : 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestASingleInvalidMatchIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\", \"fooooo\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMultipleInvalidMatchesIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\", \"foooooo\" : \"baz\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
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
                "tests\\draft2020-12\\patternProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"f.*o\": {\"type\": \"integer\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PatternProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMultipleSimultaneousPatternPropertiesAreValidated : IClassFixture<SuiteMultipleSimultaneousPatternPropertiesAreValidated.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleSimultaneousPatternPropertiesAreValidated(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestASingleValidMatchIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 21}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestASimultaneousMatchIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"aaaa\": 18}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMultipleMatchesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 21, \"aaaa\": 18}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDueToOneIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": \"bar\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDueToTheOtherIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"aaaa\": 31}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDueToBothIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"aaa\": \"foo\", \"aaaa\": 31}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\patternProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"a*\": {\"type\": \"integer\"},\r\n                \"aaa*\": {\"maximum\": 20}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PatternProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRegexesAreNotAnchoredByDefaultAndAreCaseSensitive : IClassFixture<SuiteRegexesAreNotAnchoredByDefaultAndAreCaseSensitive.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRegexesAreNotAnchoredByDefaultAndAreCaseSensitive(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNonRecognizedMembersAreIgnored()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"answer 1\": \"42\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRecognizedMembersAreAccountedFor()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a31b\": null }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRegexesAreCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a_x_3\": 3 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRegexesAreCaseSensitive2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a_X_3\": 3 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\patternProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"[0-9]{2,}\": { \"type\": \"boolean\" },\r\n                \"X_\": { \"type\": \"string\" }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PatternProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePatternPropertiesWithBooleanSchemas : IClassFixture<SuitePatternPropertiesWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternPropertiesWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithPropertyMatchingSchemaTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithPropertyMatchingSchemaFalseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithBothPropertiesIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithAPropertyMatchingBothTrueAndFalseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foobar\":1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyObjectIsValid()
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
                "tests\\draft2020-12\\patternProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"f.*\": true,\r\n                \"b.*\": false\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PatternProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePatternPropertiesWithNullValuedInstanceProperties : IClassFixture<SuitePatternPropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternPropertiesWithNullValuedInstanceProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foobar\": null}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\patternProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"^.*bar$\": {\"type\": \"null\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PatternProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

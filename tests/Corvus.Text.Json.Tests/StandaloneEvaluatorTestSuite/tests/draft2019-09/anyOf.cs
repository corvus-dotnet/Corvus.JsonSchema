using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.AnyOf;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOf : IClassFixture<SuiteAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstAnyOfValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSecondAnyOfValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothAnyOfValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNeitherAnyOfValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.5");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [\r\n                {\r\n                    \"type\": \"integer\"\r\n                },\r\n                {\r\n                    \"minimum\": 2\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfWithBaseSchema : IClassFixture<SuiteAnyOfWithBaseSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfWithBaseSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMismatchBaseSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneAnyOfValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothAnyOfInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"string\",\r\n            \"anyOf\" : [\r\n                {\r\n                    \"maxLength\": 2\r\n                },\r\n                {\r\n                    \"minLength\": 4\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfWithBooleanSchemasAllTrue : IClassFixture<SuiteAnyOfWithBooleanSchemasAllTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfWithBooleanSchemasAllTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [true, true]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfWithBooleanSchemasSomeTrue : IClassFixture<SuiteAnyOfWithBooleanSchemasSomeTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfWithBooleanSchemasSomeTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [true, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfWithBooleanSchemasAllFalse : IClassFixture<SuiteAnyOfWithBooleanSchemasAllFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfWithBooleanSchemasAllFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [false, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfComplexTypes : IClassFixture<SuiteAnyOfComplexTypes.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfComplexTypes(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstAnyOfValidComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSecondAnyOfValidComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"baz\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothAnyOfValidComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"baz\", \"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNeitherAnyOfValidComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 2, \"bar\": \"quux\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": {\"type\": \"integer\"}\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"string\"}\r\n                    },\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnyOfWithOneEmptySchema : IClassFixture<SuiteAnyOfWithOneEmptySchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOfWithOneEmptySchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [\r\n                { \"type\": \"number\" },\r\n                {}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedAnyOfToCheckValidationSemantics : IClassFixture<SuiteNestedAnyOfToCheckValidationSemantics.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedAnyOfToCheckValidationSemantics(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNullIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnythingNonNullIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\anyOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"anyOf\": [\r\n                {\r\n                    \"anyOf\": [\r\n                        {\r\n                            \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AnyOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.AllOf;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOf : IClassFixture<SuiteAllOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"baz\", \"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"baz\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchFirst()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongType()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"baz\", \"bar\": \"quux\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": {\"type\": \"integer\"}\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"string\"}\r\n                    },\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithBaseSchema : IClassFixture<SuiteAllOfWithBaseSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithBaseSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"quux\", \"bar\": 2, \"baz\": null}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchBaseSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"quux\", \"baz\": null}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchFirstAllOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2, \"baz\": null}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchSecondAllOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"quux\", \"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchBoth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"bar\": {\"type\": \"integer\"}},\r\n            \"required\": [\"bar\"],\r\n            \"allOf\" : [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"string\"}\r\n                    },\r\n                    \"required\": [\"foo\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"baz\": {\"type\": \"null\"}\r\n                    },\r\n                    \"required\": [\"baz\"]\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfSimpleTypes : IClassFixture<SuiteAllOfSimpleTypes.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfSimpleTypes(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("25");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("35");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\"maximum\": 30},\r\n                {\"minimum\": 20}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithBooleanSchemasAllTrue : IClassFixture<SuiteAllOfWithBooleanSchemasAllTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithBooleanSchemasAllTrue(Fixture fixture)
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [true, true]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithBooleanSchemasSomeFalse : IClassFixture<SuiteAllOfWithBooleanSchemasSomeFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithBooleanSchemasSomeFalse(Fixture fixture)
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [true, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithBooleanSchemasAllFalse : IClassFixture<SuiteAllOfWithBooleanSchemasAllFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithBooleanSchemasAllFalse(Fixture fixture)
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [false, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithOneEmptySchema : IClassFixture<SuiteAllOfWithOneEmptySchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithOneEmptySchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyDataIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithTwoEmptySchemas : IClassFixture<SuiteAllOfWithTwoEmptySchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithTwoEmptySchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyDataIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {},\r\n                {}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithTheFirstEmptySchema : IClassFixture<SuiteAllOfWithTheFirstEmptySchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithTheFirstEmptySchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {},\r\n                { \"type\": \"number\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfWithTheLastEmptySchema : IClassFixture<SuiteAllOfWithTheLastEmptySchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfWithTheLastEmptySchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                { \"type\": \"number\" },\r\n                {}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedAllOfToCheckValidationSemantics : IClassFixture<SuiteNestedAllOfToCheckValidationSemantics.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedAllOfToCheckValidationSemantics(Fixture fixture)
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
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"allOf\": [\r\n                        {\r\n                            \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllOfCombinedWithAnyOfOneOf : IClassFixture<SuiteAllOfCombinedWithAnyOfOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOfCombinedWithAnyOfOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllOfFalseAnyOfFalseOneOfFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfFalseAnyOfFalseOneOfTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfFalseAnyOfTrueOneOfFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfFalseAnyOfTrueOneOfTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("15");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfTrueAnyOfFalseOneOfFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfTrueAnyOfFalseOneOfTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("10");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfTrueAnyOfTrueOneOfFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("6");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllOfTrueAnyOfTrueOneOfTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("30");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\allOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [ { \"multipleOf\": 2 } ],\r\n            \"anyOf\": [ { \"multipleOf\": 3 } ],\r\n            \"oneOf\": [ { \"multipleOf\": 5 } ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AllOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

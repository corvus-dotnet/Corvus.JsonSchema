using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsAsSchema : IClassFixture<SuiteAdditionalItemsAsSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAsSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalItemsMatchSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null, 2, 3, 4 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalItemsDoNotMatchSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null, 2, 3, \"foo\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}],\r\n            \"additionalItems\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing : IClassFixture<SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWithAArrayOfTypeIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidWithAArrayOfMixedTypes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,\"2\",\"3\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\":\"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {\r\n                \"type\": \"integer\"\r\n            },\r\n            \"additionalItems\": {\r\n                \"type\": \"string\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing : IClassFixture<SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllItemsMatchSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3, 4, 5 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {},\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteArrayOfItemsWithNoAdditionalItemsPermitted : IClassFixture<SuiteArrayOfItemsWithNoAdditionalItemsPermitted.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteArrayOfItemsWithNoAdditionalItemsPermitted(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFewerNumberOfItemsPresent1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFewerNumberOfItemsPresent2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEqualNumberOfItemsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAdditionalItemsAreNotPermitted()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3, 4 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}, {}, {}],\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsAsFalseWithoutItems : IClassFixture<SuiteAdditionalItemsAsFalseWithoutItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAsFalseWithoutItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestItemsDefaultsToEmptySchemaSoEverythingIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3, 4, 5 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsAreAllowedByDefault : IClassFixture<SuiteAdditionalItemsAreAllowedByDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAreAllowedByDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOnlyTheFirstItemIsValidated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"foo\", false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{\"type\": \"integer\"}]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase : IClassFixture<SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestItemsDefinedInAllOfAreNotExamined()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"hello\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                { \"items\": [ { \"type\": \"integer\" }, { \"type\": \"string\" } ] }\r\n            ],\r\n            \"items\": [ {\"type\": \"integer\" } ],\r\n            \"additionalItems\": { \"type\": \"boolean\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems : IClassFixture<SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"x\", 2, 3 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongTypeOfSecondItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"x\", \"y\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [ { \"type\": \"string\" } ],\r\n            \"additionalItems\": { \"type\": \"integer\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsWithHeterogeneousArray : IClassFixture<SuiteAdditionalItemsWithHeterogeneousArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsWithHeterogeneousArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestHeterogeneousInvalidInstance()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"foo\", \"bar\", 37 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidInstance()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}],\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAdditionalItemsWithNullInstanceElements : IClassFixture<SuiteAdditionalItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsWithNullInstanceElements(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullElements()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

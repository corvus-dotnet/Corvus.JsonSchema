using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.AdditionalItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"items\": [{}],\r\n            \"additionalItems\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing : IClassFixture<SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing(Fixture fixture)
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"items\": {},\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"items\": [{}, {}, {}],\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\"additionalItems\": false}",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\"items\": [{\"type\": \"integer\"}]}",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"allOf\": [\r\n                { \"items\": [ { \"type\": \"integer\" }, { \"type\": \"string\" } ] }\r\n            ],\r\n            \"items\": [ {\"type\": \"integer\" } ],\r\n            \"additionalItems\": { \"type\": \"boolean\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"items\": [ { \"type\": \"string\" } ],\r\n            \"additionalItems\": { \"type\": \"integer\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"items\": [{}],\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\additionalItems.json",
                "{\r\n            \"additionalItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Items;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteASchemaGivenForItems : IClassFixture<SuiteASchemaGivenForItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteASchemaGivenForItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongTypeOfItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"x\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"0\": \"invalid\",\r\n                    \"length\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteAnArrayOfSchemasForItems : IClassFixture<SuiteAnArrayOfSchemasForItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnArrayOfSchemasForItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCorrectTypes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongTypes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"foo\", 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIncompleteArrayOfItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\", true ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"0\": \"invalid\",\r\n                    \"1\": \"valid\",\r\n                    \"length\": 2\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteItemsAndSubitems : IClassFixture<SuiteItemsAndSubitems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsAndSubitems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooManyItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooManySubItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": null},\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongSubItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFewerItemsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null} ],\r\n                    [ {\"foo\": null} ]\r\n                ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\items.json",
                "{\r\n            \"definitions\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"additionalItems\": false,\r\n                    \"items\": [\r\n                        { \"$ref\": \"#/definitions/sub-item\" },\r\n                        { \"$ref\": \"#/definitions/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"additionalItems\": false,\r\n            \"items\": [\r\n                { \"$ref\": \"#/definitions/item\" },\r\n                { \"$ref\": \"#/definitions/item\" },\r\n                { \"$ref\": \"#/definitions/item\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteNestedItems : IClassFixture<SuiteNestedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidNestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[[1]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNestedArrayWithInvalidType()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[[\"1\"]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotDeepEnough()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[1], [2],[3]], [[4], [5], [6]]]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\items.json",
                "{\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteItemsWithNullInstanceElements : IClassFixture<SuiteItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteArrayFormItemsWithNullInstanceElements : IClassFixture<SuiteArrayFormItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteArrayFormItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsTrue : IClassFixture<SuiteUnevaluatedItemsTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsFalse : IClassFixture<SuiteUnevaluatedItemsFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsAsSchema : IClassFixture<SuiteUnevaluatedItemsAsSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsAsSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithValidUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithInvalidUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[42]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": { \"type\": \"string\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithUniformItems : IClassFixture<SuiteUnevaluatedItemsWithUniformItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithUniformItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnevaluatedItemsDoesnTApply()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": { \"type\": \"string\" },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithTuple : IClassFixture<SuiteUnevaluatedItemsWithTuple.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithTuple(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithItemsAndPrefixItems : IClassFixture<SuiteUnevaluatedItemsWithItemsAndPrefixItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithItemsAndPrefixItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnevaluatedItemsDoesnTApply()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"items\": true,\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithItems : IClassFixture<SuiteUnevaluatedItemsWithItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidUnderItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[5, 6, 7, 8]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidUnderItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\"type\": \"number\"},\r\n            \"unevaluatedItems\": {\"type\": \"string\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNestedTuple : IClassFixture<SuiteUnevaluatedItemsWithNestedTuple.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedTuple(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42, true]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"number\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNestedItems : IClassFixture<SuiteUnevaluatedItemsWithNestedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithOnlyValidAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"yes\", \"no\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithInvalidAdditionalItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"yes\", false]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": {\"type\": \"boolean\"},\r\n            \"anyOf\": [\r\n                { \"items\": {\"type\": \"string\"} },\r\n                true\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems : IClassFixture<SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"items\": true\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNestedUnevaluatedItems : IClassFixture<SuiteUnevaluatedItemsWithNestedUnevaluatedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedUnevaluatedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                },\r\n                { \"unevaluatedItems\": true }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithAnyOf : IClassFixture<SuiteUnevaluatedItemsWithAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenOneSchemaMatchesAndHasNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenOneSchemaMatchesAndHasUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", 42]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenTwoSchemasMatchAndHasNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenTwoSchemasMatchAndHasUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\", 42]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithOneOf : IClassFixture<SuiteUnevaluatedItemsWithOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", 42]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"oneOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNot : IClassFixture<SuiteUnevaluatedItemsWithNot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"not\": {\r\n                \"not\": {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                }\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithIfThenElse : IClassFixture<SuiteUnevaluatedItemsWithIfThenElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithIfThenElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenIfMatchesAndItHasNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"then\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfMatchesAndItHasUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"then\", \"else\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfDoesnTMatchAndItHasNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42, 42, \"else\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfDoesnTMatchAndItHasUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42, 42, \"else\", 42]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"if\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    { \"const\": \"bar\" }\r\n                ]\r\n            },\r\n            \"then\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"then\" }\r\n                ]\r\n            },\r\n            \"else\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"else\" }\r\n                ]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithBooleanSchemas : IClassFixture<SuiteUnevaluatedItemsWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [true],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithRef : IClassFixture<SuiteUnevaluatedItemsWithRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false,\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"prefixItems\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsBeforeRef : IClassFixture<SuiteUnevaluatedItemsBeforeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsBeforeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false,\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"prefixItems\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithDynamicRef : IClassFixture<SuiteUnevaluatedItemsWithDynamicRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithDynamicRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/unevaluated-items-with-dynamic-ref/derived\",\r\n\r\n            \"$ref\": \"./baseSchema\",\r\n\r\n            \"$defs\": {\r\n                \"derived\": {\r\n                    \"$dynamicAnchor\": \"addons\",\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                },\r\n                \"baseSchema\": {\r\n                    \"$id\": \"./baseSchema\",\r\n\r\n                    \"$comment\": \"unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering\",\r\n                    \"unevaluatedItems\": false,\r\n                    \"type\": \"array\",\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"$dynamicRef\": \"#addons\",\r\n\r\n                    \"$defs\": {\r\n                        \"defaultAddons\": {\r\n                            \"$comment\": \"Needed to satisfy the bookending requirement\",\r\n                            \"$dynamicAnchor\": \"addons\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsCanTSeeInsideCousins : IClassFixture<SuiteUnevaluatedItemsCanTSeeInsideCousins.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsCanTSeeInsideCousins(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAlwaysFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [ true ]\r\n                },\r\n                { \"unevaluatedItems\": false }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems : IClassFixture<SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoExtraItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": [\r\n                        \"test\"\r\n                    ]\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": [\r\n                        \"test\",\r\n                        \"test\"\r\n                    ]\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"unevaluatedItems\": false\r\n                  }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"prefixItems\": [\r\n                                true,\r\n                                { \"type\": \"string\" }\r\n                            ]\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsDependsOnAdjacentContains : IClassFixture<SuiteUnevaluatedItemsDependsOnAdjacentContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsDependsOnAdjacentContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSecondItemIsEvaluatedByContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsFailsSecondItemIsNotEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsPassesSecondItemIsNotEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, \"foo\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [true],\r\n            \"contains\": {\"type\": \"string\"},\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsDependsOnMultipleNestedContains : IClassFixture<SuiteUnevaluatedItemsDependsOnMultipleNestedContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsDependsOnMultipleNestedContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test5NotEvaluatedPassesUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 2, 3, 4, 5, 6 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test7NotEvaluatedFailsUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 2, 3, 4, 7, 8 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                { \"contains\": { \"multipleOf\": 2 } },\r\n                { \"contains\": { \"multipleOf\": 3 } }\r\n            ],\r\n            \"unevaluatedItems\": { \"multipleOf\": 5 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship : IClassFixture<SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyASAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"a\", \"a\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestASAndBSAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"a\", \"b\", \"a\", \"b\", \"a\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestASBSAndCSAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"c\", \"a\", \"c\", \"c\", \"b\", \"a\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyBSAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"b\", \"b\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyCSAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"c\", \"c\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyBSAndCSAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"c\", \"b\", \"c\", \"b\", \"c\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyASAndCSAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"c\", \"a\", \"c\", \"a\", \"c\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"contains\": {\"const\": \"a\"}\r\n            },\r\n            \"then\": {\r\n                \"if\": {\r\n                    \"contains\": {\"const\": \"b\"}\r\n                },\r\n                \"then\": {\r\n                    \"if\": {\r\n                        \"contains\": {\"const\": \"c\"}\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithMinContains0 : IClassFixture<SuiteUnevaluatedItemsWithMinContains0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithMinContains0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoItemsEvaluatedByContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeButNotAllItemsEvaluatedByContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 0]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllItemsEvaluatedByContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"type\": \"string\"},\r\n            \"minContains\": 0,\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteNonArrayInstancesAreValid : IClassFixture<SuiteNonArrayInstancesAreValid.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonArrayInstancesAreValid(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIgnoresBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsWithNullInstanceElements : IClassFixture<SuiteUnevaluatedItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse : IClassFixture<SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"a\" ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"b\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"prefixItems\": [{\"const\": \"a\"}]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation : IClassFixture<SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithAnUnevaluatedItemThatExistsAtAnotherLocation()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [\"foo\", \"bar\"],\r\n                    \"bar\"\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

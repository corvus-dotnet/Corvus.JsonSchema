using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": { \"type\": \"string\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": { \"type\": \"string\" },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsWithItemsAndAdditionalItems : IClassFixture<SuiteUnevaluatedItemsWithItemsAndAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithItemsAndAdditionalItems(Fixture fixture)
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"additionalItems\": true,\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsWithIgnoredAdditionalItems : IClassFixture<SuiteUnevaluatedItemsWithIgnoredAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithIgnoredAdditionalItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidUnderUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllValidUnderUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalItems\": {\"type\": \"number\"},\r\n            \"unevaluatedItems\": {\"type\": \"string\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsWithIgnoredApplicatorAdditionalItems : IClassFixture<SuiteUnevaluatedItemsWithIgnoredApplicatorAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithIgnoredApplicatorAdditionalItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidUnderUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllValidUnderUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [ { \"additionalItems\": { \"type\": \"number\" } } ],\r\n            \"unevaluatedItems\": {\"type\": \"string\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"allOf\": [\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"type\": \"number\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": {\"type\": \"boolean\"},\r\n            \"anyOf\": [\r\n                { \"items\": {\"type\": \"string\"} },\r\n                true\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsWithNestedItemsAndAdditionalItems : IClassFixture<SuiteUnevaluatedItemsWithNestedItemsAndAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedItemsAndAdditionalItems(Fixture fixture)
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"items\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"additionalItems\": true\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"items\": [\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                },\r\n                { \"unevaluatedItems\": true }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"oneOf\": [\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"not\": {\r\n                \"not\": {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                }\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [ { \"const\": \"foo\" } ],\r\n            \"if\": {\r\n                \"items\": [\r\n                    true,\r\n                    { \"const\": \"bar\" }\r\n                ]\r\n            },\r\n            \"then\": {\r\n                \"items\": [\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"then\" }\r\n                ]\r\n            },\r\n            \"else\": {\r\n                \"items\": [\r\n                    true,\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"else\" }\r\n                ]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [true],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"items\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false,\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"items\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": false,\r\n            \"items\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"items\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsWithRecursiveRef : IClassFixture<SuiteUnevaluatedItemsWithRecursiveRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithRecursiveRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, [2, [], \"b\"], \"a\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, [2, [], \"b\", \"too many\"], \"a\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/unevaluated-items-with-recursive-ref/extended-tree\",\r\n\r\n            \"$recursiveAnchor\": true,\r\n\r\n            \"$ref\": \"./tree\",\r\n            \"items\": [\r\n                true,\r\n                true,\r\n                { \"type\": \"string\" }\r\n            ],\r\n\r\n            \"$defs\": {\r\n                \"tree\": {\r\n                    \"$id\": \"./tree\",\r\n                    \"$recursiveAnchor\": true,\r\n\r\n                    \"type\": \"array\",\r\n                    \"items\": [\r\n                        { \"type\": \"number\" },\r\n                        {\r\n                            \"$comment\": \"unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering\",\r\n                            \"unevaluatedItems\": false,\r\n                            \"$recursiveRef\": \"#\"\r\n                        }\r\n                    ]\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"items\": [ true ]\r\n                },\r\n                { \"unevaluatedItems\": false }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"items\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"unevaluatedItems\": false\r\n                  }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"items\": [\r\n                                true,\r\n                                { \"type\": \"string\" }\r\n                            ]\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"items\": [{\"const\": \"a\"}]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                {\r\n                    \"items\": [\r\n                        true,\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

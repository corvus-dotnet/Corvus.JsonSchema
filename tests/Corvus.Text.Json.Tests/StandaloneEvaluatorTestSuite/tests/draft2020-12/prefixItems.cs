using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.PrefixItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteASchemaGivenForPrefixItems : IClassFixture<SuiteASchemaGivenForPrefixItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteASchemaGivenForPrefixItems(Fixture fixture)
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
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PrefixItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePrefixItemsWithBooleanSchemas : IClassFixture<SuitePrefixItemsWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithOneItemIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithTwoItemsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\" ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [true, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PrefixItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
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
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"integer\"}]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PrefixItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePrefixItemsWithNullInstanceElements : IClassFixture<SuitePrefixItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.PrefixItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

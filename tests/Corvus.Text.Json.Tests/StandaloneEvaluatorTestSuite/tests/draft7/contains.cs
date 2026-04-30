using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Contains;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsKeywordValidation : IClassFixture<SuiteContainsKeywordValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithItemMatchingSchema5IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[3, 4, 5]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithItemMatchingSchema6IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[3, 4, 6]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithTwoItemsMatchingSchema56IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[3, 4, 5, 6]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithoutItemsMatchingSchemaIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[2, 3, 4]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotArrayIsValid()
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
                "tests\\draft7\\contains.json",
                "{\r\n            \"contains\": {\"minimum\": 5}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsKeywordWithConstKeyword : IClassFixture<SuiteContainsKeywordWithConstKeyword.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithConstKeyword(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithItem5IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[3, 4, 5]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithTwoItems5IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[3, 4, 5, 5]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithoutItem5IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\contains.json",
                "{\r\n            \"contains\": { \"const\": 5 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsKeywordWithBooleanSchemaTrue : IClassFixture<SuiteContainsKeywordWithBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\contains.json",
                "{\"contains\": true}",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsKeywordWithBooleanSchemaFalse : IClassFixture<SuiteContainsKeywordWithBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonArraysAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"contains does not apply to strings\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\contains.json",
                "{\"contains\": false}",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteItemsContains : IClassFixture<SuiteItemsContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesItemsDoesNotMatchContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 2, 4, 8 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesNotMatchItemsMatchesContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 3, 6, 9 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesBothItemsAndContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 6, 12 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesNeitherItemsNorContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 5 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\contains.json",
                "{\r\n            \"items\": { \"multipleOf\": 2 },\r\n            \"contains\": { \"multipleOf\": 3 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsWithFalseIfSubschema : IClassFixture<SuiteContainsWithFalseIfSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsWithFalseIfSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\contains.json",
                "{\r\n            \"contains\": {\r\n                \"if\": false,\r\n                \"else\": true\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteContainsWithNullInstanceElements : IClassFixture<SuiteContainsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsWithNullInstanceElements(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullItems()
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
                "tests\\draft7\\contains.json",
                "{\r\n            \"contains\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Contains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

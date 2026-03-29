using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.MaxContains;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaxContainsWithoutContainsIsIgnored : IClassFixture<SuiteMaxContainsWithoutContainsIsIgnored.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithoutContainsIsIgnored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneItemValidAgainstLoneMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoItemsStillValidAgainstLoneMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"maxContains\": 1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MaxContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaxContainsWithContains : IClassFixture<SuiteMaxContainsWithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchValidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchInvalidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeElementsMatchValidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeElementsMatchInvalidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MaxContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaxContainsWithContainsValueWithADecimal : IClassFixture<SuiteMaxContainsWithContainsValueWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithContainsValueWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneElementMatchesValidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooManyElementsMatchInvalidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1.0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MaxContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContainsMaxContains : IClassFixture<SuiteMinContainsMaxContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContainsMaxContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestActualMinContainsMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMinContainsActualMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMinContainsMaxContainsActual()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1, 1, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 1,\r\n            \"maxContains\": 3\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MaxContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

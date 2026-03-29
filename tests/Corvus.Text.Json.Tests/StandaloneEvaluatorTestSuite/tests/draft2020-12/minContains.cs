using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.MinContains;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContainsWithoutContainsIsIgnored : IClassFixture<SuiteMinContainsWithoutContainsIsIgnored.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContainsWithoutContainsIsIgnored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneItemValidAgainstLoneMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroItemsStillValidAgainstLoneMinContains()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minContains\": 1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContains1WithContains : IClassFixture<SuiteMinContains1WithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains1WithContains(Fixture fixture)
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
    public void TestNoElementsMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 2 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleElementMatchesValidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeElementsMatchValidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchValidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContains2WithContains : IClassFixture<SuiteMinContains2WithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains2WithContains(Fixture fixture)
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
    public void TestAllElementsMatchInvalidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeElementsMatchInvalidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchValidMinContainsExactlyAsNeeded()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchValidMinContainsMoreThanNeeded()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomeElementsMatchValidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 2\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContains2WithContainsWithADecimalValue : IClassFixture<SuiteMinContains2WithContainsWithADecimalValue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains2WithContainsWithADecimalValue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneElementMatchesInvalidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBothElementsMatchValidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 2.0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaxContainsMinContains : IClassFixture<SuiteMaxContainsMinContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsMinContains(Fixture fixture)
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
    public void TestAllElementsMatchInvalidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchInvalidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllElementsMatchValidMaxContainsAndMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 2,\r\n            \"minContains\": 2\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaxContainsMinContains1 : IClassFixture<SuiteMaxContainsMinContains1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsMinContains1(Fixture fixture)
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
    public void TestInvalidMinContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 1, 1 ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidMaxContainsAndMinContains()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1,\r\n            \"minContains\": 3\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContains0 : IClassFixture<SuiteMinContains0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMinContains0MakesContainsAlwaysPass()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 2 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMinContains0WithMaxContains : IClassFixture<SuiteMinContains0WithMaxContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains0WithMaxContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotMoreThanMaxContains()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooMany()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 0,\r\n            \"maxContains\": 1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MinContains",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.MultipleOf;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteByInt : IClassFixture<SuiteByInt.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteByInt(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntByInt()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("10");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntByIntFail()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("7");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonNumbers()
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
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 2\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteByNumber : IClassFixture<SuiteByNumber.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteByNumber(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestZeroIsMultipleOfAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test45IsMultipleOf15()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4.5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test35IsNotMultipleOf15()
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
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 1.5\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteBySmallNumber : IClassFixture<SuiteBySmallNumber.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBySmallNumber(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test00075IsMultipleOf00001()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.0075");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test000751IsNotMultipleOf00001()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.00751");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 0.0001\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteFloatDivisionInf : IClassFixture<SuiteFloatDivisionInf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatDivisionInf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAlwaysInvalidButNaiveImplementationsMayRaiseAnOverflowError()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1e308");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\", \"multipleOf\": 0.123456789\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSmallMultipleOfLargeInteger : IClassFixture<SuiteSmallMultipleOfLargeInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSmallMultipleOfLargeInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyIntegerIsAMultipleOf1e8()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12391239123");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\", \"multipleOf\": 1e-8\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

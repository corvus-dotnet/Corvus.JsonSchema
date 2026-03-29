using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.MultipleOf;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\multipleOf.json",
                "{\"multipleOf\": 2}",
                "StandaloneEvaluatorTestSuite.Draft4.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\multipleOf.json",
                "{\"multipleOf\": 1.5}",
                "StandaloneEvaluatorTestSuite.Draft4.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\multipleOf.json",
                "{\"multipleOf\": 0.0001}",
                "StandaloneEvaluatorTestSuite.Draft4.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteFloatDivisionInf : IClassFixture<SuiteFloatDivisionInf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatDivisionInf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidButNaiveImplementationsMayRaiseAnOverflowError()
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
                "tests\\draft4\\multipleOf.json",
                "{\"type\": \"integer\", \"multipleOf\": 0.123456789}",
                "StandaloneEvaluatorTestSuite.Draft4.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
                "tests\\draft4\\multipleOf.json",
                "{\"type\": \"integer\", \"multipleOf\": 1e-8}",
                "StandaloneEvaluatorTestSuite.Draft4.MultipleOf",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

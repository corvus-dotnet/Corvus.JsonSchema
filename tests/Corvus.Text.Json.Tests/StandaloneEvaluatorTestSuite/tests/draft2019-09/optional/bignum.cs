using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteInteger : IClassFixture<SuiteInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12345678910111213141516171819202122232425262728293031");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANegativeBignumIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-12345678910111213141516171819202122232425262728293031");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"integer\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNumber : IClassFixture<SuiteNumber.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNumber(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("98249283749234923498293171823948729348710298301928331");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANegativeBignumIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-98249283749234923498293171823948729348710298301928331");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"number\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteString : IClassFixture<SuiteString.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteString(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("98249283749234923498293171823948729348710298301928331");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"string\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteMaximumIntegerComparison : IClassFixture<SuiteMaximumIntegerComparison.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumIntegerComparison(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForHighNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("18446744073709551600");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"maximum\": 18446744073709551615\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteFloatComparisonWithHighPrecision : IClassFixture<SuiteFloatComparisonWithHighPrecision.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatComparisonWithHighPrecision(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForHighNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("972783798187987123879878123.188781371");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"exclusiveMaximum\": 972783798187987123879878123.18878137\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteMinimumIntegerComparison : IClassFixture<SuiteMinimumIntegerComparison.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumIntegerComparison(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-18446744073709551600");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"minimum\": -18446744073709551615\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers : IClassFixture<SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-972783798187987123879878123.188781371");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\bignum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"exclusiveMinimum\": -972783798187987123879878123.18878137\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

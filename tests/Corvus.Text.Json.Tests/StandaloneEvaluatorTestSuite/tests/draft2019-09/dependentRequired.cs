using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.DependentRequired;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteSingleDependency : IClassFixture<SuiteSingleDependency.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSingleDependency(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNeither()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNondependant()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\dependentRequired.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependentRequired\": {\"bar\": [\"foo\"]}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteEmptyDependents : IClassFixture<SuiteEmptyDependents.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEmptyDependents(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithOneProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonObjectIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\dependentRequired.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependentRequired\": {\"bar\": []}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteMultipleDependentsRequired : IClassFixture<SuiteMultipleDependentsRequired.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleDependentsRequired(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNeither()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNondependants()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithDependencies()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2, \"quux\": 3}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"quux\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingOtherDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 1, \"quux\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingBothDependencies()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": 1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\dependentRequired.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependentRequired\": {\"quux\": [\"foo\", \"bar\"]}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteDependenciesWithEscapedCharacters : IClassFixture<SuiteDependenciesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependenciesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCrlf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\rbar\": 2\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestQuotedQuotes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo'bar\": 1,\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCrlfMissingDependent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\": 2\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestQuotedQuotesMissingDependent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\dependentRequired.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependentRequired\": {\r\n                \"foo\\nbar\": [\"foo\\rbar\"],\r\n                \"foo\\\"bar\": [\"foo'bar\"]\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

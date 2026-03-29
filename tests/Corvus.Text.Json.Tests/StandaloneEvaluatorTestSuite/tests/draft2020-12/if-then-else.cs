using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.IfThenElse;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIgnoreIfWithoutThenOrElse : IClassFixture<SuiteIgnoreIfWithoutThenOrElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreIfWithoutThenOrElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneIf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneIf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIgnoreThenWithoutIf : IClassFixture<SuiteIgnoreThenWithoutIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreThenWithoutIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"then\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIgnoreElseWithoutIf : IClassFixture<SuiteIgnoreElseWithoutIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreElseWithoutIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"else\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIfAndThenWithoutElse : IClassFixture<SuiteIfAndThenWithoutElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAndThenWithoutElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidWhenIfTestFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIfAndElseWithoutThen : IClassFixture<SuiteIfAndElseWithoutThen.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAndElseWithoutThen(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenIfTestPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidateAgainstCorrectBranchThenVsElse : IClassFixture<SuiteValidateAgainstCorrectBranchThenVsElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidateAgainstCorrectBranchThenVsElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteNonInterferenceAcrossCombinedSchemas : IClassFixture<SuiteNonInterferenceAcrossCombinedSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonInterferenceAcrossCombinedSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidButWouldHaveBeenInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidButWouldHaveBeenInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"if\": {\r\n                        \"exclusiveMaximum\": 0\r\n                    }\r\n                },\r\n                {\r\n                    \"then\": {\r\n                        \"minimum\": -10\r\n                    }\r\n                },\r\n                {\r\n                    \"else\": {\r\n                        \"multipleOf\": 2\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIfWithBooleanSchemaTrue : IClassFixture<SuiteIfWithBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfWithBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"then\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"else\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": true,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIfWithBooleanSchemaFalse : IClassFixture<SuiteIfWithBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfWithBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"then\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"else\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": false,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence : IClassFixture<SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestYesRedirectsToThenAndPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"yes\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOtherRedirectsToElseAndPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"other\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoRedirectsToThenAndFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"no\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidRedirectsToElseAndFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"invalid\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"then\": { \"const\": \"yes\" },\r\n            \"else\": { \"const\": \"other\" },\r\n            \"if\": { \"maxLength\": 4 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteThenFalseFailsWhenConditionMatches : IClassFixture<SuiteThenFalseFailsWhenConditionMatches.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteThenFalseFailsWhenConditionMatches(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesIfThenFalseInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesNotMatchIfThenIgnoredValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"then\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteElseFalseFailsWhenConditionDoesNotMatch : IClassFixture<SuiteElseFalseFailsWhenConditionDoesNotMatch.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteElseFalseFailsWhenConditionDoesNotMatch(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesIfElseIgnoredValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesNotMatchIfElseExecutesInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"else\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

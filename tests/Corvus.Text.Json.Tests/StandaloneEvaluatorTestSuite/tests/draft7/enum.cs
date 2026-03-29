using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Enum;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteSimpleEnumValidation : IClassFixture<SuiteSimpleEnumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSimpleEnumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneOfTheEnumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [1, 2, 3]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteHeterogeneousEnumValidation : IClassFixture<SuiteHeterogeneousEnumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteHeterogeneousEnumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneOfTheEnumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectsAreDeepCompared()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": false}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidObjectMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExtraPropertiesInObjectIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12, \"boo\": 42}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [6, \"foo\", [], true, {\"foo\": 12}]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteHeterogeneousEnumWithNullValidation : IClassFixture<SuiteHeterogeneousEnumWithNullValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteHeterogeneousEnumWithNullValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNullIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("6");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{ \"enum\": [6, null] }",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumsInProperties : IClassFixture<SuiteEnumsInProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumsInProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBothPropertiesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":\"foo\", \"bar\":\"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongFooValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":\"foot\", \"bar\":\"bar\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongBarValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":\"foo\", \"bar\":\"bart\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingOptionalPropertyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\":\"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingRequiredPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":\"foo\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingAllPropertiesIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\r\n            \"type\":\"object\",\r\n            \"properties\": {\r\n                \"foo\": {\"enum\":[\"foo\"]},\r\n                \"bar\": {\"enum\":[\"bar\"]}\r\n            },\r\n            \"required\": [\"bar\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWithEscapedCharacters : IClassFixture<SuiteEnumWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMember1IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\\nbar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMember2IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\\rbar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\r\n            \"enum\": [\"foo\\nbar\", \"foo\\rbar\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWithFalseDoesNotMatch0 : IClassFixture<SuiteEnumWithFalseDoesNotMatch0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWithFalseDoesNotMatch0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntegerZeroIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatZeroIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [false]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWithFalseDoesNotMatch01 : IClassFixture<SuiteEnumWithFalseDoesNotMatch01.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWithFalseDoesNotMatch01(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test0IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test00IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0.0]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[false]]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWithTrueDoesNotMatch1 : IClassFixture<SuiteEnumWithTrueDoesNotMatch1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWithTrueDoesNotMatch1(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntegerOneIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatOneIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [true]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWithTrueDoesNotMatch11 : IClassFixture<SuiteEnumWithTrueDoesNotMatch11.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWithTrueDoesNotMatch11(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test1IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test10IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[true]]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWith0DoesNotMatchFalse : IClassFixture<SuiteEnumWith0DoesNotMatchFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWith0DoesNotMatchFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntegerZeroIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatZeroIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [0]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWith0DoesNotMatchFalse1 : IClassFixture<SuiteEnumWith0DoesNotMatchFalse1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWith0DoesNotMatchFalse1(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test0IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test00IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0.0]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[0]]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWith1DoesNotMatchTrue : IClassFixture<SuiteEnumWith1DoesNotMatchTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWith1DoesNotMatchTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestTrueIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntegerOneIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatOneIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [1]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteEnumWith1DoesNotMatchTrue1 : IClassFixture<SuiteEnumWith1DoesNotMatchTrue1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEnumWith1DoesNotMatchTrue1(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestTrueIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test1IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test10IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[1]]}",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteNulCharactersInStrings : IClassFixture<SuiteNulCharactersInStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNulCharactersInStrings(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchStringWithNul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\\u0000there\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoNotMatchStringLackingNul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hellothere\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\enum.json",
                "{ \"enum\": [ \"hello\\u0000there\" ] }",
                "StandaloneEvaluatorTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

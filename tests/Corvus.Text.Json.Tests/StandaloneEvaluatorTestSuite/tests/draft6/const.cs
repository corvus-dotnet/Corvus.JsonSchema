using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.Const;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstValidation : IClassFixture<SuiteConstValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSameValueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherValueIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherTypeIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": 2}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithObject : IClassFixture<SuiteConstWithObject.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithObject(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSameObjectIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\", \"baz\": \"bax\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSameObjectWithDifferentPropertyOrderIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"baz\": \"bax\", \"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherObjectIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherTypeIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": {\"foo\": \"bar\", \"baz\": \"bax\"}}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithArray : IClassFixture<SuiteConstWithArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSameArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherArrayItemIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[2]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArrayWithAdditionalItemsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": [{ \"foo\": \"bar\" }]}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithNull : IClassFixture<SuiteConstWithNull.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithNull(Fixture fixture)
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
    public void TestNotNullIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": null}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithFalseDoesNotMatch0 : IClassFixture<SuiteConstWithFalseDoesNotMatch0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithFalseDoesNotMatch0(Fixture fixture)
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
                "tests\\draft6\\const.json",
                "{\"const\": false}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithTrueDoesNotMatch1 : IClassFixture<SuiteConstWithTrueDoesNotMatch1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithTrueDoesNotMatch1(Fixture fixture)
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
                "tests\\draft6\\const.json",
                "{\"const\": true}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithFalseDoesNotMatch01 : IClassFixture<SuiteConstWithFalseDoesNotMatch01.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithFalseDoesNotMatch01(Fixture fixture)
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
                "tests\\draft6\\const.json",
                "{\"const\": [false]}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithTrueDoesNotMatch11 : IClassFixture<SuiteConstWithTrueDoesNotMatch11.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithTrueDoesNotMatch11(Fixture fixture)
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
                "tests\\draft6\\const.json",
                "{\"const\": [true]}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithAFalseDoesNotMatchA0 : IClassFixture<SuiteConstWithAFalseDoesNotMatchA0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithAFalseDoesNotMatchA0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": false}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestA0IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 0}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestA00IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 0.0}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": {\"a\": false}}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWithATrueDoesNotMatchA1 : IClassFixture<SuiteConstWithATrueDoesNotMatchA1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWithATrueDoesNotMatchA1(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestATrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": true}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestA1IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestA10IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 1.0}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": {\"a\": true}}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWith0DoesNotMatchOtherZeroLikeTypes : IClassFixture<SuiteConstWith0DoesNotMatchOtherZeroLikeTypes.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWith0DoesNotMatchOtherZeroLikeTypes(Fixture fixture)
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

    [Fact]
    public void TestEmptyObjectIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": 0}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWith1DoesNotMatchTrue : IClassFixture<SuiteConstWith1DoesNotMatchTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWith1DoesNotMatchTrue(Fixture fixture)
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
                "tests\\draft6\\const.json",
                "{\"const\": 1}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteConstWith20MatchesIntegerAndFloatTypes : IClassFixture<SuiteConstWith20MatchesIntegerAndFloatTypes.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteConstWith20MatchesIntegerAndFloatTypes(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInteger2IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInteger2IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloat20IsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloat20IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloat200001IsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.00001");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": -2.0}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteFloatAndIntegersAreEqualUpTo64BitRepresentationLimits : IClassFixture<SuiteFloatAndIntegersAreEqualUpTo64BitRepresentationLimits.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatAndIntegersAreEqualUpTo64BitRepresentationLimits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("9007199254740992");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntegerMinusOneIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("9007199254740991");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("9007199254740992.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatMinusOneIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("9007199254740991.0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\"const\": 9007199254740992}",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\const.json",
                "{ \"const\": \"hello\\u0000there\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteCharactersWithTheSameVisualRepresentationButDifferentCodepoint : IClassFixture<SuiteCharactersWithTheSameVisualRepresentationButDifferentCodepoint.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteCharactersWithTheSameVisualRepresentationButDifferentCodepoint(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCharacterUsesTheSameCodepoint()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"μ\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCharacterLooksTheSameButUsesADifferentCodepoint()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"µ\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\r\n            \"const\": \"μ\",\r\n            \"$comment\": \"U+03BC\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteCharactersWithTheSameVisualRepresentationButDifferentNumberOfCodepoints : IClassFixture<SuiteCharactersWithTheSameVisualRepresentationButDifferentNumberOfCodepoints.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteCharactersWithTheSameVisualRepresentationButDifferentNumberOfCodepoints(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCharacterUsesTheSameCodepoint()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ä\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCharacterLooksTheSameButUsesCombiningMarks()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ä\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\const.json",
                "{\r\n            \"const\": \"ä\",\r\n            \"$comment\": \"U+00E4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Const",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

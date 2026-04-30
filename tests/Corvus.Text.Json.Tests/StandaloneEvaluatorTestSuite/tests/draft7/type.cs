using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Type;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteIntegerTypeMatchesIntegers : IClassFixture<SuiteIntegerTypeMatchesIntegers.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIntegerTypeMatchesIntegers(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsStillNotAnIntegerEvenIfItLooksLikeOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"integer\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteNumberTypeMatchesNumbers : IClassFixture<SuiteNumberTypeMatchesNumbers.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNumberTypeMatchesNumbers(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsANumberAndAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsStillNotANumberEvenIfItLooksLikeOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsNotANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"number\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteStringTypeMatchesStrings : IClassFixture<SuiteStringTypeMatchesStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteStringTypeMatchesStrings(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test1IsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsStillAStringEvenIfItLooksLikeANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnEmptyStringIsStillAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"string\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteObjectTypeMatchesObjects : IClassFixture<SuiteObjectTypeMatchesObjects.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteObjectTypeMatchesObjects(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotAnObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"object\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteArrayTypeMatchesArrays : IClassFixture<SuiteArrayTypeMatchesArrays.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteArrayTypeMatchesArrays(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotAnArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"array\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteBooleanTypeMatchesBooleans : IClassFixture<SuiteBooleanTypeMatchesBooleans.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBooleanTypeMatchesBooleans(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnEmptyStringIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueIsABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseIsABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNotABoolean()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"boolean\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteNullTypeMatchesOnlyTheNullObject : IClassFixture<SuiteNullTypeMatchesOnlyTheNullObject.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNullTypeMatchesOnlyTheNullObject(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnEmptyStringIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseIsNotNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": \"null\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteMultipleTypesCanBeSpecifiedInAnArray : IClassFixture<SuiteMultipleTypesCanBeSpecifiedInAnArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleTypesCanBeSpecifiedInAnArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFloatIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnObjectIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABooleanIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\"type\": [\"integer\", \"string\"]}",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteTypeAsArrayWithOneItem : IClassFixture<SuiteTypeAsArrayWithOneItem.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTypeAsArrayWithOneItem(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\r\n            \"type\": [\"string\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteTypeArrayOrObject : IClassFixture<SuiteTypeArrayOrObject.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTypeArrayOrObject(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 123}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\r\n            \"type\": [\"array\", \"object\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteTypeArrayObjectOrNull : IClassFixture<SuiteTypeArrayObjectOrNull.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTypeArrayObjectOrNull(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 123}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\type.json",
                "{\r\n            \"type\": [\"array\", \"object\", \"null\"]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

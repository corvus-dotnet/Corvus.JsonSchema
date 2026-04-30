using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Type;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsStillNotAnIntegerEvenIfItLooksLikeOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsANumberAndAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsStillNotANumberEvenIfItLooksLikeOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"number\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsStillAStringEvenIfItLooksLikeANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmptyStringIsStillAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmptyStringIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueIsABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseIsABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotABoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"boolean\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmptyStringIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseIsNotNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"null\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": [\"integer\", \"string\"]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("123");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": [\"string\"]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1,2,3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 123}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("123");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": [\"array\", \"object\"]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1,2,3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 123}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("123");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\type.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": [\"array\", \"object\", \"null\"]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Type",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

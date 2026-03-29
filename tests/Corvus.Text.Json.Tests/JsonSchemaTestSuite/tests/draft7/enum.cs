using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Enum;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("4");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [1, 2, 3]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectsAreDeepCompared()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": false}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidObjectMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtraPropertiesInObjectIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12, \"boo\": 42}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [6, \"foo\", [], true, {\"foo\": 12}]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("6");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomethingElseIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"test\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{ \"enum\": [6, null] }",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\":\"foo\", \"bar\":\"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongFooValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\":\"foot\", \"bar\":\"bar\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongBarValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\":\"foo\", \"bar\":\"bart\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingOptionalPropertyIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\":\"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingRequiredPropertyIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\":\"foo\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingAllPropertiesIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\r\n            \"type\":\"object\",\r\n            \"properties\": {\r\n                \"foo\": {\"enum\":[\"foo\"]},\r\n                \"bar\": {\"enum\":[\"bar\"]}\r\n            },\r\n            \"required\": [\"bar\"]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\\nbar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMember2IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\\rbar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnotherStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\r\n            \"enum\": [\"foo\\nbar\", \"foo\\rbar\"]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerZeroIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFloatZeroIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [false]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test0IsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test00IsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0.0]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[false]]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerOneIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFloatOneIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [true]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test1IsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test10IsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1.0]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[true]]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerZeroIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFloatZeroIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [0]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test0IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test00IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0.0]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[0]]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerOneIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFloatOneIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [1]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test1IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test10IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1.0]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{\"enum\": [[1]]}",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hello\\u0000there\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoNotMatchStringLackingNul()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hellothere\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\enum.json",
                "{ \"enum\": [ \"hello\\u0000there\" ] }",
                "JsonSchemaTestSuite.Draft7.Enum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.MultipleOf;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("10");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntByIntFail()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("7");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MultipleOf",
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test45IsMultipleOf15()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("4.5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test35IsNotMultipleOf15()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("35");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 1.5\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MultipleOf",
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.0075");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test000751IsNotMultipleOf00001()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.00751");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"multipleOf\": 0.0001\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MultipleOf",
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1e308");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\", \"multipleOf\": 0.123456789\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MultipleOf",
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12391239123");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\multipleOf.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\", \"multipleOf\": 1e-8\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MultipleOf",
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

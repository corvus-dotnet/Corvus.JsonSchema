using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaAdditionalTests.Draft202012;

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class TypeAndFormat : IClassFixture<TypeAndFormat.Fixture>
{
    private readonly Fixture _fixture;

    public TypeAndFormat(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestA64BitIntegerIsNotAnInteger32()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3000000000");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsStillNotAnIntegerEvenIfItLooksLikeOne()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2020-12\\typeAndFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "type": "integer",
                    "format": "int32"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.TypeAndFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class MultiTypeAndFormat : IClassFixture<MultiTypeAndFormat.Fixture>
{
    private readonly Fixture _fixture;

    public MultiTypeAndFormat(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }


    [Fact]
    public void TestA64BitIntegerIsNotAnInteger32()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3000000000");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsAllowed()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }


    [Fact]
    public void TestAnObjectIsAllowed()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2019-09\\multiTypeAndFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2019-09/schema",
                    "type": ["integer", "string", "object"],
                    "format": "int32"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.MultiTypeAndFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class MultiTypeDifferentFormat : IClassFixture<MultiTypeDifferentFormat.Fixture>
{
    private readonly Fixture _fixture;

    public MultiTypeDifferentFormat(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFloatIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAStringIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmailStringIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo@example.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnObjectIsAllowed()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnArrayIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestABooleanIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2019-09\\multiTypeDifferentFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2019-09/schema",
                    "type": ["integer", "object"],
                    "format": "email"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.MultiTypeDifferentFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
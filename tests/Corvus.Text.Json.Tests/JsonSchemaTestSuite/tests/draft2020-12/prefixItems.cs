using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.PrefixItems;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteASchemaGivenForPrefixItems : IClassFixture<SuiteASchemaGivenForPrefixItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteASchemaGivenForPrefixItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCorrectTypes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongTypes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"foo\", 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIncompleteArrayOfItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"0\": \"invalid\",\r\n                    \"1\": \"valid\",\r\n                    \"length\": 2\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuitePrefixItemsWithBooleanSchemas : IClassFixture<SuitePrefixItemsWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithOneItemIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithTwoItemsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [true, false]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuiteAdditionalItemsAreAllowedByDefault : IClassFixture<SuiteAdditionalItemsAreAllowedByDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAreAllowedByDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOnlyTheFirstItemIsValidated()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, \"foo\", false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"integer\"}]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuitePrefixItemsWithNullInstanceElements : IClassFixture<SuitePrefixItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsWithNullInstanceElements(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullElements()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ null ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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

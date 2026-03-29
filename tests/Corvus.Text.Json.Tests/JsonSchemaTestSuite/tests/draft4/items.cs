using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft4.Items;

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteASchemaGivenForItems : IClassFixture<SuiteASchemaGivenForItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteASchemaGivenForItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 3 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongTypeOfItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, \"x\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"0\": \"invalid\",\r\n                    \"length\": 1\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteAnArrayOfSchemasForItems : IClassFixture<SuiteAnArrayOfSchemasForItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnArrayOfSchemasForItems(Fixture fixture)
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
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteItemsAndSubitems : IClassFixture<SuiteItemsAndSubitems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsAndSubitems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooManyItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooManySubItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongItem()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": null},\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongSubItem()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [ {}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFewerItemsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null} ],\r\n                    [ {\"foo\": null} ]\r\n                ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\items.json",
                "{\r\n            \"definitions\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"additionalItems\": false,\r\n                    \"items\": [\r\n                        { \"$ref\": \"#/definitions/sub-item\" },\r\n                        { \"$ref\": \"#/definitions/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"additionalItems\": false,\r\n            \"items\": [\r\n                { \"$ref\": \"#/definitions/item\" },\r\n                { \"$ref\": \"#/definitions/item\" },\r\n                { \"$ref\": \"#/definitions/item\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteNestedItems : IClassFixture<SuiteNestedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidNestedArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[[[1]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNestedArrayWithInvalidType()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[[[\"1\"]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotDeepEnough()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[[1], [2],[3]], [[4], [5], [6]]]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\items.json",
                "{\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteItemsWithNullInstanceElements : IClassFixture<SuiteItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteArrayFormItemsWithNullInstanceElements : IClassFixture<SuiteArrayFormItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteArrayFormItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft4\\items.json",
                "{\r\n            \"items\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft4.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Items;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuiteItemsWithBooleanSchemaTrue : IClassFixture<SuiteItemsWithBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsWithBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
        Assert.True(dynamicInstance.EvaluateSchema());
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuiteItemsWithBooleanSchemaFalse : IClassFixture<SuiteItemsWithBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsWithBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": false,\r\n                    \"prefixItems\": [\r\n                        { \"$ref\": \"#/$defs/sub-item\" },\r\n                        { \"$ref\": \"#/$defs/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"items\": false,\r\n            \"prefixItems\": [\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuitePrefixItemsWithNoAdditionalItemsAllowed : IClassFixture<SuitePrefixItemsWithNoAdditionalItemsAllowed.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsWithNoAdditionalItemsAllowed(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFewerNumberOfItemsPresent1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFewerNumberOfItemsPresent2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEqualNumberOfItemsPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 3 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalItemsAreNotPermitted()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 3, 4 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}, {}, {}],\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuiteItemsDoesNotLookInApplicatorsValidCase : IClassFixture<SuiteItemsDoesNotLookInApplicatorsValidCase.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsDoesNotLookInApplicatorsValidCase(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPrefixItemsInAllOfDoesNotConstrainItemsInvalidCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 3, 5 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPrefixItemsInAllOfDoesNotConstrainItemsValidCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 5, 5 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                { \"prefixItems\": [ { \"minimum\": 3 } ] }\r\n            ],\r\n            \"items\": { \"minimum\": 5 }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuitePrefixItemsValidationAdjustsTheStartingIndexForItems : IClassFixture<SuitePrefixItemsValidationAdjustsTheStartingIndexForItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePrefixItemsValidationAdjustsTheStartingIndexForItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"x\", 2, 3 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongTypeOfSecondItem()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"x\", \"y\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [ { \"type\": \"string\" } ],\r\n            \"items\": { \"type\": \"integer\" }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
public class SuiteItemsWithHeterogeneousArray : IClassFixture<SuiteItemsWithHeterogeneousArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsWithHeterogeneousArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestHeterogeneousInvalidInstance()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"foo\", \"bar\", 37 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidInstance()
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}],\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
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

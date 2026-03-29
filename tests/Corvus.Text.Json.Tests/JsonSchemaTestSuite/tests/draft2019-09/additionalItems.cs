using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.AdditionalItems;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalItemsAsSchema : IClassFixture<SuiteAdditionalItemsAsSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAsSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalItemsMatchSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ null, 2, 3, 4 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalItemsDoNotMatchSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ null, 2, 3, \"foo\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}],\r\n            \"additionalItems\": {\"type\": \"integer\"}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing : IClassFixture<SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWhenItemsIsSchemaAdditionalItemsDoesNothing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWithAArrayOfTypeIntegers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1,2,3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidWithAArrayOfMixedTypes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1,\"2\",\"3\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\":\"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {\r\n                \"type\": \"integer\"\r\n            },\r\n            \"additionalItems\": {\r\n                \"type\": \"string\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing : IClassFixture<SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWhenItemsIsSchemaBooleanAdditionalItemsDoesNothing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllItemsMatchSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 3, 4, 5 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {},\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteArrayOfItemsWithNoAdditionalItemsPermitted : IClassFixture<SuiteArrayOfItemsWithNoAdditionalItemsPermitted.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteArrayOfItemsWithNoAdditionalItemsPermitted(Fixture fixture)
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
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}, {}, {}],\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalItemsAsFalseWithoutItems : IClassFixture<SuiteAdditionalItemsAsFalseWithoutItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsAsFalseWithoutItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestItemsDefaultsToEmptySchemaSoEverythingIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 3, 4, 5 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{\"type\": \"integer\"}]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase : IClassFixture<SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsDoesNotLookInApplicatorsInvalidCase(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestItemsDefinedInAllOfAreNotExamined()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"hello\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                { \"items\": [ { \"type\": \"integer\" }, { \"type\": \"string\" } ] }\r\n            ],\r\n            \"items\": [ {\"type\": \"integer\" } ],\r\n            \"additionalItems\": { \"type\": \"boolean\" }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems : IClassFixture<SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsValidationAdjustsTheStartingIndexForAdditionalItems(Fixture fixture)
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
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [ { \"type\": \"string\" } ],\r\n            \"additionalItems\": { \"type\": \"integer\" }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalItemsWithHeterogeneousArray : IClassFixture<SuiteAdditionalItemsWithHeterogeneousArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsWithHeterogeneousArray(Fixture fixture)
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
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [{}],\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalItemsWithNullInstanceElements : IClassFixture<SuiteAdditionalItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft2019-09\\additionalItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

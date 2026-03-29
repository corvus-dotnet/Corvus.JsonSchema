using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.UnevaluatedItems;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteUnevaluatedItemsTrue : IClassFixture<SuiteUnevaluatedItemsTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsFalse : IClassFixture<SuiteUnevaluatedItemsFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsAsSchema : IClassFixture<SuiteUnevaluatedItemsAsSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsAsSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithValidUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithInvalidUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[42]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": { \"type\": \"string\" }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithUniformItems : IClassFixture<SuiteUnevaluatedItemsWithUniformItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithUniformItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnevaluatedItemsDoesnTApply()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": { \"type\": \"string\" },\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithTuple : IClassFixture<SuiteUnevaluatedItemsWithTuple.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithTuple(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithItemsAndPrefixItems : IClassFixture<SuiteUnevaluatedItemsWithItemsAndPrefixItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithItemsAndPrefixItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnevaluatedItemsDoesnTApply()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"items\": true,\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithItems : IClassFixture<SuiteUnevaluatedItemsWithItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidUnderItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[5, 6, 7, 8]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidUnderItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\"type\": \"number\"},\r\n            \"unevaluatedItems\": {\"type\": \"string\"}\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedTuple : IClassFixture<SuiteUnevaluatedItemsWithNestedTuple.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedTuple(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"number\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedItems : IClassFixture<SuiteUnevaluatedItemsWithNestedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithOnlyValidAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"yes\", \"no\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithInvalidAdditionalItem()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"yes\", false]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": {\"type\": \"boolean\"},\r\n            \"anyOf\": [\r\n                { \"items\": {\"type\": \"string\"} },\r\n                true\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems : IClassFixture<SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"items\": true\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedUnevaluatedItems : IClassFixture<SuiteUnevaluatedItemsWithNestedUnevaluatedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNestedUnevaluatedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithAdditionalItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                },\r\n                { \"unevaluatedItems\": true }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithAnyOf : IClassFixture<SuiteUnevaluatedItemsWithAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenOneSchemaMatchesAndHasNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenOneSchemaMatchesAndHasUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", 42]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenTwoSchemasMatchAndHasNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenTwoSchemasMatchAndHasUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\", 42]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithOneOf : IClassFixture<SuiteUnevaluatedItemsWithOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", 42]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"oneOf\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                },\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"baz\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNot : IClassFixture<SuiteUnevaluatedItemsWithNot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"not\": {\r\n                \"not\": {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"const\": \"bar\" }\r\n                    ]\r\n                }\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithIfThenElse : IClassFixture<SuiteUnevaluatedItemsWithIfThenElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithIfThenElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenIfMatchesAndItHasNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"then\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenIfMatchesAndItHasUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"then\", \"else\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenIfDoesnTMatchAndItHasNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42, 42, \"else\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWhenIfDoesnTMatchAndItHasUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 42, 42, \"else\", 42]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                { \"const\": \"foo\" }\r\n            ],\r\n            \"if\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    { \"const\": \"bar\" }\r\n                ]\r\n            },\r\n            \"then\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"then\" }\r\n                ]\r\n            },\r\n            \"else\": {\r\n                \"prefixItems\": [\r\n                    true,\r\n                    true,\r\n                    true,\r\n                    { \"const\": \"else\" }\r\n                ]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithBooleanSchemas : IClassFixture<SuiteUnevaluatedItemsWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [true],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithRef : IClassFixture<SuiteUnevaluatedItemsWithRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"unevaluatedItems\": false,\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"prefixItems\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsBeforeRef : IClassFixture<SuiteUnevaluatedItemsBeforeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsBeforeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false,\r\n            \"prefixItems\": [\r\n                { \"type\": \"string\" }\r\n            ],\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"$defs\": {\r\n              \"bar\": {\r\n                  \"prefixItems\": [\r\n                      true,\r\n                      { \"type\": \"string\" }\r\n                  ]\r\n              }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithDynamicRef : IClassFixture<SuiteUnevaluatedItemsWithDynamicRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithDynamicRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/unevaluated-items-with-dynamic-ref/derived\",\r\n\r\n            \"$ref\": \"./baseSchema\",\r\n\r\n            \"$defs\": {\r\n                \"derived\": {\r\n                    \"$dynamicAnchor\": \"addons\",\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                },\r\n                \"baseSchema\": {\r\n                    \"$id\": \"./baseSchema\",\r\n\r\n                    \"$comment\": \"unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering\",\r\n                    \"unevaluatedItems\": false,\r\n                    \"type\": \"array\",\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"$dynamicRef\": \"#addons\",\r\n\r\n                    \"$defs\": {\r\n                        \"defaultAddons\": {\r\n                            \"$comment\": \"Needed to satisfy the bookending requirement\",\r\n                            \"$dynamicAnchor\": \"addons\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsCanTSeeInsideCousins : IClassFixture<SuiteUnevaluatedItemsCanTSeeInsideCousins.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsCanTSeeInsideCousins(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAlwaysFails()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"prefixItems\": [ true ]\r\n                },\r\n                { \"unevaluatedItems\": false }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems : IClassFixture<SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoExtraItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": [\r\n                        \"test\"\r\n                    ]\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": [\r\n                        \"test\",\r\n                        \"test\"\r\n                    ]\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"prefixItems\": [\r\n                        { \"type\": \"string\" }\r\n                    ],\r\n                    \"unevaluatedItems\": false\r\n                  }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"prefixItems\": [\r\n                                true,\r\n                                { \"type\": \"string\" }\r\n                            ]\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsDependsOnAdjacentContains : IClassFixture<SuiteUnevaluatedItemsDependsOnAdjacentContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsDependsOnAdjacentContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSecondItemIsEvaluatedByContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsFailsSecondItemIsNotEvaluated()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsPassesSecondItemIsNotEvaluated()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, \"foo\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [true],\r\n            \"contains\": {\"type\": \"string\"},\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsDependsOnMultipleNestedContains : IClassFixture<SuiteUnevaluatedItemsDependsOnMultipleNestedContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsDependsOnMultipleNestedContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test5NotEvaluatedPassesUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 2, 3, 4, 5, 6 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test7NotEvaluatedFailsUnevaluatedItems()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 2, 3, 4, 7, 8 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                { \"contains\": { \"multipleOf\": 2 } },\r\n                { \"contains\": { \"multipleOf\": 3 } }\r\n            ],\r\n            \"unevaluatedItems\": { \"multipleOf\": 5 }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship : IClassFixture<SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyASAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"a\", \"a\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestASAndBSAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"a\", \"b\", \"a\", \"b\", \"a\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestASBSAndCSAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"c\", \"a\", \"c\", \"c\", \"b\", \"a\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyBSAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"b\", \"b\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyCSAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"c\", \"c\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyBSAndCSAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"c\", \"b\", \"c\", \"b\", \"c\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyASAndCSAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"c\", \"a\", \"c\", \"a\", \"c\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"contains\": {\"const\": \"a\"}\r\n            },\r\n            \"then\": {\r\n                \"if\": {\r\n                    \"contains\": {\"const\": \"b\"}\r\n                },\r\n                \"then\": {\r\n                    \"if\": {\r\n                        \"contains\": {\"const\": \"c\"}\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithMinContains0 : IClassFixture<SuiteUnevaluatedItemsWithMinContains0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithMinContains0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoItemsEvaluatedByContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeButNotAllItemsEvaluatedByContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", 0]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllItemsEvaluatedByContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"type\": \"string\"},\r\n            \"minContains\": 0,\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteNonArrayInstancesAreValid : IClassFixture<SuiteNonArrayInstancesAreValid.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonArrayInstancesAreValid(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIgnoresBooleans()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresIntegers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("123");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresFloats()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNull()
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
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNullInstanceElements : IClassFixture<SuiteUnevaluatedItemsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsWithNullInstanceElements(Fixture fixture)
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
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedItems\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse : IClassFixture<SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidInCaseIfIsEvaluated()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"a\" ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ \"b\" ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"prefixItems\": [{\"const\": \"a\"}]\r\n            },\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation : IClassFixture<SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithAnUnevaluatedItemThatExistsAtAnotherLocation()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    [\"foo\", \"bar\"],\r\n                    \"bar\"\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\r\n                    \"prefixItems\": [\r\n                        true,\r\n                        { \"type\": \"string\" }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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

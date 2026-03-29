using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.RecursiveRef;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteRecursiveRefWithoutRecursiveAnchorWorksLikeRef : IClassFixture<SuiteRecursiveRefWithoutRecursiveAnchorWorksLikeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithoutRecursiveAnchorWorksLikeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": false}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRecursiveMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"foo\": false } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"bar\": false }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRecursiveMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": false } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"$recursiveRef\": \"#\" }\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithoutUsingNesting : IClassFixture<SuiteRecursiveRefWithoutUsingNesting.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithoutUsingNesting(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsNoMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef2/schema.json\",\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithNesting : IClassFixture<SuiteRecursiveRefWithNesting.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithNesting(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerNowMatchesAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsPropertiesMatchWithRecursiveRef()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef3/schema.json\",\r\n            \"$recursiveAnchor\": true,\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithRecursiveAnchorFalseWorksLikeRef : IClassFixture<SuiteRecursiveRefWithRecursiveAnchorFalseWorksLikeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithRecursiveAnchorFalseWorksLikeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef4/schema.json\",\r\n            \"$recursiveAnchor\": false,\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": false,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithNoRecursiveAnchorWorksLikeRef : IClassFixture<SuiteRecursiveRefWithNoRecursiveAnchorWorksLikeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithNoRecursiveAnchorWorksLikeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef5/schema.json\",\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": false,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithNoRecursiveAnchorInTheInitialTargetSchemaResource : IClassFixture<SuiteRecursiveRefWithNoRecursiveAnchorInTheInitialTargetSchemaResource.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithNoRecursiveAnchorInTheInitialTargetSchemaResource(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLeafNodeDoesNotMatchNoRecursion()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": true }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeafNodeMatchesRecursionUsesTheInnerSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeafNodeDoesNotMatchRecursionUsesTheInnerSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": true } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/base.json\",\r\n            \"$recursiveAnchor\": true,\r\n            \"anyOf\": [\r\n                { \"type\": \"boolean\" },\r\n                {\r\n                    \"type\": \"object\",\r\n                    \"additionalProperties\": {\r\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/inner.json\",\r\n                        \"$comment\": \"there is no $recursiveAnchor: true here, so we do NOT recurse to the base\",\r\n                        \"anyOf\": [\r\n                            { \"type\": \"integer\" },\r\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\r\n                        ]\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteRecursiveRefWithNoRecursiveAnchorInTheOuterSchemaResource : IClassFixture<SuiteRecursiveRefWithNoRecursiveAnchorInTheOuterSchemaResource.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveRefWithNoRecursiveAnchorInTheOuterSchemaResource(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLeafNodeDoesNotMatchNoRecursion()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": true }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeafNodeMatchesRecursionOnlyUsesInnerSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeafNodeDoesNotMatchRecursionOnlyUsesInnerSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": true } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/base.json\",\r\n            \"anyOf\": [\r\n                { \"type\": \"boolean\" },\r\n                {\r\n                    \"type\": \"object\",\r\n                    \"additionalProperties\": {\r\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/inner.json\",\r\n                        \"$recursiveAnchor\": true,\r\n                        \"anyOf\": [\r\n                            { \"type\": \"integer\" },\r\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\r\n                        ]\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteMultipleDynamicPathsToTheRecursiveRefKeyword : IClassFixture<SuiteMultipleDynamicPathsToTheRecursiveRefKeyword.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleDynamicPathsToTheRecursiveRefKeyword(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRecurseToAnyLeafNodeFloatsAreAllowed()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"alpha\": 1.1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRecurseToIntegerNodeFloatsAreNotAllowed()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"november\": 1.1 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/recursiveRef8_main.json\",\r\n            \"$defs\": {\r\n                \"inner\": {\r\n                    \"$id\": \"recursiveRef8_inner.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"title\": \"inner\",\r\n                    \"additionalProperties\": {\r\n                        \"$recursiveRef\": \"#\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"propertyNames\": {\r\n                    \"pattern\": \"^[a-m]\"\r\n                }\r\n            },\r\n            \"then\": {\r\n                \"title\": \"any type of node\",\r\n                \"$id\": \"recursiveRef8_anyLeafNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"$ref\": \"recursiveRef8_inner.json\"\r\n            },\r\n            \"else\": {\r\n                \"title\": \"integer node\",\r\n                \"$id\": \"recursiveRef8_integerNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"type\": [ \"object\", \"integer\" ],\r\n                \"$ref\": \"recursiveRef8_inner.json\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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
public class SuiteDynamicRecursiveRefDestinationNotPredictableAtSchemaCompileTime : IClassFixture<SuiteDynamicRecursiveRefDestinationNotPredictableAtSchemaCompileTime.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDynamicRecursiveRefDestinationNotPredictableAtSchemaCompileTime(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumericNode()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"alpha\": 1.1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntegerNode()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"november\": 1.1 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/main.json\",\r\n            \"$defs\": {\r\n                \"inner\": {\r\n                    \"$id\": \"inner.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"title\": \"inner\",\r\n                    \"additionalProperties\": {\r\n                        \"$recursiveRef\": \"#\"\r\n                    }\r\n                }\r\n\r\n            },\r\n            \"if\": { \"propertyNames\": { \"pattern\": \"^[a-m]\" } },\r\n            \"then\": {\r\n                \"title\": \"any type of node\",\r\n                \"$id\": \"anyLeafNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"$ref\": \"main.json#/$defs/inner\"\r\n            },\r\n            \"else\": {\r\n                \"title\": \"integer node\",\r\n                \"$id\": \"integerNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"type\": [ \"object\", \"integer\" ],\r\n                \"$ref\": \"main.json#/$defs/inner\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
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

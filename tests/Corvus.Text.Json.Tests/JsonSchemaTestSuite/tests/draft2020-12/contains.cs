using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Contains;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteContainsKeywordValidation : IClassFixture<SuiteContainsKeywordValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithItemMatchingSchema5IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[3, 4, 5]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithItemMatchingSchema6IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[3, 4, 6]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithTwoItemsMatchingSchema56IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[3, 4, 5, 6]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithoutItemsMatchingSchemaIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[2, 3, 4]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"minimum\": 5}\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteContainsKeywordWithConstKeyword : IClassFixture<SuiteContainsKeywordWithConstKeyword.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithConstKeyword(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestArrayWithItem5IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[3, 4, 5]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithTwoItems5IsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[3, 4, 5, 5]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArrayWithoutItem5IsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3, 4]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": { \"const\": 5 }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteContainsKeywordWithBooleanSchemaTrue : IClassFixture<SuiteContainsKeywordWithBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteContainsKeywordWithBooleanSchemaFalse : IClassFixture<SuiteContainsKeywordWithBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsKeywordWithBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonArraysAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"contains does not apply to strings\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteItemsContains : IClassFixture<SuiteItemsContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteItemsContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesItemsDoesNotMatchContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 2, 4, 8 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesNotMatchItemsMatchesContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 3, 6, 9 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesBothItemsAndContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 6, 12 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesNeitherItemsNorContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 5 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": { \"multipleOf\": 2 },\r\n            \"contains\": { \"multipleOf\": 3 }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteContainsWithFalseIfSubschema : IClassFixture<SuiteContainsWithFalseIfSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsWithFalseIfSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyNonEmptyArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyArrayIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\r\n                \"if\": false,\r\n                \"else\": true\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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
public class SuiteContainsWithNullInstanceElements : IClassFixture<SuiteContainsWithNullInstanceElements.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContainsWithNullInstanceElements(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullItems()
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
                "tests\\draft2020-12\\contains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Contains",
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

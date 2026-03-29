using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.MaxContains;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteMaxContainsWithoutContainsIsIgnored : IClassFixture<SuiteMaxContainsWithoutContainsIsIgnored.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithoutContainsIsIgnored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneItemValidAgainstLoneMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoItemsStillValidAgainstLoneMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"maxContains\": 1\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxContains",
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
public class SuiteMaxContainsWithContains : IClassFixture<SuiteMaxContainsWithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchValidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchInvalidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeElementsMatchValidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeElementsMatchInvalidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxContains",
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
public class SuiteMaxContainsWithContainsValueWithADecimal : IClassFixture<SuiteMaxContainsWithContainsValueWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsWithContainsValueWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneElementMatchesValidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooManyElementsMatchInvalidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1.0\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxContains",
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
public class SuiteMinContainsMaxContains : IClassFixture<SuiteMinContainsMaxContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContainsMaxContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestActualMinContainsMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMinContainsActualMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMinContainsMaxContainsActual()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1, 1, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\maxContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 1,\r\n            \"maxContains\": 3\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxContains",
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

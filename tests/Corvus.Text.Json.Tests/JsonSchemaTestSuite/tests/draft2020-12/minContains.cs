using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.MinContains;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteMinContainsWithoutContainsIsIgnored : IClassFixture<SuiteMinContainsWithoutContainsIsIgnored.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContainsWithoutContainsIsIgnored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneItemValidAgainstLoneMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroItemsStillValidAgainstLoneMinContains()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minContains\": 1\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains1WithContains : IClassFixture<SuiteMinContains1WithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains1WithContains(Fixture fixture)
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
    public void TestNoElementsMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 2 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleElementMatchesValidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeElementsMatchValidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchValidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 1\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains2WithContains : IClassFixture<SuiteMinContains2WithContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains2WithContains(Fixture fixture)
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
    public void TestAllElementsMatchInvalidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeElementsMatchInvalidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchValidMinContainsExactlyAsNeeded()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchValidMinContainsMoreThanNeeded()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSomeElementsMatchValidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 2, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains2WithContainsWithADecimalValue : IClassFixture<SuiteMinContains2WithContainsWithADecimalValue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains2WithContainsWithADecimalValue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestOneElementMatchesInvalidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBothElementsMatchValidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 2.0\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMaxContainsMinContains : IClassFixture<SuiteMaxContainsMinContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsMinContains(Fixture fixture)
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
    public void TestAllElementsMatchInvalidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchInvalidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllElementsMatchValidMaxContainsAndMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 2,\r\n            \"minContains\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMaxContainsMinContains1 : IClassFixture<SuiteMaxContainsMinContains1.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxContainsMinContains1(Fixture fixture)
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
    public void TestInvalidMinContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidMaxContainsAndMinContains()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"maxContains\": 1,\r\n            \"minContains\": 3\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains0 : IClassFixture<SuiteMinContains0.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains0(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMinContains0MakesContainsAlwaysPass()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 2 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 0\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains0WithMaxContains : IClassFixture<SuiteMinContains0WithMaxContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinContains0WithMaxContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyData()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotMoreThanMaxContains()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooMany()
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
                "tests\\draft2020-12\\minContains.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contains\": {\"const\": 1},\r\n            \"minContains\": 0,\r\n            \"maxContains\": 1\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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

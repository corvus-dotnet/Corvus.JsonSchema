using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteProperUtf16SurrogatePairHandlingPattern : IClassFixture<SuiteProperUtf16SurrogatePairHandlingPattern.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteProperUtf16SurrogatePairHandlingPattern(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesEmpty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesSingle()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"🐲\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"🐲🐲\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"🐉\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"🐉🐉\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchOneAscii()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"D\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchTwoAscii()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"DD\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\non-bmp-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"pattern\": \"^🐲*$\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex",
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
public class SuiteProperUtf16SurrogatePairHandlingPatternProperties : IClassFixture<SuiteProperUtf16SurrogatePairHandlingPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteProperUtf16SurrogatePairHandlingPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesEmpty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesSingle()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"🐲\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"🐲🐲\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"🐲\": \"hello\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTMatchTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"🐲🐲\": \"hello\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\non-bmp-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"patternProperties\": {\r\n                \"^🐲*$\": {\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex",
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

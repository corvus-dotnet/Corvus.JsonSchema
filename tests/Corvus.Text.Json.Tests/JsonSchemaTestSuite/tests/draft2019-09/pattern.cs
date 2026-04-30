using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Pattern;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuitePatternValidation : IClassFixture<SuitePatternValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAMatchingPatternIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"aaa\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonMatchingPatternIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
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
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
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
                "tests\\draft2019-09\\pattern.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"pattern\": \"^a*$\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Pattern",
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
public class SuitePatternIsNotAnchored : IClassFixture<SuitePatternIsNotAnchored.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternIsNotAnchored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesASubstring()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xxaayy\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\pattern.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"pattern\": \"a+\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Pattern",
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

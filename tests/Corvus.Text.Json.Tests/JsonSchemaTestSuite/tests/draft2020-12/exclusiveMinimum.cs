using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.ExclusiveMinimum;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteExclusiveMinimumValidation : IClassFixture<SuiteExclusiveMinimumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteExclusiveMinimumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAboveTheExclusiveMinimumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.2");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBelowTheExclusiveMinimumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.6");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"x\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\exclusiveMinimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"exclusiveMinimum\": 1.1\r\n        }",
                "JsonSchemaTestSuite.Draft202012.ExclusiveMinimum",
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

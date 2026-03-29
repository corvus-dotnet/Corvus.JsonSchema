using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.ExclusiveMinimum;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
                "tests\\draft7\\exclusiveMinimum.json",
                "{\r\n            \"exclusiveMinimum\": 1.1\r\n        }",
                "JsonSchemaTestSuite.Draft7.ExclusiveMinimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

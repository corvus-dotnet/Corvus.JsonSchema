using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.ExclusiveMaximum;

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteExclusiveMaximumValidation : IClassFixture<SuiteExclusiveMaximumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteExclusiveMaximumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheExclusiveMaximumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("2.2");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3.0");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAboveTheExclusiveMaximumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3.5");
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
                "tests\\draft6\\exclusiveMaximum.json",
                "{\r\n            \"exclusiveMaximum\": 3.0\r\n        }",
                "JsonSchemaTestSuite.Draft6.ExclusiveMaximum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

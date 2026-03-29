using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.NoSchema;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteValidationWithoutSchema : IClassFixture<SuiteValidationWithoutSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationWithoutSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestA3CharacterStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestA1CharacterStringIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\no-schema.json",
                "{\r\n            \"minLength\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.NoSchema",
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

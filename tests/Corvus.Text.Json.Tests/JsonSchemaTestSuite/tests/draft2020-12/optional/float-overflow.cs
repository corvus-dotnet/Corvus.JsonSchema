using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.FloatOverflow;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled : IClassFixture<SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidIfOptionalOverflowHandlingIsImplemented()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1e308");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\float-overflow.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"integer\",\r\n            \"multipleOf\": 0.5\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.FloatOverflow",
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

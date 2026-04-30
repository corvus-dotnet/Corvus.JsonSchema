using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.Optional.FloatOverflow;

[Trait("JsonSchemaTestSuite", "Draft6")]
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
                "tests\\draft6\\optional\\float-overflow.json",
                "{\"type\": \"integer\", \"multipleOf\": 0.5}",
                "JsonSchemaTestSuite.Draft6.Optional.FloatOverflow",
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

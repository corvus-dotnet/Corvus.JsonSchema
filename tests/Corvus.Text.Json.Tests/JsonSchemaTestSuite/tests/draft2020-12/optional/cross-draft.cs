using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.CrossDraft;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts : IClassFixture<SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstItemNotAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2019-09/ignore-prefixItems.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.CrossDraft",
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

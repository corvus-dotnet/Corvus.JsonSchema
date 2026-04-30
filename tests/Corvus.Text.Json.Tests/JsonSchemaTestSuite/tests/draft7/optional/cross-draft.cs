using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.CrossDraft;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts : IClassFixture<SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMissingBarIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"any value\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPresentBarIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"any value\", \"bar\": \"also any value\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft2019-09/dependentRequired.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.CrossDraft",
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

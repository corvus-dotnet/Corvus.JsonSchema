using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.EcmascriptRegex;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteAIsNotAnEcma262ControlEscape : IClassFixture<SuiteAIsNotAnEcma262ControlEscape.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAIsNotAnEcma262ControlEscape(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenUsedAsAPattern()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\ecmascript-regex.json",
                "{\r\n      \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n      \"format\": \"regex\"\r\n    }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

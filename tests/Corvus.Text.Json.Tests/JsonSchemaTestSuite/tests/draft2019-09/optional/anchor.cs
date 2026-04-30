using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.Anchor;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifier : IClassFixture<SuiteAnchorInsideAnEnumIsNotARealIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnchorInsideAnEnumIsNotARealIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"$anchor\": \"my_anchor\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInImplementationsThatStripAnchorThisMayMatchEitherDef()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"type\": \"null\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchRefToAnchor()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string to match #/$defs/anchor_in_enum\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoMatchOnEnumOrRefToAnchor()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"anchor_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_identifier_in_schema\": {\r\n                    \"$anchor\": \"my_anchor\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_anchor_in_const\": {\r\n                    \"const\": {\r\n                        \"$anchor\": \"my_anchor\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/$defs/anchor_in_enum\" },\r\n                { \"$ref\": \"#my_anchor\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Anchor",
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

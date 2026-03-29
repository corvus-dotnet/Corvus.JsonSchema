using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Id;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteIdInsideAnEnumIsNotARealIdentifier : IClassFixture<SuiteIdInsideAnEnumIsNotARealIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdInsideAnEnumIsNotARealIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchRefToId()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string to match #/definitions/id_in_enum\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoMatchOnEnumOrRefToId()
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                          \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                          \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_id_in_const\": {\r\n                    \"const\": {\r\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_enum\" },\r\n                { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Id",
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

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteNonSchemaObjectContainingAPlainNameIdProperty : IClassFixture<SuiteNonSchemaObjectContainingAPlainNameIdProperty.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonSchemaObjectContainingAPlainNameIdProperty(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"skip not_a_real_anchor\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestConstAtConstNotAnchorDoesNotMatch()
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"const_not_anchor\": {\r\n                    \"const\": {\r\n                        \"$id\": \"#not_a_real_anchor\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"const\": \"skip not_a_real_anchor\"\r\n            },\r\n            \"then\": true,\r\n            \"else\" : {\r\n                \"$ref\": \"#/definitions/const_not_anchor\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Id",
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

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteNonSchemaObjectContainingAnIdProperty : IClassFixture<SuiteNonSchemaObjectContainingAnIdProperty.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonSchemaObjectContainingAnIdProperty(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"skip not_a_real_id\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestConstAtConstNotIdDoesNotMatch()
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"const_not_id\": {\r\n                    \"const\": {\r\n                        \"$id\": \"not_a_real_id\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"const\": \"skip not_a_real_id\"\r\n            },\r\n            \"then\": true,\r\n            \"else\" : {\r\n                \"$ref\": \"#/definitions/const_not_id\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Id",
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

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Vocabulary;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestApplicatorVocabularyStillWorks()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"badProperty\": \"this property should not exist\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoValidationValidNumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"numberProperty\": 20\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"numberProperty\": 1\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\vocabulary.json",
                "{\r\n            \"$id\": \"https://schema/using/no/validation\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/metaschema-no-validation.json\",\r\n            \"properties\": {\r\n                \"badProperty\": false,\r\n                \"numberProperty\": {\r\n                    \"minimum\": 10\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Vocabulary",
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

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteIgnoreUnrecognizedOptionalVocabulary : IClassFixture<SuiteIgnoreUnrecognizedOptionalVocabulary.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreUnrecognizedOptionalVocabulary(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foobar\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumberValue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("20");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\vocabulary.json",
                "{\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/metaschema-optional-vocabulary.json\",\r\n            \"type\": \"number\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Vocabulary",
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

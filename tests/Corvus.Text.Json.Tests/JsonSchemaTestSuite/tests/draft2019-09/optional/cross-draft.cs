using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.CrossDraft;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts : IClassFixture<SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstItemNotAStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFirstItemIsAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"a string\", 1, 2, 3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/prefixItems.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.CrossDraft",
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

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts : IClassFixture<SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMissingBarIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"any value\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft7/ignore-dependentRequired.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.CrossDraft",
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

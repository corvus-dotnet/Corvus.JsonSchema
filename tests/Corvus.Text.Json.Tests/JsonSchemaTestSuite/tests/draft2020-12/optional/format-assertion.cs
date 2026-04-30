using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFormatAssertionFalseValidString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFormatAssertionFalseInvalidString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"not-an-ipv4\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/false\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-false.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion",
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

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFormatAssertionTrueValidString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFormatAssertionTrueInvalidString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"not-an-ipv4\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/true\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-true.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion",
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

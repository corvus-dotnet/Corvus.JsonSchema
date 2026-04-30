using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.MaxItems;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteMaxItemsValidation : IClassFixture<SuiteMaxItemsValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxItemsValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestShorterIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExactLengthIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooLongIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foobar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\maxItems.json",
                "{\"maxItems\": 2}",
                "JsonSchemaTestSuite.Draft7.MaxItems",
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
public class SuiteMaxItemsValidationWithADecimal : IClassFixture<SuiteMaxItemsValidationWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxItemsValidationWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestShorterIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooLongIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\maxItems.json",
                "{\"maxItems\": 2.0}",
                "JsonSchemaTestSuite.Draft7.MaxItems",
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

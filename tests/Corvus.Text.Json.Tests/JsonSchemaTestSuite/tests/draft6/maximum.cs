using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.Maximum;

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteMaximumValidation : IClassFixture<SuiteMaximumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheMaximumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("2.6");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAboveTheMaximumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3.5");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"x\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\maximum.json",
                "{\"maximum\": 3.0}",
                "JsonSchemaTestSuite.Draft6.Maximum",
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

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteMaximumValidationWithUnsignedInteger : IClassFixture<SuiteMaximumValidationWithUnsignedInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumValidationWithUnsignedInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheMaximumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("299.97");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIntegerIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("300");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointFloatIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("300.00");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAboveTheMaximumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("300.5");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\maximum.json",
                "{\"maximum\": 300}",
                "JsonSchemaTestSuite.Draft6.Maximum",
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

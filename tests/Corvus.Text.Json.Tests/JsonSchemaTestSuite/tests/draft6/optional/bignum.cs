using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.Optional.Bignum;

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteInteger : IClassFixture<SuiteInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12345678910111213141516171819202122232425262728293031");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANegativeBignumIsAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-12345678910111213141516171819202122232425262728293031");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{ \"type\": \"integer\" }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteNumber : IClassFixture<SuiteNumber.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNumber(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("98249283749234923498293171823948729348710298301928331");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANegativeBignumIsANumber()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-98249283749234923498293171823948729348710298301928331");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{ \"type\": \"number\" }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteString : IClassFixture<SuiteString.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteString(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestABignumIsNotAString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("98249283749234923498293171823948729348710298301928331");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{ \"type\": \"string\" }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteMaximumIntegerComparison : IClassFixture<SuiteMaximumIntegerComparison.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumIntegerComparison(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForHighNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("18446744073709551600");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{ \"maximum\": 18446744073709551615 }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteFloatComparisonWithHighPrecision : IClassFixture<SuiteFloatComparisonWithHighPrecision.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatComparisonWithHighPrecision(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForHighNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("972783798187987123879878123.188781371");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{\r\n            \"exclusiveMaximum\": 972783798187987123879878123.18878137\r\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteMinimumIntegerComparison : IClassFixture<SuiteMinimumIntegerComparison.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumIntegerComparison(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-18446744073709551600");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{ \"minimum\": -18446744073709551615 }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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
public class SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers : IClassFixture<SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-972783798187987123879878123.188781371");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\bignum.json",
                "{\r\n            \"exclusiveMinimum\": -972783798187987123879878123.18878137\r\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.Bignum",
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

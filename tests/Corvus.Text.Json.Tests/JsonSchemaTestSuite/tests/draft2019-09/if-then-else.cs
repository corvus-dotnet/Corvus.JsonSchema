using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.IfThenElse;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteIgnoreIfWithoutThenOrElse : IClassFixture<SuiteIgnoreIfWithoutThenOrElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreIfWithoutThenOrElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneIf()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneIf()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hello\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIgnoreThenWithoutIf : IClassFixture<SuiteIgnoreThenWithoutIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreThenWithoutIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hello\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"then\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIgnoreElseWithoutIf : IClassFixture<SuiteIgnoreElseWithoutIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreElseWithoutIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenValidAgainstLoneElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidWhenInvalidAgainstLoneElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hello\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"else\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIfAndThenWithoutElse : IClassFixture<SuiteIfAndThenWithoutElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAndThenWithoutElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidThroughThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidThroughThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-100");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidWhenIfTestFails()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIfAndElseWithoutThen : IClassFixture<SuiteIfAndElseWithoutThen.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAndElseWithoutThen(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenIfTestPasses()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidThroughElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("4");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidThroughElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteValidateAgainstCorrectBranchThenVsElse : IClassFixture<SuiteValidateAgainstCorrectBranchThenVsElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidateAgainstCorrectBranchThenVsElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidThroughThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidThroughThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-100");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidThroughElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("4");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidThroughElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteNonInterferenceAcrossCombinedSchemas : IClassFixture<SuiteNonInterferenceAcrossCombinedSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonInterferenceAcrossCombinedSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidButWouldHaveBeenInvalidThroughThen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidButWouldHaveBeenInvalidThroughElse()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("3");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"if\": {\r\n                        \"exclusiveMaximum\": 0\r\n                    }\r\n                },\r\n                {\r\n                    \"then\": {\r\n                        \"minimum\": -10\r\n                    }\r\n                },\r\n                {\r\n                    \"else\": {\r\n                        \"multipleOf\": 2\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIfWithBooleanSchemaTrue : IClassFixture<SuiteIfWithBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfWithBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"then\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"else\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": true,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIfWithBooleanSchemaFalse : IClassFixture<SuiteIfWithBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfWithBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"then\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"else\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": false,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence : IClassFixture<SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestYesRedirectsToThenAndPasses()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"yes\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOtherRedirectsToElseAndPasses()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"other\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoRedirectsToThenAndFails()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"no\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidRedirectsToElseAndFails()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"invalid\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"then\": { \"const\": \"yes\" },\r\n            \"else\": { \"const\": \"other\" },\r\n            \"if\": { \"maxLength\": 4 }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteThenFalseFailsWhenConditionMatches : IClassFixture<SuiteThenFalseFailsWhenConditionMatches.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteThenFalseFailsWhenConditionMatches(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesIfThenFalseInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesNotMatchIfThenIgnoredValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("2");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"then\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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
public class SuiteElseFalseFailsWhenConditionDoesNotMatch : IClassFixture<SuiteElseFalseFailsWhenConditionDoesNotMatch.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteElseFalseFailsWhenConditionDoesNotMatch(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesIfElseIgnoredValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesNotMatchIfElseExecutesInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("2");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"else\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.IfThenElse",
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

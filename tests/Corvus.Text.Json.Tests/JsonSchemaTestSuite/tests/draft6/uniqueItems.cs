using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.UniqueItems;

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteUniqueItemsValidation : IClassFixture<SuiteUniqueItemsValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfIntegersIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 1]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfMoreThanTwoIntegersIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 1]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1.0, 1.00, 1]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseIsNotEqualToZero()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueIsNotEqualToOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfStringsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfStringsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfObjectsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPropertyOrderOfArrayOfObjectsIsIgnored()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\", \"bar\": \"foo\"}, {\"bar\": \"foo\", \"foo\": \"bar\"}]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfNestedObjectsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfArraysIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"foo\"], [\"foo\"]]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfMoreThanTwoArraysIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"], [\"foo\"]]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test1AndTrueAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test0AndFalseAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test1AndTrueAreUnique1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[1], [true]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test0AndFalseAreUnique1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[0], [false]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNested1AndTrueAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[[1], \"foo\"], [[true], \"foo\"]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNested0AndFalseAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[[0], \"foo\"], [[false], \"foo\"]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{}, [1], true, null, 1, \"{}\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueHeterogeneousTypesAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{}, [1], true, null, {}, 1]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDifferentObjectsAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"a\": 1, \"b\": 2}, {\"a\": 2, \"b\": 1}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectsAreNonUniqueDespiteKeyOrder()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"a\": 1, \"b\": 2}, {\"b\": 2, \"a\": 1}]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAFalseAndA0AreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"a\": false}, {\"a\": 0}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestATrueAndA1AreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"a\": true}, {\"a\": 1}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{\"uniqueItems\": true}",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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
public class SuiteUniqueItemsWithAnArrayOfItems : IClassFixture<SuiteUniqueItemsWithAnArrayOfItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsWithAnArrayOfItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, false]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, true]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true\r\n        }",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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
public class SuiteUniqueItemsWithAnArrayOfItemsAndAdditionalItemsFalse : IClassFixture<SuiteUniqueItemsWithAnArrayOfItemsAndAdditionalItemsFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsWithAnArrayOfItemsAndAdditionalItemsFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, false]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, true]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, null]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true,\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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
public class SuiteUniqueItemsFalseValidation : IClassFixture<SuiteUniqueItemsFalseValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsFalseValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1.0, 1.00, 1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseIsNotEqualToZero()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueIsNotEqualToOne()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"foo\"], [\"foo\"]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test1AndTrueAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test0AndFalseAreUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[0, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{}, [1], true, null, 1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[{}, [1], true, null, {}, 1]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{ \"uniqueItems\": false }",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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
public class SuiteUniqueItemsFalseWithAnArrayOfItems : IClassFixture<SuiteUniqueItemsFalseWithAnArrayOfItems.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsFalseWithAnArrayOfItems(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"foo\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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
public class SuiteUniqueItemsFalseWithAnArrayOfItemsAndAdditionalItemsFalse : IClassFixture<SuiteUniqueItemsFalseWithAnArrayOfItemsAndAdditionalItemsFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUniqueItemsFalseWithAnArrayOfItemsAndAdditionalItemsFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, false]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[true, true]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[false, true, null]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false,\r\n            \"additionalItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft6.UniqueItems",
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

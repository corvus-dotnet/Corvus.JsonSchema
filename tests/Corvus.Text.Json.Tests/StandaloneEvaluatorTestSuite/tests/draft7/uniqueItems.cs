using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.UniqueItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfIntegersIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfMoreThanTwoIntegersIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0, 1.00, 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseIsNotEqualToZero()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueIsNotEqualToOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfObjectsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPropertyOrderOfArrayOfObjectsIsIgnored()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\", \"bar\": \"foo\"}, {\"bar\": \"foo\", \"foo\": \"bar\"}]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfNestedObjectsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfArraysIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"foo\"]]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfMoreThanTwoArraysIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"], [\"foo\"]]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test1AndTrueAreUnique1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1], [true]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test0AndFalseAreUnique1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[0], [false]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNested1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[1], \"foo\"], [[true], \"foo\"]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNested0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[0], \"foo\"], [[false], \"foo\"]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, 1, \"{}\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueHeterogeneousTypesAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, {}, 1]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDifferentObjectsAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": 1, \"b\": 2}, {\"a\": 2, \"b\": 1}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectsAreNonUniqueDespiteKeyOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": 1, \"b\": 2}, {\"b\": 2, \"a\": 1}]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAFalseAndA0AreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": false}, {\"a\": 0}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestATrueAndA1AreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": true}, {\"a\": 1}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{\"uniqueItems\": true}",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, null]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true,\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfIntegersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0, 1.00, 1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseIsNotEqualToZero()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueIsNotEqualToOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"foo\"]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, 1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, {}, 1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{ \"uniqueItems\": false }",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"bar\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"foo\"]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, null]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\uniqueItems.json",
                "{\r\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false,\r\n            \"additionalItems\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

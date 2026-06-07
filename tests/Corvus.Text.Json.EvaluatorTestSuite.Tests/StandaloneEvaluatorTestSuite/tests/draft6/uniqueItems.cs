using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.UniqueItems;

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsValidation
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfIntegersIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfMoreThanTwoIntegersIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 1]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0, 1.00, 1]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseIsNotEqualToZero()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueIsNotEqualToOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\", \"foo\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfObjectsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPropertyOrderOfArrayOfObjectsIsIgnored()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\", \"bar\": \"foo\"}, {\"bar\": \"foo\", \"foo\": \"bar\"}]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\n                ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfNestedObjectsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\n                ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfArraysIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"foo\"]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfMoreThanTwoArraysIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"], [\"foo\"]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test1AndTrueAreUnique1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1], [true]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test0AndFalseAreUnique1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[0], [false]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNested1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[1], \"foo\"], [[true], \"foo\"]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNested0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[0], \"foo\"], [[false], \"foo\"]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, 1, \"{}\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueHeterogeneousTypesAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, {}, 1]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDifferentObjectsAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": 1, \"b\": 2}, {\"a\": 2, \"b\": 1}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectsAreNonUniqueDespiteKeyOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": 1, \"b\": 2}, {\"b\": 2, \"a\": 1}]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAFalseAndA0AreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": false}, {\"a\": 0}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrueAndA1AreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": true}, {\"a\": 1}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{\"uniqueItems\": true}",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsWithAnArrayOfItems
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"foo\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"foo\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\n            \"uniqueItems\": true\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsWithAnArrayOfItemsAndAdditionalItemsFalse
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, null]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\n            \"uniqueItems\": true,\n            \"additionalItems\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsFalseValidation
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfIntegersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0, 1.00, 1]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseIsNotEqualToZero()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueIsNotEqualToOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\n                ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfNestedObjectsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\n                ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"bar\"]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayOfArraysIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"foo\"], [\"foo\"]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test1AndTrueAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test0AndFalseAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, 1]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueHeterogeneousTypesAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{}, [1], true, null, {}, 1]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{ \"uniqueItems\": false }",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsFalseWithAnArrayOfItems
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, \"foo\", \"foo\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, \"foo\", \"foo\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\n            \"uniqueItems\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsFalseWithAnArrayOfItemsAndAdditionalItemsFalse
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, false]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, true]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false, true, null]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/uniqueItems.json",
                "{\n            \"items\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\n            \"uniqueItems\": false,\n            \"additionalItems\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

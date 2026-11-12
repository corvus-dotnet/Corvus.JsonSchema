using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum;

[TestCategory("Draft6")]
[TestClass]
public class SuiteInteger
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
    public void TestABignumIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12345678910111213141516171819202122232425262728293031");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANegativeBignumIsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-12345678910111213141516171819202122232425262728293031");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"type\": \"integer\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteNumber
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
    public void TestABignumIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("98249283749234923498293171823948729348710298301928331");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANegativeBignumIsANumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-98249283749234923498293171823948729348710298301928331");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"type\": \"number\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteString
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
    public void TestABignumIsNotAString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("98249283749234923498293171823948729348710298301928331");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"type\": \"string\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteMaximumIntegerComparison
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
    public void TestComparisonWorksForHighNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("18446744073709551600");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestJustAboveTheMaximumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("18446744073709551616");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"maximum\": 18446744073709551615\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteFloatComparisonWithHighPrecision
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
    public void TestBelowTheExclusiveMaximumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("972783798187987123879878123.188781369");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestComparisonWorksForHighNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("972783798187987123879878123.188781371");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"exclusiveMaximum\": 972783798187987123879878123.18878137\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteMinimumIntegerComparison
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
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-18446744073709551600");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestJustBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-18446744073709551616");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"minimum\": -18446744073709551615\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteFloatComparisonWithHighPrecisionOnNegativeNumbers
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
    public void TestAboveTheExclusiveMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-972783798187987123879878123.188781369");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestComparisonWorksForVeryNegativeNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-972783798187987123879878123.188781371");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"exclusiveMinimum\": -972783798187987123879878123.18878137\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEnumComparisonWithHighPrecision
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
    public void TestTheExactDecimalIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456789");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASlightlySmallerDecimalIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456788");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASlightlyLargerDecimalIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456790");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"enum\": [0.123456789123456789]\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteConstComparisonWithHighPrecision
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
    public void TestTheExactDecimalIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456789");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASlightlySmallerDecimalIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456788");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASlightlyLargerDecimalIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.123456789123456790");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"const\": 0.123456789123456789\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled
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
    public void TestValidIfOptionalOverflowHandlingIsImplemented()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1e308");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"type\": \"number\",\n                \"multipleOf\": 0.5\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteUniqueItemsComparisonWithHighPrecision
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
    public void TestDistinctHighPrecisionDecimalsAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0.123456789123456789, 0.123456789123456790]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTheSameHighPrecisionDecimalIsNotUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0.123456789123456789, 0.123456789123456789]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingZeroesDoNotMakeDecimalsUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[0.123456789123456789, 0.1234567891234567890]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArraysContainingDistinctDecimalsAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[0.123456789123456789], [0.123456789123456790]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArraysContainingTheSameDecimalAreNotUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[0.123456789123456789], [0.123456789123456789]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectsContainingDistinctDecimalsAreUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"value\": 0.123456789123456789}, {\"value\": 0.123456789123456790}]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectsContainingTheSameDecimalAreNotUnique()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"value\": 0.123456789123456789}, {\"value\": 0.123456789123456789}]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"uniqueItems\": true\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteMultipleOfWithIntegersAbove253
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
    public void Test3e20IsMultipleOf3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("300000000000000000000");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test1e20IsNotMultipleOf3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100000000000000000000");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/bignum.json",
                "{\n                \"multipleOf\": 3\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Bignum",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

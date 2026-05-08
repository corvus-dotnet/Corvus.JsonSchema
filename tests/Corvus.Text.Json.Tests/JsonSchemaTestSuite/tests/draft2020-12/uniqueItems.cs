using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.UniqueItems;

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfIntegersIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 1]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfMoreThanTwoIntegersIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2, 1]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1.0, 1.00, 1]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseIsNotEqualToZero()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[0, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueIsNotEqualToOne()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfStringsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfStringsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfObjectsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPropertyOrderOfArrayOfObjectsIsIgnored()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\", \"bar\": \"foo\"}, {\"bar\": \"foo\", \"foo\": \"bar\"}]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfNestedObjectsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfArraysIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[\"foo\"], [\"foo\"]]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfMoreThanTwoArraysIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"], [\"foo\"]]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test1AndTrueAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test0AndFalseAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[0, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test1AndTrueAreUnique1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[1], [true]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test0AndFalseAreUnique1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[0], [false]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNested1AndTrueAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[[1], \"foo\"], [[true], \"foo\"]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNested0AndFalseAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[[0], \"foo\"], [[false], \"foo\"]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{}, [1], true, null, 1, \"{}\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueHeterogeneousTypesAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{}, [1], true, null, {}, 1]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDifferentObjectsAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"a\": 1, \"b\": 2}, {\"a\": 2, \"b\": 1}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectsAreNonUniqueDespiteKeyOrder()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"a\": 1, \"b\": 2}, {\"b\": 2, \"a\": 1}]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFalseAndA0AreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"a\": false}, {\"a\": 0}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrueAndA1AreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"a\": true}, {\"a\": 1}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"uniqueItems\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, false]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, true]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, false]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, true]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, null]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": true,\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfIntegersIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 1]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNumbersAreUniqueIfMathematicallyUnequal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1.0, 1.00, 1]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseIsNotEqualToZero()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[0, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueIsNotEqualToOne()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"baz\"}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{\"foo\": \"bar\"}, {\"foo\": \"bar\"}]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : false}}}\r\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfNestedObjectsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}},\r\n                    {\"foo\": {\"bar\" : {\"baz\" : true}}}\r\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[\"foo\"], [\"bar\"]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayOfArraysIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[\"foo\"], [\"foo\"]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test1AndTrueAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test0AndFalseAreUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[0, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{}, [1], true, null, 1]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueHeterogeneousTypesAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[{}, [1], true, null, {}, 1]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"uniqueItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromFalseTrueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, \"foo\", \"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonUniqueArrayExtendedFromTrueFalseIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false, \"foo\", \"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestFalseTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFalseFalseFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrueTrueFromItemsArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExtraItemsAreInvalidEvenIfUnique()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[false, true, null]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\uniqueItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"boolean\"}, {\"type\": \"boolean\"}],\r\n            \"uniqueItems\": false,\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UniqueItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Ref;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRootPointerRef
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
    public void TestMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": false}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": {\"foo\": false}}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": false}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": {\"bar\": false}}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#\"}\n            },\n            \"additionalProperties\": false\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRelativePointerRefToObject
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
    public void TestMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 3}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": true}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": {\"type\": \"integer\"},\n                \"bar\": {\"$ref\": \"#/properties/foo\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRelativePointerRefToArray
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
    public void TestMatchArray()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatchArray()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, \"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"items\": [\n                {\"type\": \"integer\"},\n                {\"$ref\": \"#/items/0\"}\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEscapedPointerRef
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
    public void TestSlashInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"slash\": \"aoeu\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"tilde\": \"aoeu\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPercentInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"percent\": \"aoeu\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSlashValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"slash\": 123}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"tilde\": 123}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPercentValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"percent\": 123}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"tilde~field\": {\"type\": \"integer\"},\n                \"slash/field\": {\"type\": \"integer\"},\n                \"percent%field\": {\"type\": \"integer\"}\n            },\n            \"properties\": {\n                \"tilde\": {\"$ref\": \"#/$defs/tilde~0field\"},\n                \"slash\": {\"$ref\": \"#/$defs/slash~1field\"},\n                \"percent\": {\"$ref\": \"#/$defs/percent%25field\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNestedRefs
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
    public void TestNestedRefValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("5");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNestedRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"a\": {\"type\": \"integer\"},\n                \"b\": {\"$ref\": \"#/$defs/a\"},\n                \"c\": {\"$ref\": \"#/$defs/b\"}\n            },\n            \"$ref\": \"#/$defs/c\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefAppliesAlongsideSiblingKeywords
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
    public void TestRefValidMaxItemsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": [] }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRefValidMaxItemsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": [1, 2, 3] }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"string\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"reffed\": {\n                    \"type\": \"array\"\n                }\n            },\n            \"properties\": {\n                \"foo\": {\n                    \"$ref\": \"#/$defs/reffed\",\n                    \"maxItems\": 2\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRemoteRefContainingRefsItself
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
    public void TestRemoteRefValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"minLength\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRemoteRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"minLength\": -1}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"https://json-schema.org/draft/2019-09/schema\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuitePropertyNamedRefThatIsNotAReference
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
    public void TestPropertyNamedRefValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"$ref\": \"a\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"$ref\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"$ref\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuitePropertyNamedRefContainingAnActualRef
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
    public void TestPropertyNamedRefValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"$ref\": \"a\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"$ref\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"$ref\": {\"$ref\": \"#/$defs/is-string\"}\n            },\n            \"$defs\": {\n                \"is-string\": {\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToBooleanSchemaTrue
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
    public void TestAnyValueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"#/$defs/bool\",\n            \"$defs\": {\n                \"bool\": true\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToBooleanSchemaFalse
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
    public void TestAnyValueIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"#/$defs/bool\",\n            \"$defs\": {\n                \"bool\": false\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveReferencesBetweenSchemas
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
    public void TestValidTree()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"meta\": \"root\",\n                    \"nodes\": [\n                        {\n                            \"value\": 1,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 1.1},\n                                    {\"value\": 1.2}\n                                ]\n                            }\n                        },\n                        {\n                            \"value\": 2,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 2.1},\n                                    {\"value\": 2.2}\n                                ]\n                            }\n                        }\n                    ]\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTree()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"meta\": \"root\",\n                    \"nodes\": [\n                        {\n                            \"value\": 1,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": \"string is invalid\"},\n                                    {\"value\": 1.2}\n                                ]\n                            }\n                        },\n                        {\n                            \"value\": 2,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 2.1},\n                                    {\"value\": 2.2}\n                                ]\n                            }\n                        }\n                    ]\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:1234/draft2019-09/tree\",\n            \"description\": \"tree of nodes\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"meta\": {\"type\": \"string\"},\n                \"nodes\": {\n                    \"type\": \"array\",\n                    \"items\": {\"$ref\": \"node\"}\n                }\n            },\n            \"required\": [\"meta\", \"nodes\"],\n            \"$defs\": {\n                \"node\": {\n                    \"$id\": \"http://localhost:1234/draft2019-09/node\",\n                    \"description\": \"node\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"value\": {\"type\": \"number\"},\n                        \"subtree\": {\"$ref\": \"tree\"}\n                    },\n                    \"required\": [\"value\"]\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsWithQuote
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
    public void TestObjectWithNumbersIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\\\"bar\": 1\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithStringsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\\\"bar\": \"1\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\\\"bar\": {\"$ref\": \"#/$defs/foo%22bar\"}\n            },\n            \"$defs\": {\n                \"foo\\\"bar\": {\"type\": \"number\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefCreatesNewScopeWhenAdjacentToKeywords
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
    public void TestReferencedSubschemaDoesnTSeeAnnotationsFromProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"prop1\": \"match\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"A\": {\n                    \"unevaluatedProperties\": false\n                }\n            },\n            \"properties\": {\n                \"prop1\": {\n                    \"type\": \"string\"\n                }\n            },\n            \"$ref\": \"#/$defs/A\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect
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
    public void TestDoNotEvaluateTheRefInsideTheEnumMatchingAnyString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"this is a string\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoNotEvaluateTheRefInsideTheEnumDefinitionExactMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"type\": \"string\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchTheEnumExactly()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"$ref\": \"#/$defs/a_string\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"a_string\": { \"type\": \"string\" }\n            },\n            \"enum\": [\n                { \"$ref\": \"#/$defs/a_string\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsWithRelativeUrisAndDefs
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
    public void TestInvalidOnInnerField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": 1\n                    },\n                    \"bar\": \"a\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidOnOuterField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": \"a\"\n                    },\n                    \"bar\": 1\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidOnBothFields()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": \"a\"\n                    },\n                    \"bar\": \"a\"\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://example.com/schema-relative-uri-defs1.json\",\n            \"properties\": {\n                \"foo\": {\n                    \"$id\": \"schema-relative-uri-defs2.json\",\n                    \"$defs\": {\n                        \"inner\": {\n                            \"properties\": {\n                                \"bar\": { \"type\": \"string\" }\n                            }\n                        }\n                    },\n                    \"$ref\": \"#/$defs/inner\"\n                }\n            },\n            \"$ref\": \"schema-relative-uri-defs2.json\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRelativeRefsWithAbsoluteUrisAndDefs
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
    public void TestInvalidOnInnerField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": 1\n                    },\n                    \"bar\": \"a\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidOnOuterField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": \"a\"\n                    },\n                    \"bar\": 1\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidOnBothFields()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": {\n                        \"bar\": \"a\"\n                    },\n                    \"bar\": \"a\"\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\n            \"properties\": {\n                \"foo\": {\n                    \"$id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\n                    \"$defs\": {\n                        \"inner\": {\n                            \"properties\": {\n                                \"bar\": { \"type\": \"string\" }\n                            }\n                        }\n                    },\n                    \"$ref\": \"#/$defs/inner\"\n                }\n            },\n            \"$ref\": \"schema-refs-absolute-uris-defs2.json\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent
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
    public void TestNumberIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://example.com/a.json\",\n            \"$defs\": {\n                \"x\": {\n                    \"$id\": \"http://example.com/b/c.json\",\n                    \"not\": {\n                        \"$defs\": {\n                            \"y\": {\n                                \"$id\": \"d.json\",\n                                \"type\": \"number\"\n                            }\n                        }\n                    }\n                }\n            },\n            \"allOf\": [\n                {\n                    \"$ref\": \"http://example.com/b/d.json\"\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndRef
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
    public void TestDataIsValidAgainstFirstDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("5");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("50");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id1/base.json\",\n            \"$ref\": \"int.json\",\n            \"$defs\": {\n                \"bigint\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id1/int.json\",\n                    \"$id\": \"int.json\",\n                    \"maximum\": 10\n                },\n                \"smallint\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id1-int.json\",\n                    \"$id\": \"/draft2019-09/ref-and-id1-int.json\",\n                    \"maximum\": 2\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndAnchorAndRef
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
    public void TestDataIsValidAgainstFirstDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("5");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("50");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id2/base.json\",\n            \"$ref\": \"#bigint\",\n            \"$defs\": {\n                \"bigint\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id2/base.json#/$defs/bigint; another valid uri for this location: https://example.com/ref-and-id2/base.json#bigint\",\n                    \"$anchor\": \"bigint\",\n                    \"maximum\": 10\n                },\n                \"smallint\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id2#/$defs/smallint; another valid uri for this location: https://example.com/ref-and-id2/#bigint\",\n                    \"$id\": \"/draft2019-09/ref-and-id2/\",\n                    \"$anchor\": \"bigint\",\n                    \"maximum\": 2\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndRefOnNestedSchema
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
    public void TestDataIsValidAgainstNestedSibling()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("5");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstNestedSibling()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id3/base.json\",\n            \"$ref\": \"nested/foo.json\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id3/nested/foo.json\",\n                    \"$id\": \"nested/foo.json\",\n                    \"$ref\": \"./bar.json\"\n                },\n                \"bar\": {\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id3/nested/bar.json\",\n                    \"$id\": \"nested/bar.json\",\n                    \"type\": \"number\"\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithRefViaTheUrn
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
    public void TestValidUnderTheUrnIDedSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 37}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidUnderTheUrnIDedSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n            \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\n            \"minimum\": 30,\n            \"properties\": {\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithJsonPointer
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n            \"$id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\n            },\n            \"$defs\": {\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithNss
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"RFC 8141 §2.2\",\n            \"$id\": \"urn:example:1/406/47452/2\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\n            },\n            \"$defs\": {\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithRComponent
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"RFC 8141 §2.3.1\",\n            \"$id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\n            },\n            \"$defs\": {\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithQComponent
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$comment\": \"RFC 8141 §2.3.2\",\n            \"$id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\n            },\n            \"$defs\": {\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndJsonPointerRef
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/$defs/bar\"}\n            },\n            \"$defs\": {\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndAnchorRef
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\n            \"properties\": {\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\"}\n            },\n            \"$defs\": {\n                \"bar\": {\n                    \"$anchor\": \"something\",\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnRefWithNestedPointerRef
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$id\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\n                    \"$defs\": {\"bar\": {\"type\": \"string\"}},\n                    \"$ref\": \"#/$defs/bar\"\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToIf
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"http://example.com/ref/if\",\n            \"if\": {\n                \"$id\": \"http://example.com/ref/if\",\n                \"type\": \"integer\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToThen
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"http://example.com/ref/then\",\n            \"then\": {\n                \"$id\": \"http://example.com/ref/then\",\n                \"type\": \"integer\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToElse
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"http://example.com/ref/else\",\n            \"else\": {\n                \"$id\": \"http://example.com/ref/else\",\n                \"type\": \"integer\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefWithAbsolutePathReference
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
    public void TestAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIntegerIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n             \"$id\": \"http://example.com/ref/absref.json\",\n             \"$defs\": {\n                 \"a\": {\n                     \"$id\": \"http://example.com/ref/absref/foobar.json\",\n                     \"type\": \"number\"\n                 },\n                 \"b\": {\n                     \"$id\": \"http://example.com/absref/foobar.json\",\n                     \"type\": \"string\"\n                 }\n             },\n             \"$ref\": \"/absref/foobar.json\"\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIdWithFileUriStillResolvesPointersNix
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
    public void TestNumberIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n             \"$id\": \"file:///folder/file.json\",\n             \"$defs\": {\n                 \"foo\": {\n                     \"type\": \"number\"\n                 }\n             },\n             \"$ref\": \"#/$defs/foo\"\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIdWithFileUriStillResolvesPointersWindows
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
    public void TestNumberIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n             \"$id\": \"file:///c:/folder/file.json\",\n             \"$defs\": {\n                 \"foo\": {\n                     \"type\": \"number\"\n                 }\n             },\n             \"$ref\": \"#/$defs/foo\"\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEmptyTokensInRefJsonPointer
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
    public void TestNumberIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n             \"$defs\": {\n                 \"\": {\n                     \"$defs\": {\n                         \"\": { \"type\": \"number\" }\n                     }\n                 } \n             },\n             \"allOf\": [\n                 {\n                     \"$ref\": \"#/$defs//$defs/\"\n                 }\n             ]\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefWithRecursiveAnchor
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
    public void TestExtraItemsAllowedForInnerArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\",[\"bar\" , [] , 8]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExtraItemsDisallowedForRoot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\",[\"bar\" , [] , 8], 8]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/ref.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/schemas/unevaluated-items-are-disallowed\",\n            \"$ref\": \"/schemas/unevaluated-items-are-allowed\",\n            \"$recursiveAnchor\": true,\n            \"unevaluatedItems\": false,\n            \"$defs\": {\n                \"/schemas/unevaluated-items-are-allowed\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"$id\": \"/schemas/unevaluated-items-are-allowed\",\n                    \"$recursiveAnchor\": true,\n                    \"type\": \"array\",\n                    \"items\": [\n                        {\n                            \"type\": \"string\"\n                        },\n                        {\n                            \"$ref\": \"#\"\n                        }\n                    ]\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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

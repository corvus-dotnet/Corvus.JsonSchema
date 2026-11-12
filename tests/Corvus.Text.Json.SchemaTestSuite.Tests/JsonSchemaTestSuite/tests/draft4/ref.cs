using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Ref;

[TestCategory("Draft4")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": false }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"foo\": false } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": false }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": false } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/ref.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#\" }\n                },\n                \"additionalProperties\": false\n            }",
                "JsonSchemaTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": 3 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": true }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/ref.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"type\": \"integer\" },\n                    \"bar\": { \"$ref\": \"#/properties/foo\" }\n                }\n            }",
                "JsonSchemaTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"$ref\": \"a\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"$ref\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/ref.json",
                "{\n                \"properties\": {\n                    \"$ref\": { \"type\": \"string\" }\n                }\n            }",
                "JsonSchemaTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                        \"meta\": \"root\",\n                        \"nodes\": [\n                            {\n                                \"value\": 1,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 1.1 },\n                                        { \"value\": 1.2 }\n                                    ]\n                                }\n                            },\n                            {\n                                \"value\": 2,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 2.1 },\n                                        { \"value\": 2.2 }\n                                    ]\n                                }\n                            }\n                        ]\n                    }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTree()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                        \"meta\": \"root\",\n                        \"nodes\": [\n                            {\n                                \"value\": 1,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": \"string is invalid\" },\n                                        { \"value\": 1.2 }\n                                    ]\n                                }\n                            },\n                            {\n                                \"value\": 2,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 2.1 },\n                                        { \"value\": 2.2 }\n                                    ]\n                                }\n                            }\n                        ]\n                    }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/ref.json",
                "{ \"$ref\": \"http://localhost:1234/tree\" }",
                "JsonSchemaTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/tree\": {\n                    \"description\": \"tree of nodes\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"meta\": { \"type\": \"string\" },\n                        \"nodes\": {\n                            \"type\": \"array\",\n                            \"items\": { \"$ref\": \"node\" }\n                        }\n                    }\n                },\n                \"http://localhost:1234/node\": {\n                    \"description\": \"node\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"value\": { \"type\": \"number\" },\n                        \"subtree\": { \"$ref\": \"tree\" }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRemoteRef
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRemoteRefInvalid()
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
                "tests/draft4/ref.json",
                "{ \"$ref\": \"http://localhost:1234/integer.json\" }",
                "JsonSchemaTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/integer.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

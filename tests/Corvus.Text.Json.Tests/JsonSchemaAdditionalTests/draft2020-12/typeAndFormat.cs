using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaAdditionalTests.Draft202012;

[TestCategory("Draft202012")]
[TestClass]
public class TypeAndFormat
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
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestA64BitIntegerIsNotAnInteger32()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("3000000000");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.1");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAStringIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAStringIsStillNotAnIntegerEvenIfItLooksLikeOne()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnObjectIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnArrayIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestABooleanIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNullIsNotAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2020-12\\typeAndFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "type": "integer",
                    "format": "int32"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.TypeAndFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class MultiTypeAndFormat
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
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestA64BitIntegerIsNotAnInteger32()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("3000000000");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.1");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAStringIsAllowed()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnObjectIsAllowed()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnArrayIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestABooleanIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNullIsNotAnIntegerOrAStringOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2019-09\\multiTypeAndFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2019-09/schema",
                    "type": ["integer", "string", "object"],
                    "format": "int32"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.MultiTypeAndFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class MultiTypeDifferentFormat
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
    public void TestAnIntegerIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatWithZeroFractionalPartIsAnInteger()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFloatIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.1");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAStringIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEmailStringIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnObjectIsAllowed()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnArrayIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestABooleanIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNullIsNotAnIntegerOrAnObject()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2019-09\\multiTypeDifferentFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2019-09/schema",
                    "type": ["integer", "object"],
                    "format": "email"
                }
                """,
                "JsonSchemaTestSuite.Draft202012.MultiTypeDifferentFormat",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
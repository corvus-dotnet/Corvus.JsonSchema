using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.MinContains;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteMinContainsWithoutContainsIsIgnored
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
    public void TestOneItemValidAgainstLoneMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroItemsStillValidAgainstLoneMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"minContains\": 1\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains1WithContains
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoElementsMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 2 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleElementMatchesValidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSomeElementsMatchValidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchValidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"minContains\": 1\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains2WithContains
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchInvalidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSomeElementsMatchInvalidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchValidMinContainsExactlyAsNeeded()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchValidMinContainsMoreThanNeeded()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSomeElementsMatchValidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"minContains\": 2\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains2WithContainsWithADecimalValue
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
    public void TestOneElementMatchesInvalidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBothElementsMatchValidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"minContains\": 2.0\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMaxContainsMinContains
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchInvalidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchInvalidMaxContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllElementsMatchValidMaxContainsAndMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"maxContains\": 2,\n            \"minContains\": 2\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMaxContainsMinContains1
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidMaxContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1, 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidMaxContainsAndMinContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"maxContains\": 1,\n            \"minContains\": 3\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains0
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMinContains0MakesContainsAlwaysPass()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 2 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"minContains\": 0\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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
public class SuiteMinContains0WithMaxContains
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
    public void TestEmptyData()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotMoreThanMaxContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooMany()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/minContains.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"const\": 1},\n            \"minContains\": 0,\n            \"maxContains\": 1\n        }",
                "JsonSchemaTestSuite.Draft202012.MinContains",
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

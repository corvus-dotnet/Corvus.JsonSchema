using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.DependentSchemas;

[TestCategory("Draft6")]
[TestClass]
public class SuiteSingleDependencyDependencies
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
    public void TestValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"quux\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongType()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"quux\", \"bar\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeOther()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 2, \"bar\": \"quux\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeBoth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"quux\", \"bar\": \"quux\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"bar\": {\n                        \"properties\": {\n                            \"foo\": { \"type\": \"integer\" },\n                            \"bar\": { \"type\": \"integer\" }\n                        }\n                    }\n                }\n            }",
                "JsonSchemaTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteBooleanSubschemasDependencies
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
    public void TestObjectWithPropertyHavingSchemaTrueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithPropertyHavingSchemaFalseIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithBothPropertiesIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyObjectIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"foo\": true,\n                    \"bar\": false\n                }\n            }",
                "JsonSchemaTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependentsWithEscapedCharactersDependencies
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
    public void TestValidObject1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\\nbar\": 1, \"foo\\rbar\": 2 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidObject2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\\tbar\": 1, \"a\": 2, \"b\": 3, \"c\": 4 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidObject3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo'bar\": 1, \"foo\\\"bar\": 2 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\\nbar\": 1, \"foo\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\\tbar\": 1, \"a\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo'bar\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\\\"bar\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"foo\\nbar\": [\"foo\\rbar\"],\n                    \"foo\\tbar\": { \"minProperties\": 4 },\n                    \"foo'bar\": { \"required\": [\"foo\\\"bar\"] },\n                    \"foo\\\"bar\": [\"foo'bar\"]\n                }\n            }",
                "JsonSchemaTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependentSubschemaIncompatibleWithRootDependencies
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
    public void TestMatchesRoot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesBoth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"baz\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"properties\": { \"foo\": {} },\n                \"dependencies\": {\n                    \"foo\": {\n                        \"properties\": { \"bar\": {} },\n                        \"additionalProperties\": false\n                    }\n                }\n            }",
                "JsonSchemaTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.Dependencies;

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependencies
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
    public void TestNeither()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNondependant()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foobar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
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
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\"bar\": [\"foo\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependenciesWithEmptyArray
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
    public void TestEmptyObject()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithOneProperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonObjectIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\"bar\": []}\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteMultipleDependencies
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
    public void TestNeither()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNondependants()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithDependencies()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2, \"quux\": 3}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"quux\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingOtherDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 1, \"quux\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingBothDependencies()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"quux\": 1}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\"quux\": [\"foo\", \"bar\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteMultipleDependenciesSubschema
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
    public void TestValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"quux\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongType()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"quux\", \"bar\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeOther()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 2, \"bar\": \"quux\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeBoth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"quux\", \"bar\": \"quux\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"integer\"},\r\n                        \"bar\": {\"type\": \"integer\"}\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependenciesWithBooleanSubschemas
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
    public void TestObjectWithPropertyHavingSchemaTrueIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithPropertyHavingSchemaFalseIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithBothPropertiesIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
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
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\r\n                \"foo\": true,\r\n                \"bar\": false\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependenciesWithEscapedCharacters
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
    public void TestValidObject1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\rbar\": 2\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidObject2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2,\r\n                    \"b\": 3,\r\n                    \"c\": 4\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidObject3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": 1,\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\": 2\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidObject4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"dependencies\": {\r\n                \"foo\\nbar\": [\"foo\\rbar\"],\r\n                \"foo\\tbar\": {\r\n                    \"minProperties\": 4\r\n                },\r\n                \"foo'bar\": {\"required\": [\"foo\\\"bar\"]},\r\n                \"foo\\\"bar\": [\"foo'bar\"]\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependentSubschemaIncompatibleWithRoot
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
    public void TestMatchesRoot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesBoth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDependency()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"baz\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\dependencies.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {}\r\n            },\r\n            \"dependencies\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": {}\r\n                    },\r\n                    \"additionalProperties\": false\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft6.Dependencies",
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

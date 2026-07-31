using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft6.Applicators;

[TestCategory("Draft6")]
[TestClass]
public class SuitePropertiesPatternPropertiesAndAdditionalProperties
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
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{}",
            "/foo",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0TitleAppleAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{}",
            "/apple",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0TitleBarAssertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{}",
            "/bar",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\n            \"foo\": {},\n            \"apple\": {},\n            \"baz\": {}\n          }",
            "/foo",
            "title",
            "{\n                \"#/properties/foo\": \"Foo\"\n              }");
    }

    [TestMethod]
    public void Test1TitleAppleAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\n            \"foo\": {},\n            \"apple\": {},\n            \"baz\": {}\n          }",
            "/apple",
            "title",
            "{\n                \"#/patternProperties/%5Ea\": \"Bar\"\n              }");
    }

    [TestMethod]
    public void Test1TitleBazAssertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\n            \"foo\": {},\n            \"apple\": {},\n            \"baz\": {}\n          }",
            "/baz",
            "title",
            "{\n                \"#/additionalProperties\": \"Baz\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"properties\": {\n          \"foo\": {\n            \"title\": \"Foo\"\n          }\n        },\n        \"patternProperties\": {\n          \"^a\": {\n            \"title\": \"Bar\"\n          }\n        },\n        \"additionalProperties\": {\n          \"title\": \"Baz\"\n        }\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePropertyNamesDoesnTAnnotatePropertyValues
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
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\n            \"foo\": 42\n          }",
            "/foo",
            "title",
            "{}");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"propertyNames\": {\n          \"const\": \"foo\",\n          \"title\": \"Foo\"\n        }\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteContains
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
    public void Test0Title0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\n            \"foo\",\n            42,\n            true\n          ]",
            "/0",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\n            \"foo\",\n            42,\n            true\n          ]",
            "/1",
            "title",
            "{\n                \"#/contains\": \"Foo\"\n              }");
    }

    [TestMethod]
    public void Test0Title2Assertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\n            \"foo\",\n            42,\n            true\n          ]",
            "/2",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0Title3Assertion3()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\n            \"foo\",\n            42,\n            true\n          ]",
            "/3",
            "title",
            "{}");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"contains\": {\n          \"type\": \"number\",\n          \"title\": \"Foo\"\n        }\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteAllOf
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\n                \"#/allOf/1\": \"Bar\",\n                \"#/allOf/0\": \"Foo\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"allOf\": [\n          {\n            \"title\": \"Foo\"\n          },\n          {\n            \"title\": \"Bar\"\n          }\n        ]\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteAnyOf
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "title",
            "{\n                \"#/anyOf/1\": \"Bar\",\n                \"#/anyOf/0\": \"Foo\"\n              }");
    }

    [TestMethod]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "4.2",
            "",
            "title",
            "{\n                \"#/anyOf/1\": \"Bar\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"anyOf\": [\n          {\n            \"type\": \"integer\",\n            \"title\": \"Foo\"\n          },\n          {\n            \"type\": \"number\",\n            \"title\": \"Bar\"\n          }\n        ]\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteOneOf
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\n                \"#/oneOf/0\": \"Foo\"\n              }");
    }

    [TestMethod]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "title",
            "{\n                \"#/oneOf/1\": \"Bar\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"oneOf\": [\n          {\n            \"type\": \"string\",\n            \"title\": \"Foo\"\n          },\n          {\n            \"type\": \"number\",\n            \"title\": \"Bar\"\n          }\n        ]\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteNot
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{}",
            "",
            "title",
            "{\n                \"#\": \"Foo\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\n        \"title\": \"Foo\",\n        \"not\": {\n          \"not\": {\n            \"title\": \"Bar\"\n          }\n        }\n      }",
                "AnnotationTestSuite.Draft6.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

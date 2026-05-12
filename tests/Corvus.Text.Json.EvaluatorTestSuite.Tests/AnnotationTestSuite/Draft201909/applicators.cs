using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft201909.Applicators;

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/foo",
            "title",
            "{\r\n                \"#/properties/foo\": \"Foo\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleAppleAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/apple",
            "title",
            "{\r\n                \"#/patternProperties/%5Ea\": \"Bar\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleBazAssertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/baz",
            "title",
            "{\r\n                \"#/additionalProperties\": \"Baz\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"properties\": {\r\n          \"foo\": {\r\n            \"title\": \"Foo\"\r\n          }\r\n        },\r\n        \"patternProperties\": {\r\n          \"^a\": {\r\n            \"title\": \"Bar\"\r\n          }\r\n        },\r\n        \"additionalProperties\": {\r\n          \"title\": \"Baz\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
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
                "{\r\n        \"propertyNames\": {\r\n          \"const\": \"foo\",\r\n          \"title\": \"Foo\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void Test0Title0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/0",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/1",
            "title",
            "{\r\n                \"#/contains\": \"Foo\"\r\n              }");
    }

    [TestMethod]
    public void Test0Title2Assertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/2",
            "title",
            "{}");
    }

    [TestMethod]
    public void Test0Title3Assertion3()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
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
                "{\r\n        \"contains\": {\r\n          \"type\": \"number\",\r\n          \"title\": \"Foo\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/allOf/1\": \"Bar\",\r\n                \"#/allOf/0\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"allOf\": [\r\n          {\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/anyOf/1\": \"Bar\",\r\n                \"#/anyOf/0\": \"Foo\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "4.2",
            "",
            "title",
            "{\r\n                \"#/anyOf/1\": \"Bar\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"anyOf\": [\r\n          {\r\n            \"type\": \"integer\",\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"type\": \"number\",\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/oneOf/0\": \"Foo\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "title",
            "{\r\n                \"#/oneOf/1\": \"Bar\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"oneOf\": [\r\n          {\r\n            \"type\": \"string\",\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"type\": \"number\",\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"title\": \"Foo\",\r\n        \"not\": {\r\n          \"not\": {\r\n            \"title\": \"Bar\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteDependentSchemas
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
            "",
            "title",
            "{\r\n                \"#/dependentSchemas/foo\": \"Foo\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
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
                "{\r\n        \"dependentSchemas\": {\r\n          \"foo\": {\r\n            \"title\": \"Foo\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIfThenAndElse
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
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\r\n                \"#/then\": \"Then\",\r\n                \"#/if\": \"If\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "title",
            "{\r\n                \"#/else\": \"Else\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"if\": {\r\n          \"title\": \"If\",\r\n          \"type\": \"string\"\r\n        },\r\n        \"then\": {\r\n          \"title\": \"Then\"\r\n        },\r\n        \"else\": {\r\n          \"title\": \"Else\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

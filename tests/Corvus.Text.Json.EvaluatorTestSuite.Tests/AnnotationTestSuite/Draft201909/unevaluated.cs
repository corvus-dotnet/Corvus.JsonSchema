using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft201909.Unevaluated;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesAlone
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithProperties
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/properties/foo\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"properties\": {\n          \"foo\": { \"title\": \"Evaluated\" }\n        },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithPatternProperties
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
    public void Test0TitleAppleAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"apple\": 42, \"bar\": 24 }",
            "/apple",
            "title",
            "{\n                \"#/patternProperties/^a\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"apple\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"patternProperties\": {\n          \"^a\": { \"title\": \"Evaluated\" }\n        },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAdditionalProperties
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/additionalProperties\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/additionalProperties\": \"Evaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"additionalProperties\": { \"title\": \"Evaluated\" },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithDependentSchemas
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/dependentSchemas/foo/properties/bar\": \"Evaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"dependentSchemas\": {\n          \"foo\": {\n            \"properties\": {\n              \"bar\": { \"title\": \"Evaluated\" }\n            }\n          }\n        },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithIfThenAndElse
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
            "{ \"foo\": \"\", \"bar\": 42 }",
            "/foo",
            "title",
            "{\n                \"#/then/properties/foo\": \"Then\",\n                \"#/if/properties/foo\": \"If\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": \"\", \"bar\": 42 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    [TestMethod]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/foo",
            "title",
            "{\n                \"#/else/properties/foo\": \"Else\"\n              }");
    }

    [TestMethod]
    public void Test1TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"if\": {\n          \"properties\": {\n            \"foo\": {\n              \"type\": \"string\",\n              \"title\": \"If\"\n            }\n          }\n        },\n        \"then\": {\n          \"properties\": {\n            \"foo\": { \"title\": \"Then\" }\n          }\n        },\n        \"else\": {\n          \"properties\": {\n            \"foo\": { \"title\": \"Else\" }\n          }\n        },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAllOf
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/allOf/0/properties/foo\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"allOf\": [\n          {\n            \"properties\": {\n              \"foo\": { \"title\": \"Evaluated\" }\n            }\n          }\n        ],\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAnyOf
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/anyOf/0/properties/foo\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"anyOf\": [\n          {\n            \"properties\": {\n              \"foo\": { \"title\": \"Evaluated\" }\n            }\n          }\n        ],\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithOneOf
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/oneOf/0/properties/foo\": \"Evaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"oneOf\": [\n          {\n            \"properties\": {\n              \"foo\": { \"title\": \"Evaluated\" }\n            }\n          }\n        ],\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNot
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
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\n                \"#/unevaluatedProperties\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"not\": {\n          \"not\": {\n            \"properties\": {\n              \"foo\": { \"title\": \"Evaluated\" }\n            }\n          }\n        },\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedItemsAlone
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
            "[42, 24]",
            "/0",
            "title",
            "{\n                \"#/unevaluatedItems\": \"Unevaluated\"\n              }");
    }

    [TestMethod]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[42, 24]",
            "/1",
            "title",
            "{\n                \"#/unevaluatedItems\": \"Unevaluated\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\n        \"unevaluatedItems\": { \"title\": \"Unevaluated\" }\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

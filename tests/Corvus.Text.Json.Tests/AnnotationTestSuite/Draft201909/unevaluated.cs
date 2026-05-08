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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"properties\": {\r\n          \"foo\": { \"title\": \"Evaluated\" }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/patternProperties/%5Ea\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"apple\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"patternProperties\": {\r\n          \"^a\": { \"title\": \"Evaluated\" }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/additionalProperties\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/additionalProperties\": \"Evaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"additionalProperties\": { \"title\": \"Evaluated\" },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/dependentSchemas/foo/properties/bar\": \"Evaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"dependentSchemas\": {\r\n          \"foo\": {\r\n            \"properties\": {\r\n              \"bar\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/then/properties/foo\": \"Then\",\r\n                \"#/if/properties/foo\": \"If\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": \"\", \"bar\": 42 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/foo",
            "title",
            "{\r\n                \"#/else/properties/foo\": \"Else\"\r\n              }");
    }

    [TestMethod]
    public void Test1TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"if\": {\r\n          \"properties\": {\r\n            \"foo\": {\r\n              \"type\": \"string\",\r\n              \"title\": \"If\"\r\n            }\r\n          }\r\n        },\r\n        \"then\": {\r\n          \"properties\": {\r\n            \"foo\": { \"title\": \"Then\" }\r\n          }\r\n        },\r\n        \"else\": {\r\n          \"properties\": {\r\n            \"foo\": { \"title\": \"Else\" }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/allOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"allOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/anyOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"anyOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/oneOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"oneOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"not\": {\r\n          \"not\": {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
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
        (s_fixture as IDisposable)?.Dispose();
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
            "{\r\n                \"#/unevaluatedItems\": \"Unevaluated\"\r\n              }");
    }

    [TestMethod]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "[42, 24]",
            "/1",
            "title",
            "{\r\n                \"#/unevaluatedItems\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"unevaluatedItems\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft4.MetaData;

[TestCategory("Draft4")]
[TestClass]
public class SuiteTitleIsAnAnnotation
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
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"title\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft4.MetaData",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteDescriptionIsAnAnnotation
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
    public void Test0DescriptionRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "description",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"description\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft4.MetaData",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteDefaultIsAnAnnotation
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
    public void Test0DefaultRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "default",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"default\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft4.MetaData",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

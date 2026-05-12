using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft202012.Unknown;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteUnknownKeywordIsAnAnnotation
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
    public void Test0XUnknownKeywordRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "42",
            "",
            "x-unknownKeyword",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unknown.json",
                "{\r\n        \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n        \"x-unknownKeyword\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft202012.Unknown",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

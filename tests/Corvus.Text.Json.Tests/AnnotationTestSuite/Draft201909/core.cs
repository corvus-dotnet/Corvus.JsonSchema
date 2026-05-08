using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft201909.Core;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefAndDefs
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
            "{\r\n                \"#/$defs/foo\": \"Foo\"\r\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\r\n        \"$ref\": \"#/$defs/foo\",\r\n        \"$defs\": {\r\n          \"foo\": { \"title\": \"Foo\" }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Core",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft4.Applicators;

[TestCategory("Draft4")]
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
                "AnnotationTestSuite.Draft4.Applicators",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

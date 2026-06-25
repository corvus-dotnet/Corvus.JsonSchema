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
            "{\n                \"#/patternProperties/^a\": \"Bar\"\n              }");
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
                "AnnotationTestSuite.Draft4.Applicators",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationTestSuite.Draft202012.Core;

[TestCategory("Draft202012")]
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
            "{\n                \"#/$defs/foo\": \"Foo\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\n        \"$ref\": \"#/$defs/foo\",\n        \"$defs\": {\n          \"foo\": { \"title\": \"Foo\" }\n        }\n      }",
                "AnnotationTestSuite.Draft202012.Core",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefResolvesToDynamicAnchor
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
            "\"bar\"",
            "",
            "title",
            "{\n                \"#/$defs/foo\": \"Foo\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\n        \"$dynamicRef\": \"#foo\",\n        \"$defs\": {\n          \"foo\": {\n            \"$dynamicAnchor\": \"foo\",\n            \"title\": \"Foo\"\n          }\n        }\n      }",
                "AnnotationTestSuite.Draft202012.Core",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefResolvesToDifferentDynamicAnchorSDependingOnDynamicPath
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
    public void Test0TitleList0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"kindOfList\": \"numbers\", \"list\": [1] }",
            "/list/0",
            "title",
            "{\n                \"#/$defs/numberList/$defs/itemType\": \"Number Item\"\n              }");
    }

    [TestMethod]
    public void Test1TitleList0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            s_fixture!.Evaluator,
            "{ \"kindOfList\": \"strings\", \"list\": [\"foo\"] }",
            "/list/0",
            "title",
            "{\n                \"#/$defs/stringList/$defs/itemType\": \"String Item\"\n              }");
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\n        \"$id\": \"https://test.json-schema.org/dynamic-ref-annotation/main\",\n        \"if\": {\n          \"properties\": { \"kindOfList\": { \"const\": \"numbers\" } },\n          \"required\": [\"kindOfList\"]\n        },\n        \"then\": { \"$ref\": \"numberList\" },\n        \"else\": { \"$ref\": \"stringList\" },\n        \"$defs\": {\n          \"genericList\": {\n            \"$id\": \"genericList\",\n            \"properties\": {\n              \"list\": {\n                \"items\": { \"$dynamicRef\": \"#itemType\" }\n              }\n            },\n            \"$defs\": {\n              \"defaultItemType\": {\n                \"$dynamicAnchor\": \"itemType\"\n              }\n            }\n          },\n          \"numberList\": {\n            \"$id\": \"numberList\",\n            \"$defs\": {\n              \"itemType\": {\n                \"$dynamicAnchor\": \"itemType\",\n                \"title\": \"Number Item\"\n              }\n            },\n            \"$ref\": \"genericList\"\n          },\n          \"stringList\": {\n            \"$id\": \"stringList\",\n            \"$defs\": {\n              \"itemType\": {\n                \"$dynamicAnchor\": \"itemType\",\n                \"title\": \"String Item\"\n              }\n            },\n            \"$ref\": \"genericList\"\n          }\n        }\n      }",
                "AnnotationTestSuite.Draft202012.Core",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

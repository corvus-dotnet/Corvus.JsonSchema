using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.DynamicRef;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefToADynamicAnchorInTheSameSchemaResourceBehavesLikeANormalRefToAnAnchor
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
    public void TestAnArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayContainingNonStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamicRef-dynamicAnchor-same-schema/root\",\n            \"type\": \"array\",\n            \"items\": { \"$dynamicRef\": \"#items\" },\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefToAnAnchorInTheSameSchemaResourceBehavesLikeANormalRefToAnAnchor
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
    public void TestAnArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayContainingNonStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamicRef-anchor-same-schema/root\",\n            \"type\": \"array\",\n            \"items\": { \"$dynamicRef\": \"#items\" },\n            \"$defs\": {\n                \"foo\": {\n                    \"$anchor\": \"items\",\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteARefToADynamicAnchorInTheSameSchemaResourceBehavesLikeANormalRefToAnAnchor
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
    public void TestAnArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayContainingNonStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/ref-dynamicAnchor-same-schema/root\",\n            \"type\": \"array\",\n            \"items\": { \"$ref\": \"#items\" },\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefResolvesToTheFirstDynamicAnchorStillInScopeThatIsEncounteredWhenTheSchemaIsEvaluated
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
    public void TestAnArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayContainingNonStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/typical-dynamic-resolution/root\",\n            \"$ref\": \"list\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#items\" },\n                    \"$defs\": {\n                      \"items\": {\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\n                          \"$dynamicAnchor\": \"items\"\n                      }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefWithoutAnchorInFragmentBehavesIdenticalToRef
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
    public void TestAnArrayOfStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayOfNumbersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[24, 42]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamicRef-without-anchor/root\",\n            \"$ref\": \"list\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#/$defs/items\" },\n                    \"$defs\": {\n                      \"items\": {\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\n                          \"$dynamicAnchor\": \"items\",\n                          \"type\": \"number\"\n                      }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefWithIntermediateScopesThatDonTIncludeAMatchingDynamicAnchorDoesNotAffectDynamicScopeResolution
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
    public void TestAnArrayOfStringsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", \"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnArrayContainingNonStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-with-intermediate-scopes/root\",\n            \"$ref\": \"intermediate-scope\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"intermediate-scope\": {\n                    \"$id\": \"intermediate-scope\",\n                    \"$ref\": \"list\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#items\" },\n                    \"$defs\": {\n                      \"items\": {\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\n                          \"$dynamicAnchor\": \"items\"\n                      }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteAnAnchorWithTheSameNameAsADynamicAnchorIsNotUsedForDynamicScopeResolution
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
    public void TestAnyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-ignores-anchors/root\",\n            \"$ref\": \"list\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$anchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#items\" },\n                    \"$defs\": {\n                      \"items\": {\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\n                          \"$dynamicAnchor\": \"items\"\n                      }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefWithoutAMatchingDynamicAnchorInTheSameSchemaResourceBehavesLikeANormalRefToAnchor
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
    public void TestAnyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-without-bookend/root\",\n            \"$ref\": \"list\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#items\" },\n                    \"$defs\": {\n                        \"items\": {\n                            \"$comment\": \"This is only needed to give the reference somewhere to resolve to when it behaves like $ref\",\n                            \"$anchor\": \"items\"\n                        }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefWithANonMatchingDynamicAnchorInTheSameSchemaResourceBehavesLikeANormalRefToAnchor
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
    public void TestAnyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"foo\", 42]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/unmatched-dynamic-anchor/root\",\n            \"$ref\": \"list\",\n            \"$defs\": {\n                \"foo\": {\n                    \"$dynamicAnchor\": \"items\",\n                    \"type\": \"string\"\n                },\n                \"list\": {\n                    \"$id\": \"list\",\n                    \"type\": \"array\",\n                    \"items\": { \"$dynamicRef\": \"#items\" },\n                    \"$defs\": {\n                        \"items\": {\n                            \"$comment\": \"This is only needed to give the reference somewhere to resolve to when it behaves like $ref\",\n                            \"$anchor\": \"items\",\n                            \"$dynamicAnchor\": \"foo\"\n                        }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefThatInitiallyResolvesToASchemaWithAMatchingDynamicAnchorResolvesToTheFirstDynamicAnchorInTheDynamicScope
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
    public void TestTheRecursivePartIsValidAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"pass\",\n                    \"bar\": {\n                        \"baz\": { \"foo\": \"pass\" }\n                    }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTheRecursivePartIsNotValidAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"pass\",\n                    \"bar\": {\n                        \"baz\": { \"foo\": \"fail\" }\n                    }\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/relative-dynamic-reference/root\",\n            \"$dynamicAnchor\": \"meta\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"const\": \"pass\" }\n            },\n            \"$ref\": \"extended\",\n            \"$defs\": {\n                \"extended\": {\n                    \"$id\": \"extended\",\n                    \"$dynamicAnchor\": \"meta\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"bar\": { \"$ref\": \"bar\" }\n                    }\n                },\n                \"bar\": {\n                    \"$id\": \"bar\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"baz\": { \"$dynamicRef\": \"extended#meta\" }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteADynamicRefThatInitiallyResolvesToASchemaWithoutAMatchingDynamicAnchorBehavesLikeANormalRefToAnchor
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
    public void TestTheRecursivePartDoesnTNeedToValidateAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"pass\",\n                    \"bar\": {\n                        \"baz\": { \"foo\": \"fail\" }\n                    }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/relative-dynamic-reference-without-bookend/root\",\n            \"$dynamicAnchor\": \"meta\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"const\": \"pass\" }\n            },\n            \"$ref\": \"extended\",\n            \"$defs\": {\n                \"extended\": {\n                    \"$id\": \"extended\",\n                    \"$anchor\": \"meta\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"bar\": { \"$ref\": \"bar\" }\n                    }\n                },\n                \"bar\": {\n                    \"$id\": \"bar\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"baz\": { \"$dynamicRef\": \"extended#meta\" }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteMultipleDynamicPathsToTheDynamicRefKeyword
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
    public void TestNumberListWithNumberValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"kindOfList\": \"numbers\",\n                    \"list\": [1.1]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNumberListWithStringValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"kindOfList\": \"numbers\",\n                    \"list\": [\"foo\"]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringListWithNumberValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"kindOfList\": \"strings\",\n                    \"list\": [1.1]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringListWithStringValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"kindOfList\": \"strings\",\n                    \"list\": [\"foo\"]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-with-multiple-paths/main\",\n            \"if\": {\n                \"properties\": {\n                    \"kindOfList\": { \"const\": \"numbers\" }\n                },\n                \"required\": [\"kindOfList\"]\n            },\n            \"then\": { \"$ref\": \"numberList\" },\n            \"else\": { \"$ref\": \"stringList\" },\n\n            \"$defs\": {\n                \"genericList\": {\n                    \"$id\": \"genericList\",\n                    \"properties\": {\n                        \"list\": {\n                            \"items\": { \"$dynamicRef\": \"#itemType\" }\n                        }\n                    },\n                    \"$defs\": {\n                        \"defaultItemType\": {\n                            \"$comment\": \"Only needed to satisfy bookending requirement\",\n                            \"$dynamicAnchor\": \"itemType\"\n                        }\n                    }\n                },\n                \"numberList\": {\n                    \"$id\": \"numberList\",\n                    \"$defs\": {\n                        \"itemType\": {\n                            \"$dynamicAnchor\": \"itemType\",\n                            \"type\": \"number\"\n                        }\n                    },\n                    \"$ref\": \"genericList\"\n                },\n                \"stringList\": {\n                    \"$id\": \"stringList\",\n                    \"$defs\": {\n                        \"itemType\": {\n                            \"$dynamicAnchor\": \"itemType\",\n                            \"type\": \"string\"\n                        }\n                    },\n                    \"$ref\": \"genericList\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteAfterLeavingADynamicScopeItIsNotUsedByADynamicRef
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
    public void TestStringMatchesDefsThingyButTheDynamicRefDoesNotStopHere()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFirstScopeIsNotInDynamicScopeForTheDynamicRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestThenDefsThingyIsTheFinalStopForTheDynamicRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-leaving-dynamic-scope/main\",\n            \"if\": {\n                \"$id\": \"first_scope\",\n                \"$defs\": {\n                    \"thingy\": {\n                        \"$comment\": \"this is first_scope#thingy\",\n                        \"$dynamicAnchor\": \"thingy\",\n                        \"type\": \"number\"\n                    }\n                }\n            },\n            \"then\": {\n                \"$id\": \"second_scope\",\n                \"$ref\": \"start\",\n                \"$defs\": {\n                    \"thingy\": {\n                        \"$comment\": \"this is second_scope#thingy, the final destination of the $dynamicRef\",\n                        \"$dynamicAnchor\": \"thingy\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"$defs\": {\n                \"start\": {\n                    \"$comment\": \"this is the landing spot from $ref\",\n                    \"$id\": \"start\",\n                    \"$dynamicRef\": \"inner_scope#thingy\"\n                },\n                \"thingy\": {\n                    \"$comment\": \"this is the first stop for the $dynamicRef\",\n                    \"$id\": \"inner_scope\",\n                    \"$dynamicAnchor\": \"thingy\",\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteStrictTreeSchemaGuardsAgainstMisspelledProperties
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
    public void TestInstanceWithMisspelledField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"children\": [{\n                            \"daat\": 1\n                        }]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInstanceWithCorrectField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"children\": [{\n                            \"data\": 1\n                        }]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-tree.json\",\n            \"$dynamicAnchor\": \"node\",\n\n            \"$ref\": \"tree.json\",\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteTestsForImplementationDynamicAnchorAndReferenceLink
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
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"a\": true\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"b\": 1 }\n                    ]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"a\": 1 }\n                    ]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible.json\",\n            \"$ref\": \"extendible-dynamic-ref.json\",\n            \"$defs\": {\n                \"elements\": {\n                    \"$dynamicAnchor\": \"elements\",\n                    \"properties\": {\n                        \"a\": true\n                    },\n                    \"required\": [\"a\"],\n                    \"additionalProperties\": false\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefAndDynamicAnchorAreIndependentOfOrderDefsFirst
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
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"a\": true\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"b\": 1 }\n                    ]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"a\": 1 }\n                    ]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible-allof-defs-first.json\",\n            \"allOf\": [\n                {\n                    \"$ref\": \"extendible-dynamic-ref.json\"\n                },\n                {\n                    \"$defs\": {\n                        \"elements\": {\n                            \"$dynamicAnchor\": \"elements\",\n                            \"properties\": {\n                                \"a\": true\n                            },\n                            \"required\": [\"a\"],\n                            \"additionalProperties\": false\n                        }\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefAndDynamicAnchorAreIndependentOfOrderRefFirst
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
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"a\": true\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"b\": 1 }\n                    ]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"elements\": [\n                        { \"a\": 1 }\n                    ]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible-allof-ref-first.json\",\n            \"allOf\": [\n                {\n                    \"$defs\": {\n                        \"elements\": {\n                            \"$dynamicAnchor\": \"elements\",\n                            \"properties\": {\n                                \"a\": true\n                            },\n                            \"required\": [\"a\"],\n                            \"additionalProperties\": false\n                        }\n                    }\n                },\n                {\n                    \"$ref\": \"extendible-dynamic-ref.json\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefToDynamicRefFindsDetachedDynamicAnchor
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
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$ref\": \"http://localhost:1234/draft2020-12/detached-dynamicref.json#/$defs/foo\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefPointsToABooleanSchema
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
    public void TestFollowDynamicRefToATrueSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"true\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFollowDynamicRefToAFalseSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"false\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$defs\": {\n                \"true\": true,\n                \"false\": false\n            },\n            \"properties\": {\n                \"true\": {\n                    \"$dynamicRef\": \"#/$defs/true\"\n                },\n                \"false\": {\n                    \"$dynamicRef\": \"#/$defs/false\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefSkipsOverIntermediateResourcesDirectReference
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
    public void TestIntegerPropertyPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar-item\": { \"content\": 42 } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringPropertyFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar-item\": { \"content\": \"value\" } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-skips-intermediate-resource/main\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"bar-item\": {\n                    \"$ref\": \"item\"\n                }\n            },\n            \"$defs\": {\n                \"bar\": {\n                    \"$id\": \"bar\",\n                    \"type\": \"array\",\n                    \"items\": {\n                        \"$ref\": \"item\"\n                    },\n                    \"$defs\": {\n                        \"item\": {\n                            \"$id\": \"item\",\n                            \"type\": \"object\",\n                            \"properties\": {\n                                \"content\": {\n                                    \"$dynamicRef\": \"#content\"\n                                }\n                            },\n                            \"$defs\": {\n                                \"defaultContent\": {\n                                    \"$dynamicAnchor\": \"content\",\n                                    \"type\": \"integer\"\n                                }\n                            }\n                        },\n                        \"content\": {\n                            \"$dynamicAnchor\": \"content\",\n                            \"type\": \"string\"\n                        }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefAvoidsTheRootOfEachSchemaButScopesAreStillRegistered
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
    public void TestDataIsSufficientForSchemaAtSecondDefsLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hi\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDataIsNotSufficientForSchemaAtSecondDefsLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hey\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/dynamicRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-avoids-root-of-each-schema/base\",\n            \"$ref\": \"first#/$defs/stuff\",\n            \"$defs\": {\n                \"first\": {\n                    \"$id\": \"first\",\n                    \"$defs\": {\n                        \"stuff\": {\n                            \"$ref\": \"second#/$defs/stuff\"\n                        },\n                        \"length\": {\n                            \"$comment\": \"unused, because there is no $dynamicAnchor here\",\n                            \"maxLength\": 1\n                        }\n                    }\n                },\n                \"second\": {\n                    \"$id\": \"second\",\n                    \"$defs\": {\n                        \"stuff\": {\n                            \"$ref\": \"third#/$defs/stuff\"\n                        },\n                        \"length\": {\n                            \"$dynamicAnchor\": \"length\",\n                            \"maxLength\": 2\n                        }\n                    }\n                },\n                \"third\": {\n                    \"$id\": \"third\",\n                    \"$defs\": {\n                        \"stuff\": {\n                            \"$dynamicRef\": \"#length\"\n                        },\n                        \"length\": {\n                            \"$dynamicAnchor\": \"length\",\n                            \"maxLength\": 3\n                        }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

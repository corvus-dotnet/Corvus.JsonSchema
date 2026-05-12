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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamicRef-dynamicAnchor-same-schema/root\",\r\n            \"type\": \"array\",\r\n            \"items\": { \"$dynamicRef\": \"#items\" },\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamicRef-anchor-same-schema/root\",\r\n            \"type\": \"array\",\r\n            \"items\": { \"$dynamicRef\": \"#items\" },\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$anchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/ref-dynamicAnchor-same-schema/root\",\r\n            \"type\": \"array\",\r\n            \"items\": { \"$ref\": \"#items\" },\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/typical-dynamic-resolution/root\",\r\n            \"$ref\": \"list\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#items\" },\r\n                    \"$defs\": {\r\n                      \"items\": {\r\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\r\n                          \"$dynamicAnchor\": \"items\"\r\n                      }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamicRef-without-anchor/root\",\r\n            \"$ref\": \"list\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#/$defs/items\" },\r\n                    \"$defs\": {\r\n                      \"items\": {\r\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\r\n                          \"$dynamicAnchor\": \"items\",\r\n                          \"type\": \"number\"\r\n                      }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-with-intermediate-scopes/root\",\r\n            \"$ref\": \"intermediate-scope\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"intermediate-scope\": {\r\n                    \"$id\": \"intermediate-scope\",\r\n                    \"$ref\": \"list\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#items\" },\r\n                    \"$defs\": {\r\n                      \"items\": {\r\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\r\n                          \"$dynamicAnchor\": \"items\"\r\n                      }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-ignores-anchors/root\",\r\n            \"$ref\": \"list\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$anchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#items\" },\r\n                    \"$defs\": {\r\n                      \"items\": {\r\n                          \"$comment\": \"This is only needed to satisfy the bookending requirement\",\r\n                          \"$dynamicAnchor\": \"items\"\r\n                      }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-resolution-without-bookend/root\",\r\n            \"$ref\": \"list\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#items\" },\r\n                    \"$defs\": {\r\n                        \"items\": {\r\n                            \"$comment\": \"This is only needed to give the reference somewhere to resolve to when it behaves like $ref\",\r\n                            \"$anchor\": \"items\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/unmatched-dynamic-anchor/root\",\r\n            \"$ref\": \"list\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$dynamicAnchor\": \"items\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"list\": {\r\n                    \"$id\": \"list\",\r\n                    \"type\": \"array\",\r\n                    \"items\": { \"$dynamicRef\": \"#items\" },\r\n                    \"$defs\": {\r\n                        \"items\": {\r\n                            \"$comment\": \"This is only needed to give the reference somewhere to resolve to when it behaves like $ref\",\r\n                            \"$anchor\": \"items\",\r\n                            \"$dynamicAnchor\": \"foo\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestTheRecursivePartIsValidAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"pass\",\r\n                    \"bar\": {\r\n                        \"baz\": { \"foo\": \"pass\" }\r\n                    }\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTheRecursivePartIsNotValidAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"pass\",\r\n                    \"bar\": {\r\n                        \"baz\": { \"foo\": \"fail\" }\r\n                    }\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/relative-dynamic-reference/root\",\r\n            \"$dynamicAnchor\": \"meta\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"const\": \"pass\" }\r\n            },\r\n            \"$ref\": \"extended\",\r\n            \"$defs\": {\r\n                \"extended\": {\r\n                    \"$id\": \"extended\",\r\n                    \"$dynamicAnchor\": \"meta\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"bar\": { \"$ref\": \"bar\" }\r\n                    }\r\n                },\r\n                \"bar\": {\r\n                    \"$id\": \"bar\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"baz\": { \"$dynamicRef\": \"extended#meta\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestTheRecursivePartDoesnTNeedToValidateAgainstTheRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"pass\",\r\n                    \"bar\": {\r\n                        \"baz\": { \"foo\": \"fail\" }\r\n                    }\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/relative-dynamic-reference-without-bookend/root\",\r\n            \"$dynamicAnchor\": \"meta\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"const\": \"pass\" }\r\n            },\r\n            \"$ref\": \"extended\",\r\n            \"$defs\": {\r\n                \"extended\": {\r\n                    \"$id\": \"extended\",\r\n                    \"$anchor\": \"meta\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"bar\": { \"$ref\": \"bar\" }\r\n                    }\r\n                },\r\n                \"bar\": {\r\n                    \"$id\": \"bar\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"baz\": { \"$dynamicRef\": \"extended#meta\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestNumberListWithNumberValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"kindOfList\": \"numbers\",\r\n                    \"list\": [1.1]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNumberListWithStringValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"kindOfList\": \"numbers\",\r\n                    \"list\": [\"foo\"]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringListWithNumberValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"kindOfList\": \"strings\",\r\n                    \"list\": [1.1]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringListWithStringValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"kindOfList\": \"strings\",\r\n                    \"list\": [\"foo\"]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-with-multiple-paths/main\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"kindOfList\": { \"const\": \"numbers\" }\r\n                },\r\n                \"required\": [\"kindOfList\"]\r\n            },\r\n            \"then\": { \"$ref\": \"numberList\" },\r\n            \"else\": { \"$ref\": \"stringList\" },\r\n\r\n            \"$defs\": {\r\n                \"genericList\": {\r\n                    \"$id\": \"genericList\",\r\n                    \"properties\": {\r\n                        \"list\": {\r\n                            \"items\": { \"$dynamicRef\": \"#itemType\" }\r\n                        }\r\n                    },\r\n                    \"$defs\": {\r\n                        \"defaultItemType\": {\r\n                            \"$comment\": \"Only needed to satisfy bookending requirement\",\r\n                            \"$dynamicAnchor\": \"itemType\"\r\n                        }\r\n                    }\r\n                },\r\n                \"numberList\": {\r\n                    \"$id\": \"numberList\",\r\n                    \"$defs\": {\r\n                        \"itemType\": {\r\n                            \"$dynamicAnchor\": \"itemType\",\r\n                            \"type\": \"number\"\r\n                        }\r\n                    },\r\n                    \"$ref\": \"genericList\"\r\n                },\r\n                \"stringList\": {\r\n                    \"$id\": \"stringList\",\r\n                    \"$defs\": {\r\n                        \"itemType\": {\r\n                            \"$dynamicAnchor\": \"itemType\",\r\n                            \"type\": \"string\"\r\n                        }\r\n                    },\r\n                    \"$ref\": \"genericList\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-leaving-dynamic-scope/main\",\r\n            \"if\": {\r\n                \"$id\": \"first_scope\",\r\n                \"$defs\": {\r\n                    \"thingy\": {\r\n                        \"$comment\": \"this is first_scope#thingy\",\r\n                        \"$dynamicAnchor\": \"thingy\",\r\n                        \"type\": \"number\"\r\n                    }\r\n                }\r\n            },\r\n            \"then\": {\r\n                \"$id\": \"second_scope\",\r\n                \"$ref\": \"start\",\r\n                \"$defs\": {\r\n                    \"thingy\": {\r\n                        \"$comment\": \"this is second_scope#thingy, the final destination of the $dynamicRef\",\r\n                        \"$dynamicAnchor\": \"thingy\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"$defs\": {\r\n                \"start\": {\r\n                    \"$comment\": \"this is the landing spot from $ref\",\r\n                    \"$id\": \"start\",\r\n                    \"$dynamicRef\": \"inner_scope#thingy\"\r\n                },\r\n                \"thingy\": {\r\n                    \"$comment\": \"this is the first stop for the $dynamicRef\",\r\n                    \"$id\": \"inner_scope\",\r\n                    \"$dynamicAnchor\": \"thingy\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestInstanceWithMisspelledField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"children\": [{\r\n                            \"daat\": 1\r\n                        }]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInstanceWithCorrectField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"children\": [{\r\n                            \"data\": 1\r\n                        }]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-tree.json\",\r\n            \"$dynamicAnchor\": \"node\",\r\n\r\n            \"$ref\": \"tree.json\",\r\n            \"unevaluatedProperties\": false\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"a\": true\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"b\": 1 }\r\n                    ]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"a\": 1 }\r\n                    ]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible.json\",\r\n            \"$ref\": \"extendible-dynamic-ref.json\",\r\n            \"$defs\": {\r\n                \"elements\": {\r\n                    \"$dynamicAnchor\": \"elements\",\r\n                    \"properties\": {\r\n                        \"a\": true\r\n                    },\r\n                    \"required\": [\"a\"],\r\n                    \"additionalProperties\": false\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"a\": true\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"b\": 1 }\r\n                    ]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"a\": 1 }\r\n                    ]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible-allof-defs-first.json\",\r\n            \"allOf\": [\r\n                {\r\n                    \"$ref\": \"extendible-dynamic-ref.json\"\r\n                },\r\n                {\r\n                    \"$defs\": {\r\n                        \"elements\": {\r\n                            \"$dynamicAnchor\": \"elements\",\r\n                            \"properties\": {\r\n                                \"a\": true\r\n                            },\r\n                            \"required\": [\"a\"],\r\n                            \"additionalProperties\": false\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestIncorrectParentSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"a\": true\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"b\": 1 }\r\n                    ]\r\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCorrectExtendedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"elements\": [\r\n                        { \"a\": 1 }\r\n                    ]\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/strict-extendible-allof-ref-first.json\",\r\n            \"allOf\": [\r\n                {\r\n                    \"$defs\": {\r\n                        \"elements\": {\r\n                            \"$dynamicAnchor\": \"elements\",\r\n                            \"properties\": {\r\n                                \"a\": true\r\n                            },\r\n                            \"required\": [\"a\"],\r\n                            \"additionalProperties\": false\r\n                        }\r\n                    }\r\n                },\r\n                {\r\n                    \"$ref\": \"extendible-dynamic-ref.json\"\r\n                }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/detached-dynamicref.json#/$defs/foo\"\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"true\": true,\r\n                \"false\": false\r\n            },\r\n            \"properties\": {\r\n                \"true\": {\r\n                    \"$dynamicRef\": \"#/$defs/true\"\r\n                },\r\n                \"false\": {\r\n                    \"$dynamicRef\": \"#/$defs/false\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-skips-intermediate-resource/main\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"bar-item\": {\r\n                    \"$ref\": \"item\"\r\n                }\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"$id\": \"bar\",\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"$ref\": \"item\"\r\n                    },\r\n                    \"$defs\": {\r\n                        \"item\": {\r\n                            \"$id\": \"item\",\r\n                            \"type\": \"object\",\r\n                            \"properties\": {\r\n                                \"content\": {\r\n                                    \"$dynamicRef\": \"#content\"\r\n                                }\r\n                            },\r\n                            \"$defs\": {\r\n                                \"defaultContent\": {\r\n                                    \"$dynamicAnchor\": \"content\",\r\n                                    \"type\": \"integer\"\r\n                                }\r\n                            }\r\n                        },\r\n                        \"content\": {\r\n                            \"$dynamicAnchor\": \"content\",\r\n                            \"type\": \"string\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\dynamicRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://test.json-schema.org/dynamic-ref-avoids-root-of-each-schema/base\",\r\n            \"$ref\": \"first#/$defs/stuff\",\r\n            \"$defs\": {\r\n                \"first\": {\r\n                    \"$id\": \"first\",\r\n                    \"$defs\": {\r\n                        \"stuff\": {\r\n                            \"$ref\": \"second#/$defs/stuff\"\r\n                        },\r\n                        \"length\": {\r\n                            \"$comment\": \"unused, because there is no $dynamicAnchor here\",\r\n                            \"maxLength\": 1\r\n                        }\r\n                    }\r\n                },\r\n                \"second\": {\r\n                    \"$id\": \"second\",\r\n                    \"$defs\": {\r\n                        \"stuff\": {\r\n                            \"$ref\": \"third#/$defs/stuff\"\r\n                        },\r\n                        \"length\": {\r\n                            \"$dynamicAnchor\": \"length\",\r\n                            \"maxLength\": 2\r\n                        }\r\n                    }\r\n                },\r\n                \"third\": {\r\n                    \"$id\": \"third\",\r\n                    \"$defs\": {\r\n                        \"stuff\": {\r\n                            \"$dynamicRef\": \"#length\"\r\n                        },\r\n                        \"length\": {\r\n                            \"$dynamicAnchor\": \"length\",\r\n                            \"maxLength\": 3\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.DynamicRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

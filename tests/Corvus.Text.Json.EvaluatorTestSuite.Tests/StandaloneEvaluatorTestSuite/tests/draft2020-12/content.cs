using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Content;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfStringEncodedContentBasedOnMediaType
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
    public void TestAValidJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{:}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/content.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contentMediaType\": \"application/json\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfBinaryStringEncoding
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
    public void TestAValidBase64String()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidBase64StringIsNotAValidCharacterValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/content.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contentEncoding\": \"base64\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfBinaryEncodedMediaTypeDocuments
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
    public void TestAValidBase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ezp9Cg==\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/content.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contentMediaType\": \"application/json\",\n            \"contentEncoding\": \"base64\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfBinaryEncodedMediaTypeDocumentsWithSchema
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
    public void TestAValidBase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnotherValidBase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJib28iOiAyMCwgImZvbyI6ICJiYXoifQ==\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidBase64EncodedJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJib28iOiAyMH0=\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyObjectAsABase64EncodedJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"e30=\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyArrayAsABase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"W10=\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ezp9Cg==\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/content.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contentMediaType\": \"application/json\",\n            \"contentEncoding\": \"base64\",\n            \"contentSchema\": { \"type\": \"object\", \"required\": [\"foo\"], \"properties\": { \"foo\": { \"type\": \"string\" } } }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

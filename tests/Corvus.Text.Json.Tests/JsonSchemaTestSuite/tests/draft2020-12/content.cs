using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Content;

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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAValidJsonDocument()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"{\\\"foo\\\": \\\"bar\\\"}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"{:}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("100");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAValidBase64String()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidBase64StringIsNotAValidCharacterValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("100");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ezp9Cg==\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"{}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("100");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnotherValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJib28iOiAyMCwgImZvbyI6ICJiYXoifQ==\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidBase64EncodedJsonDocumentValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"eyJib28iOiAyMH0=\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEmptyObjectAsABase64EncodedJsonDocumentValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"e30=\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEmptyArrayAsABase64EncodedJsonDocument()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"W10=\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ezp9Cg==\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"{}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("100");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\",\r\n            \"contentSchema\": { \"type\": \"object\", \"required\": [\"foo\"], \"properties\": { \"foo\": { \"type\": \"string\" } } }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

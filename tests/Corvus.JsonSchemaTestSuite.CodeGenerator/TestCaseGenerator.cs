// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using System.Xml;
using Corvus.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using TestUtilities;
using TestUtilities.JsonSchemaTestSuite;

#pragma warning disable CS8618

namespace Corvus.JsonSchemaTestSuite.CodeGenerator;

internal static class TestCaseGenerator
{
    private static readonly IConfiguration s_config;

    static TestCaseGenerator()
    {
        s_config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }

    public static void GenerateTests(Action<string> fileCallback)
    {
        StringBuilder builder = new();

        string outputPath = s_config["outputPath"] ?? throw new InvalidOperationException("You must provide an <outputPath> in app settings.");

        outputPath = Path.GetFullPath(outputPath);

        // Delete the output directory
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        static string GetNamespaceFromPath(string path)
        {
            string namespacePath = Path.GetDirectoryName(path)!;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path)!;
            string[] segments = namespacePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            string fileNameSegment = TestJsonSchemaCodeGenerator.ToPascalCase(fileNameWithoutExtension);
            if (segments.Length > 0)
            {
                return $"{string.Join('.', segments.Select(p => TestJsonSchemaCodeGenerator.ToPascalCase(p)))}.{fileNameSegment}";
            }

            return fileNameSegment;
        }

        foreach (TestGroup testGroup in ProvideTestGroups())
        {
            string namespaceGroup = TestJsonSchemaCodeGenerator.ToPascalCase(testGroup.Name);

            foreach (TestFile testFile in testGroup.Files)
            {

                HashSet<string> suiteNames = [];

                string relativePath = Path.ChangeExtension(testFile.RelativePath, ".cs");
                string outputFile = Path.Combine(outputPath, relativePath);

                string namespaceSuffix = GetNamespaceFromPath(testFile.NamespaceRelativePath);
                string namespaceValue = $"JsonSchemaTestSuite.{namespaceGroup}{(namespaceSuffix.Length > 0 ? "." : string.Empty)}{namespaceSuffix}";
                fileCallback(relativePath);

                // Work on a new test file
                builder.Clear();

                builder
                    .AppendLine("using System.Reflection;")
                    .AppendLine("using System.Threading.Tasks;")
                    .AppendLine("using TestUtilities;")
                    .AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;")
                    .AppendLine()
                    .AppendLine($"namespace {namespaceValue};");

                string current = Path.GetFullPath(".");
                // The schema is registered as a virtual document under the suite's tests directory; any
                // other documents it references come from the test case's own externalSchemas.
                string remotesDirectory = Path.Combine(testFile.BaseDirectory, "validation", "tests");

                foreach (TestSuite testSuite in testFile.TestSuites)
                {
                    string pascalSuiteName = GetUniqueName(TestJsonSchemaCodeGenerator.ToPascalCase(testSuite.SuiteName), suiteNames);
                    builder
                        .AppendLine()
                        .AppendLine($"[TestCategory(\"{namespaceGroup}\")]")
                        .AppendLine("[TestClass]")
                        .AppendLine($"public class Suite{pascalSuiteName}")
                        .AppendLine("{")
                        .AppendLine($"    private static Fixture? s_fixture;")
                        .AppendLine()
                        .AppendLine("    [ClassInitialize]")
                        .AppendLine($"    public static async Task ClassInit(TestContext _)")
                        .AppendLine("    {")
                        .AppendLine("        s_fixture = new Fixture();")
                        .AppendLine("        await s_fixture.InitializeAsync();")
                        .AppendLine("    }")
                        .AppendLine()
                        .AppendLine("    [ClassCleanup]")
                        .AppendLine("    public static void ClassCleanupMethod()")
                        .AppendLine("    {")
                        .AppendLine("        s_fixture = null;")
                        .AppendLine("    }");

                    HashSet<string> testNames = [];

                    foreach (TestSpecification test in testSuite.TestSpecifications)
                    {
                        string testName = GetUniqueName(TestJsonSchemaCodeGenerator.ToPascalCase(test.TestDescription), testNames);

                        builder
                            .AppendLine()
                            .AppendLine("    [TestMethod]")
                            .AppendLine($"    public void Test{testName}()")
                            .AppendLine("    {")
                            .AppendLine($"        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance({SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(test.Instance.ToString()), true)});")
                            .AppendLine($"        Assert.{(test.Expectation ? "IsTrue" : "IsFalse")}(dynamicInstance.EvaluateSchema());")
                            .AppendLine("    }");
                    }

                    builder
                        .AppendLine()
                        .AppendLine("    public class Fixture")
                        .AppendLine("    {")
                        .AppendLine("        public DynamicJsonType DynamicJsonType { get; private set; }")
                        .AppendLine()
                        .AppendLine("        public async Task InitializeAsync()")
                        .AppendLine("        {")
                        .AppendLine("            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(EmbeddedLiteral.ForwardSlash(testFile.RelativePath), true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(testSuite.Schema.ToString()), true)},")
                        .AppendLine($"                \"{namespaceValue}\",")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(EmbeddedLiteral.ForwardSlash(remotesDirectory), true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testGroup.DefaultVocabulary, true)},")
                        .AppendLine($"                validateFormat: {(testSuite.ValidateFormat ? "true" : "false")},")
                        .AppendLine("                optionalAsNullable: false,")
                        .AppendLine("                useImplicitOperatorString: false,")
                        .AppendLine("                addExplicitUsings: false,")
                        .AppendLine(testSuite.ExternalSchemas is null ? "                Assembly.GetExecutingAssembly());" : "                Assembly.GetExecutingAssembly(),")
                        .AppendLineIf(testSuite.ExternalSchemas is not null, () => $"                externalSchemas: {SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(testSuite.ExternalSchemas!), true)});")
                        .AppendLine("        }")
                        .AppendLine("    }")
                        .AppendLine("}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllText(outputFile, builder.ToString());
            }
        }
    }

    private static string GetUniqueName(string v, HashSet<string> suiteNames)
    {
        if (suiteNames.Add(v))
        {
            return v;
        }

        int index = 1;
        while (true)
        {
            string testName = $"{v}{index++}";
            if (suiteNames.Add(testName))
            {
                return testName;
            }
        }
    }

    public static IEnumerable<TestGroup> ProvideTestGroups()
    {
        TestConfiguration jsonSchemaTestSuite = new();
        s_config.Bind("jsonSchemaTestSuite", jsonSchemaTestSuite);

        // Always work with the full path
        string baseDirectory = Path.GetFullPath(jsonSchemaTestSuite.BaseDirectory);
        string testsDirectory = Path.Combine(baseDirectory, jsonSchemaTestSuite.TestsDirectory);

        foreach (TestCollection collection in jsonSchemaTestSuite.Collections)
        {
            List<TestFile> testFiles = [];

            // The unified suite holds every dialect in one tree; each test case says which
            // releases it applies to in its "compatibility" property.
            foreach (string file in Directory.EnumerateFiles(testsDirectory, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                List<TestSuite> testSuites = [];

                string relativePath = Path.GetRelativePath(testsDirectory, file).Replace('\\', '/');

                if (jsonSchemaTestSuite.ExcludedDirectories?.Any(d => relativePath.StartsWith(d.TrimEnd('/') + "/", StringComparison.Ordinal)) == true)
                {
                    continue;
                }

                PathOptions? pathOptions = collection.Options?.FirstOrDefault(fe => IsInPath(currentPath: relativePath, basePath: fe.Path));
                FileExclusions? fileExclusions = collection.Exclusions?.FirstOrDefault(fe => EqualsPath(fe.File, relativePath));

                if (fileExclusions is FileExclusions f && f.Suites is null)
                {
                    // We have exclusions for the file, but have not set suites at all, which means we should exclude the file.
                    continue;
                }

                using System.Text.Json.JsonDocument model = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));

                foreach (System.Text.Json.JsonElement testSuite in model.RootElement.GetProperty("tests").EnumerateArray())
                {
                    string? compatibility = testSuite.TryGetProperty("compatibility", out System.Text.Json.JsonElement compatibilityElement)
                        ? compatibilityElement.GetString()
                        : null;

                    if (!SuiteCompatibility.IsCompatible(compatibility, collection.Release))
                    {
                        continue;
                    }

                    bool isFormatAnnotation = testSuite.TryGetProperty("tags", out System.Text.Json.JsonElement caseTags) &&
                        caseTags.EnumerateArray().Any(t => t.ValueEquals("format-annotation"));

                    // Where the dialect asserts format by default (draft4 to draft7 in this
                    // implementation, which the specification allows), a "format-annotation" case
                    // describes behaviour the implementation does not provide, so it does not apply.
                    if (isFormatAnnotation && collection.FormatAssertedByDefault)
                    {
                        continue;
                    }

                    List<TestSpecification> testList = [];
                    string suiteName = testSuite.GetProperty("description").GetString()!;

                    SuiteExclusions? suiteExclusions = null;

                    if (fileExclusions is FileExclusions fe)
                    {
                        suiteExclusions = fe.Suites?.FirstOrDefault(s => s.Name == suiteName);
                    }

                    if (suiteExclusions is SuiteExclusions wholeSuite && wholeSuite.Tests is null)
                    {
                        // We have exclusions for the suite, but have not set tests at all, which means we should exclude the suite.
                        continue;
                    }

                    foreach (System.Text.Json.JsonElement test in testSuite.GetProperty("tests").EnumerateArray())
                    {
                        string testDescription = test.GetProperty("description").GetString()!;

                        if (suiteExclusions is SuiteExclusions se &&
                            se.Tests is List<TestExclusion> te &&
                            te.Any(t => t.Name == testDescription))
                        {
                            continue;
                        }

                        testList.Add(
                            new TestSpecification(
                                testDescription,
                                JsonAny.Parse(test.GetProperty("data").GetRawText()),
                                test.GetProperty("valid").GetBoolean()));
                    }

                    if (testList.Count > 0)
                    {
                        // A "format-assertion" tag means the case expects format to be asserted;
                        // "format-annotation" (or no tag) means the dialect's default behaviour.
                        bool validateFormat = pathOptions?.ValidateFormat ?? false;
                        if (testSuite.TryGetProperty("tags", out System.Text.Json.JsonElement tags) &&
                            tags.EnumerateArray().Any(t => t.ValueEquals("format-assertion")))
                        {
                            validateFormat = true;
                        }

                        string? externalSchemas = testSuite.TryGetProperty("externalSchemas", out System.Text.Json.JsonElement externalSchemasElement)
                            ? externalSchemasElement.GetRawText()
                            : null;

                        testSuites.Add(
                            new TestSuite(
                                suiteName,
                                JsonAny.Parse(testSuite.GetProperty("schema").GetRawText()),
                                testList,
                                validateFormat,
                                externalSchemas));
                    }
                }

                if (testSuites.Count > 0)
                {
                    // Keep the generated tree as tests/<dialect>/<file>, as it was when the suite
                    // had one directory per dialect.
                    testFiles.Add(new TestFile(jsonSchemaTestSuite.BaseDirectory, $"tests/{collection.Name}/{relativePath}", relativePath, testSuites));
                }
            }

            if (testFiles.Count > 0)
            {
                yield return new(collection.Name, collection.DefaultVocabulary, testFiles);
            }
        }
    }

    private static bool IsInPath(string currentPath, string basePath)
    {
        return NormalizePath(currentPath).StartsWith(NormalizePath(basePath));

    }

    private static bool EqualsPath(string currentPath, string basePath)
    {
        return NormalizePath(currentPath).Equals(NormalizePath(basePath));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    public class TestConfiguration
    {
        public string BaseDirectory { get; set; }
        public string TestsDirectory { get; set; }
        public List<string>? ExcludedDirectories { get; set; }
        public List<TestCollection> Collections { get; set; }
    }

    public class TestCollection
    {
        public string Name { get; set; }
        public int Release { get; set; }
        public bool FormatAssertedByDefault { get; set; }
        public string DefaultVocabulary { get; set; }
        public List<PathOptions>? Options { get; set; }
        public List<FileExclusions>? Exclusions { get; set; }
    }

    public class PathOptions
    {
        public string Path { get; set; }
        public bool? ValidateFormat { get; set; }
    }

    public class FileExclusions
    {
        public string File { get; set; }
        public List<SuiteExclusions>? Suites { get; set; }
    }

    public class SuiteExclusions
    {
        public string Name { get; set; }
        public List<TestExclusion>? Tests { get; set; }
    }

    public class TestExclusion
    {
        public string Name { get; set; }
        public string Reason { get; set; }
    }
}
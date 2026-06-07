// <copyright file="AnnotationTestCaseGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.JsonSchemaTestSuite.CodeGenerator;

/// <summary>
/// Generates MSTest test classes for the JSON Schema annotation test suite,
/// using the standalone evaluator infrastructure.
/// </summary>
internal static class AnnotationTestCaseGenerator
{
    private static readonly (string Name, string Vocabulary, string MinCompat)[] Drafts =
    [
        ("Draft4", "http://json-schema.org/draft-04/schema#", "3"),
        ("Draft6", "http://json-schema.org/draft-06/schema#", "6"),
        ("Draft7", "http://json-schema.org/draft-07/schema#", "7"),
        ("Draft201909", "https://json-schema.org/draft/2019-09/schema", "2019"),
        ("Draft202012", "https://json-schema.org/draft/2020-12/schema", "2020"),
    ];

    // The compatibility ordering — a draft is compatible if its compat level >= suite compat level.
    private static readonly string[] CompatOrder = ["3", "4", "6", "7", "2019", "2020"];

    public static void GenerateTests(string baseDirectory, string outputPath, Action<string> fileCallback)
    {
        // Resolve to an absolute path for reading the annotation files; the embedded remotes
        // path below stays relative (machine-independent) like the type/evaluator generators.
        string annotationsDir = Path.Combine(Path.GetFullPath(baseDirectory), "annotations", "tests");
        if (!Directory.Exists(annotationsDir))
        {
            Console.WriteLine($"Annotations directory not found: {annotationsDir}");
            return;
        }

        outputPath = Path.GetFullPath(outputPath);

        // Delete the output directory
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        string remotesDirectory = Path.Combine(baseDirectory, "remotes");

        foreach (string file in Directory.EnumerateFiles(annotationsDir, "*.json"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string pascalFileName = ToPascalCase(fileName);

            string json = File.ReadAllText(file);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("suite", out JsonElement suiteArray))
            {
                continue;
            }

            // For each draft, generate a test file with applicable suites.
            foreach ((string draftName, string vocabulary, string minCompat) in Drafts)
            {
                StringBuilder builder = new();
                bool hasSuites = false;
                HashSet<string> suiteNames = [];

                builder
                    .AppendLine("using System.Collections.Generic;")
                    .AppendLine("using System.Reflection;")
                    .AppendLine("using System.Threading.Tasks;")
                    .AppendLine("using Corvus.Text.Json;")
                    .AppendLine("using TestUtilities;")
                    .AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;")
                    .AppendLine()
                    .AppendLine($"namespace AnnotationTestSuite.{draftName}.{pascalFileName};");

                foreach (JsonElement suite in suiteArray.EnumerateArray())
                {
                    string suiteCompat = suite.TryGetProperty("compatibility", out JsonElement compatEl)
                        ? compatEl.GetString()!
                        : "3"; // Default: all drafts

                    if (!IsCompatible(minCompat, suiteCompat))
                    {
                        continue;
                    }

                    string suiteDescription = suite.GetProperty("description").GetString()!;
                    string pascalSuiteName = GetUniqueName(ToPascalCase(suiteDescription), suiteNames);
                    string schemaJson = suite.GetProperty("schema").GetRawText();

                    hasSuites = true;

                    builder
                        .AppendLine()
                        .AppendLine($"[TestCategory(\"{draftName}\")]")
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
                    int testIndex = 0;

                    foreach (JsonElement test in suite.GetProperty("tests").EnumerateArray())
                    {
                        string instanceJson = test.GetProperty("instance").GetRawText();
                        JsonElement assertions = test.GetProperty("assertions");

                        int assertionIndex = 0;
                        foreach (JsonElement assertion in assertions.EnumerateArray())
                        {
                            string location = assertion.GetProperty("location").GetString()!;
                            string keyword = assertion.GetProperty("keyword").GetString()!;
                            JsonElement expected = assertion.GetProperty("expected");

                            string testName = GetUniqueName(
                                ToPascalCase($"Test{testIndex}_{keyword}_{SanitizeLocation(location)}_Assertion{assertionIndex}"),
                                testNames);

                            builder
                                .AppendLine()
                                .AppendLine("    [TestMethod]")
                                .AppendLine($"    public void {testName}()")
                                .AppendLine("    {")
                                .AppendLine($"        AnnotationTestHelper.AssertAnnotations(")
                                .AppendLine($"            s_fixture!.Evaluator,")
                                .AppendLine($"            {SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(instanceJson), true)},")
                                .AppendLine($"            {SymbolDisplay.FormatLiteral(location, true)},")
                                .AppendLine($"            {SymbolDisplay.FormatLiteral(keyword, true)},")
                                .AppendLine($"            {SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(expected.GetRawText()), true)});")
                                .AppendLine("    }");

                            assertionIndex++;
                        }

                        testIndex++;
                    }

                    builder
                        .AppendLine()
                        .AppendLine("    public class Fixture")
                        .AppendLine("    {")
                        .AppendLine("        public CompiledEvaluator Evaluator { get; private set; }")
                        .AppendLine()
                        .AppendLine("        public async Task InitializeAsync()")
                        .AppendLine("        {")
                        .AppendLine("            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral($"annotations/{fileName}.json", true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(EmbeddedLiteral.Json(schemaJson), true)},")
                        .AppendLine($"                \"AnnotationTestSuite.{draftName}.{pascalFileName}\",")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(EmbeddedLiteral.ForwardSlash(remotesDirectory), true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(vocabulary, true)},")
                        .AppendLine("                validateFormat: false,")
                        .AppendLine("                Assembly.GetExecutingAssembly());")
                        .AppendLine("        }")
                        .AppendLine("    }")
                        .AppendLine("}");
                }

                if (hasSuites)
                {
                    string relPath = Path.Combine(draftName, $"{fileName}.cs");
                    string outputFile = Path.Combine(outputPath, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                    File.WriteAllText(outputFile, builder.ToString());
                    fileCallback(relPath);
                }
            }
        }
    }

    private static bool IsCompatible(string draftMinCompat, string suiteCompat)
    {
        int draftLevel = Array.IndexOf(CompatOrder, draftMinCompat);
        int suiteLevel = Array.IndexOf(CompatOrder, suiteCompat);

        if (draftLevel < 0 || suiteLevel < 0)
        {
            return false;
        }

        return draftLevel >= suiteLevel;
    }

    private static string SanitizeLocation(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            return "Root";
        }

        return location.Replace("/", "_").TrimStart('_');
    }

    private static string GetUniqueName(string name, HashSet<string> names)
    {
        if (names.Add(name))
        {
            return name;
        }

        int index = 1;
        while (true)
        {
            string candidate = $"{name}{index++}";
            if (names.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string ToPascalCase(string input)
    {
        // Delegate to TestJsonSchemaCodeGenerator's implementation
        return TestUtilities.TestJsonSchemaCodeGenerator.ToPascalCase(input);
    }
}
// <copyright file="EvaluatorTestCaseGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using TestUtilities;
using TestUtilities.JsonSchemaTestSuite;

#pragma warning disable CS8618

namespace Corvus.JsonSchemaTestSuite.CodeGenerator;

/// <summary>
/// Generates xUnit test classes for the JSON Schema Test Suite that use
/// the standalone evaluator rather than the generated type hierarchy.
/// </summary>
/// <remarks>
/// This mirrors <see cref="TestCaseGenerator"/> but generates tests that
/// invoke <c>CompiledEvaluator.Evaluate</c> instead of
/// <c>DynamicJsonElement.EvaluateSchema</c>.
/// </remarks>
internal static class EvaluatorTestCaseGenerator
{
    public static void GenerateTests(Action<string> fileCallback)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        StringBuilder builder = new();

        string outputPath = config["evaluatorOutputPath"]
            ?? throw new InvalidOperationException("You must provide an <evaluatorOutputPath> in app settings.");

        outputPath = Path.GetFullPath(outputPath);

        // Delete the output directory
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        foreach (TestGroup testGroup in TestCaseGenerator.ProvideTestGroups())
        {
            string namespaceGroup = TestJsonSchemaCodeGenerator.ToPascalCase(testGroup.Name);

            foreach (TestFile testFile in testGroup.Files)
            {
                HashSet<string> suiteNames = [];

                string relativePath = Path.ChangeExtension(testFile.RelativePath, ".cs");
                string outputFile = Path.Combine(outputPath, relativePath);

                string namespaceSuffix = GetNamespaceFromPath(testFile.NamespaceRelativePath);
                string namespaceValue = $"StandaloneEvaluatorTestSuite.{namespaceGroup}{(namespaceSuffix.Length > 0 ? "." : string.Empty)}{namespaceSuffix}";
                fileCallback(relativePath);

                builder.Clear();

                builder
                    .AppendLine("using System.Reflection;")
                    .AppendLine("using System.Threading.Tasks;")
                    .AppendLine("using Corvus.Text.Json;")
                    .AppendLine("using TestUtilities;")
                    .AppendLine("using Xunit;")
                    .AppendLine()
                    .AppendLine($"namespace {namespaceValue};");

                string remotesDirectory = Path.Combine(testFile.BaseDirectory, "remotes");

                foreach (TestSuite testSuite in testFile.TestSuites)
                {
                    string pascalSuiteName = GetUniqueName(TestJsonSchemaCodeGenerator.ToPascalCase(testSuite.SuiteName), suiteNames);
                    builder
                        .AppendLine()
                        .AppendLine($"[Trait(\"StandaloneEvaluatorTestSuite\", \"{namespaceGroup}\")]")
                        .AppendLine($"public class Suite{pascalSuiteName} : IClassFixture<Suite{pascalSuiteName}.Fixture>")
                        .AppendLine("{")
                        .AppendLine("    private readonly Fixture _fixture;")
                        .AppendLine($"    public Suite{pascalSuiteName}(Fixture fixture)")
                        .AppendLine("    {")
                        .AppendLine("        _fixture = fixture;")
                        .AppendLine("    }");

                    HashSet<string> testNames = [];

                    foreach (TestSpecification test in testSuite.TestSpecifications)
                    {
                        string testName = GetUniqueName(TestJsonSchemaCodeGenerator.ToPascalCase(test.TestDescription), testNames);

                        builder
                            .AppendLine()
                            .AppendLine("    [Fact]")
                            .AppendLine($"    public void Test{testName}()")
                            .AppendLine("    {")
                            .AppendLine($"        using var doc = ParsedJsonDocument<JsonElement>.Parse({SymbolDisplay.FormatLiteral(test.Instance.ToString(), true)});")
                            .AppendLine($"        Assert.{(test.Expectation ? "True" : "False")}(_fixture.Evaluator.Evaluate(doc.RootElement));")
                            .AppendLine("    }");
                    }

                    builder
                        .AppendLine()
                        .AppendLine("    public class Fixture : IAsyncLifetime")
                        .AppendLine("    {")
                        .AppendLine("        public CompiledEvaluator Evaluator { get; private set; }")
                        .AppendLine()
                        .AppendLine("        public Task DisposeAsync() => Task.CompletedTask;")
                        .AppendLine()
                        .AppendLine("        public Task InitializeAsync()")
                        .AppendLine("        {")
                        .AppendLine("            this.Evaluator = TestEvaluatorHelper.GenerateEvaluatorForVirtualFile(")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testFile.RelativePath, true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testSuite.Schema.ToString(), true)},")
                        .AppendLine($"                \"{namespaceValue}\",")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(remotesDirectory, true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testGroup.DefaultVocabulary, true)},")
                        .AppendLine($"                validateFormat: {(testSuite.ValidateFormat ? "true" : "false")},")
                        .AppendLine("                Assembly.GetExecutingAssembly());")
                        .AppendLine("            return Task.CompletedTask;")
                        .AppendLine("        }")
                        .AppendLine("    }")
                        .AppendLine("}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllText(outputFile, builder.ToString());
            }
        }
    }

    private static string GetNamespaceFromPath(string path)
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
}
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using System.Xml;
using Corvus.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using TestUtilities;
using TestUtilities.JsonSchemaTestSuite;
using Xunit.Sdk;

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
                    .AppendLine("using Corvus.Text.Json.Validator;")
                    .AppendLine("using TestUtilities;")
                    .AppendLine("using Xunit;")
                    .AppendLine()
                    .AppendLine($"namespace {namespaceValue};");

                string current = Path.GetFullPath(".");
                string remotesDirectory = Path.Combine(testFile.BaseDirectory, "remotes");

                foreach (TestSuite testSuite in testFile.TestSuites)
                {
                    string pascalSuiteName = GetUniqueName(TestJsonSchemaCodeGenerator.ToPascalCase(testSuite.SuiteName), suiteNames);
                    builder
                        .AppendLine()
                        .AppendLine($"[Trait(\"JsonSchemaTestSuite\", \"{namespaceGroup}\")]")
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
                            .AppendLine($"        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance({SymbolDisplay.FormatLiteral(test.Instance.ToString(), true)});")
                            .AppendLine($"        Assert.{(test.Expectation ? "True" : "False")}(dynamicInstance.EvaluateSchema());")
                            .AppendLine("    }");
                    }

                    builder
                        .AppendLine()
                        .AppendLine("    public class Fixture : IAsyncLifetime")
                        .AppendLine("    {")
                        .AppendLine("        public DynamicJsonType DynamicJsonType { get; private set; }")
                        .AppendLine()
                        .AppendLine("        public Task DisposeAsync() => Task.CompletedTask;")
                        .AppendLine()
                        .AppendLine("        public async Task InitializeAsync()")
                        .AppendLine("        {")
                        .AppendLine("            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testFile.RelativePath, true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testSuite.Schema.ToString(), true)},")
                        .AppendLine($"                \"{namespaceValue}\",")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(remotesDirectory, true)},")
                        .AppendLine($"                {SymbolDisplay.FormatLiteral(testGroup.DefaultVocabulary, true)},")
                        .AppendLine($"                validateFormat: {(testSuite.ValidateFormat ? "true" : "false")},")
                        .AppendLine("                optionalAsNullable: false,")
                        .AppendLine("                useImplicitOperatorString: false,")
                        .AppendLine("                addExplicitUsings: false,")
                        .AppendLine("                Assembly.GetExecutingAssembly());")
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
        foreach (TestCollection collection in jsonSchemaTestSuite.Collections)
        {
            // Compbine the base directory with the current directory
            string currentDirectory = Path.Combine(baseDirectory, collection.Directory);

            List<TestFile> testFiles = [];

            // Iterate the files
            foreach (string file in Directory.EnumerateFiles(currentDirectory, "*.json", SearchOption.AllDirectories))
            {
                List<TestSuite> testSuites = [];

#if NET
                string relativePath = Path.GetRelativePath(currentDirectory, file);
                string baseRelativePath = Path.GetRelativePath(baseDirectory, file);
#else
                int offset = currentDirectory.Length;
                if (currentDirectory[currentDirectory.Length - 1] != Path.DirectorySeparatorChar &&
                    currentDirectory[currentDirectory.Length - 1] != Path.AltDirectorySeparatorChar)
                {
                    offset += 1;
                }

                string relativePath = file.Substring(offset);

                offset = baseDirectory.Length;
                if (baseDirectory[baseDirectory.Length - 1] != Path.DirectorySeparatorChar &&
                    baseDirectory[baseDirectory.Length - 1] != Path.AltDirectorySeparatorChar)
                {
                    offset += 1;
                }

                string baseRelativePath = file.Substring(offset);
#endif

                PathOptions? pathOptions = collection.Options?.FirstOrDefault(fe => IsInPath(currentPath: relativePath, basePath: fe.Path));
                FileExclusions? fileExclusions = collection.Exclusions?.FirstOrDefault(fe => EqualsPath(fe.File, relativePath));

                if (fileExclusions is FileExclusions f && f.Suites is null)
                {
                    // We have exclusions for the file, but have not set suites at all, which means we should exclude the file.
                    continue;
                }

                var model = ParsedValue<TestFileModel>.Parse(File.ReadAllText(file));

                foreach (TestFileModel.RequiredDescriptionAndSchemaAndTests testSuite in model.Instance.EnumerateArray())
                {

                    List<TestSpecification> testList = [];
                    string suiteName = (string)testSuite.Description;

                    SuiteExclusions? suiteExclusions = null;

                    if (fileExclusions is FileExclusions fe)
                    {
                        suiteExclusions = fe.Suites?.FirstOrDefault(s => s.Name == suiteName);
                    }

                    foreach (TestFileModel.Test test in testSuite.Tests.EnumerateArray())
                    {
                        if (suiteExclusions is SuiteExclusions se)
                        {
                            if (se.Tests is List<TestExclusion> te &&
                                te.Any(t => test.Description.EqualsString(t.Name)))
                            {
                                // We could log the reason here if we wanted
                                continue;
                            }
                        }

                        testList.Add(
                            new TestSpecification(
                                (string)test.Description,
                                test.Data,
                                test.Valid));
                    }

                    if (testList.Count > 0)
                    {
                        testSuites.Add(
                            new TestSuite(
                                (string)testSuite.Description,
                                testSuite.Schema,
                                testList,
                                pathOptions?.ValidateFormat ?? false));
                    }
                }

                if (testSuites.Count > 0)
                {
                    testFiles.Add(new TestFile(jsonSchemaTestSuite.BaseDirectory, baseRelativePath, relativePath, testSuites));
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
        return Path.GetFullPath(new Uri(Path.GetFullPath(path)).LocalPath)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public class TestConfiguration
    {
        public string BaseDirectory { get; set; }
        public List<TestCollection> Collections { get; set; }
    }

    public class TestCollection
    {
        public string Name { get; set; }
        public string DefaultVocabulary { get; set; }
        public string Directory { get; set; }
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
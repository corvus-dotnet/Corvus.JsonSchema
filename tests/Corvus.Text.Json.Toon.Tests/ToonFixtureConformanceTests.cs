#if STJ && TOON
using Corvus.Toon;

namespace Corvus.Toon.Tests;
#else
using Corvus.Text.Json.Toon;

namespace Corvus.Text.Json.Toon.Tests;
#endif

[TestClass]
public sealed class ToonFixtureConformanceTests
{
    public static IEnumerable<object[]> DecodeFixtureCases => ReadFixtureCases("decode");

    public static IEnumerable<object[]> EncodeFixtureCases => ReadFixtureCases("encode");

    [TestMethod]
    [DynamicData(nameof(DecodeFixtureCases))]
    public void DecodeFixtureConforms(FixtureCase testCase)
    {
        if (testCase.ShouldError)
        {
            Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(testCase.Input, testCase.ReaderOptions));
            return;
        }

        string actual = NormalizeJson(ToonDocument.ConvertToJsonString(testCase.Input, testCase.ReaderOptions));

        Assert.AreEqual(testCase.ExpectedJson, actual);
    }

    [TestMethod]
    [DynamicData(nameof(EncodeFixtureCases))]
    public void EncodeFixtureConforms(FixtureCase testCase)
    {
        if (testCase.ShouldError)
        {
            Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToToonString(testCase.Input, testCase.WriterOptions));
            return;
        }

        string actual = ToonDocument.ConvertToToonString(testCase.Input, testCase.WriterOptions);

        Assert.AreEqual(testCase.ExpectedToon, actual);
    }

    private static IEnumerable<object[]> ReadFixtureCases(string category)
    {
        string directory = Path.Combine(FindRepositoryRoot(), "toon-format-spec", "tests", "fixtures", category);
        foreach (string file in Directory.EnumerateFiles(directory, "*.json").OrderBy(static f => f, StringComparer.Ordinal))
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            foreach (System.Text.Json.JsonElement test in document.RootElement.GetProperty("tests").EnumerateArray())
            {
                yield return new object[] { CreateFixtureCase(category, file, test) };
            }
        }
    }

    private static FixtureCase CreateFixtureCase(string category, string file, System.Text.Json.JsonElement test)
    {
        bool shouldError = test.TryGetProperty("shouldError", out System.Text.Json.JsonElement shouldErrorElement) &&
            shouldErrorElement.GetBoolean();

        ToonReaderOptions readerOptions = CreateReaderOptions(test);
        ToonWriterOptions writerOptions = CreateWriterOptions(test);

        if (category == "decode")
        {
            return new FixtureCase(
                Path.GetFileName(file),
                test.GetProperty("name").GetString()!,
                test.GetProperty("input").GetString()!,
                shouldError ? "null" : NormalizeJson(test.GetProperty("expected").GetRawText()),
                string.Empty,
                shouldError,
                readerOptions,
                writerOptions);
        }

        return new FixtureCase(
            Path.GetFileName(file),
            test.GetProperty("name").GetString()!,
            test.GetProperty("input").GetRawText(),
            string.Empty,
            test.GetProperty("expected").GetString()!,
            shouldError,
            readerOptions,
            writerOptions);
    }

    private static ToonReaderOptions CreateReaderOptions(System.Text.Json.JsonElement test)
    {
        ToonReaderOptions options = new();
        if (!test.TryGetProperty("options", out System.Text.Json.JsonElement optionsElement))
        {
            return options;
        }

        if (optionsElement.TryGetProperty("strict", out System.Text.Json.JsonElement strictElement))
        {
            options = options with { Strict = strictElement.GetBoolean() };
        }

        if (optionsElement.TryGetProperty("indent", out System.Text.Json.JsonElement indentElement))
        {
            options = options with { IndentSize = indentElement.GetInt32() };
        }

        if (optionsElement.TryGetProperty("expandPaths", out System.Text.Json.JsonElement expandPathsElement))
        {
            options = options with { ExpandPaths = ParsePathExpansion(expandPathsElement.GetString()!) };
        }

        return options;
    }

    private static ToonWriterOptions CreateWriterOptions(System.Text.Json.JsonElement test)
    {
        ToonWriterOptions options = new();
        if (!test.TryGetProperty("options", out System.Text.Json.JsonElement optionsElement))
        {
            return options;
        }

        if (optionsElement.TryGetProperty("delimiter", out System.Text.Json.JsonElement delimiterElement))
        {
            options = options with { Delimiter = ParseDelimiter(delimiterElement.GetString()!) };
        }

        if (optionsElement.TryGetProperty("indent", out System.Text.Json.JsonElement indentElement))
        {
            options = options with { IndentSize = indentElement.GetInt32() };
        }

        if (optionsElement.TryGetProperty("keyFolding", out System.Text.Json.JsonElement keyFoldingElement))
        {
            options = options with { KeyFolding = ParseKeyFolding(keyFoldingElement.GetString()!) };
        }

        if (optionsElement.TryGetProperty("flattenDepth", out System.Text.Json.JsonElement flattenDepthElement))
        {
            options = options with { FlattenDepth = flattenDepthElement.GetInt32() };
        }

        return options;
    }

    private static ToonDelimiter ParseDelimiter(string value)
    {
        return value switch
        {
            "\t" => ToonDelimiter.Tab,
            "|" => ToonDelimiter.Pipe,
            _ => ToonDelimiter.Comma,
        };
    }

    private static ToonKeyFolding ParseKeyFolding(string value)
    {
        return value == "safe" ? ToonKeyFolding.Safe : ToonKeyFolding.Off;
    }

    private static ToonPathExpansion ParsePathExpansion(string value)
    {
        return value == "safe" ? ToonPathExpansion.Safe : ToonPathExpansion.Off;
    }

    private static string NormalizeJson(string json)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);

        return System.Text.Json.JsonSerializer.Serialize(document.RootElement);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "toon-format-spec", "tests", "fixtures")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find the repository root containing toon-format-spec.");
        return string.Empty;
    }

    public sealed class FixtureCase
    {
        public FixtureCase(
            string fileName,
            string name,
            string input,
            string expectedJson,
            string expectedToon,
            bool shouldError,
            ToonReaderOptions readerOptions,
            ToonWriterOptions writerOptions)
        {
            this.FileName = fileName;
            this.Name = name;
            this.Input = input;
            this.ExpectedJson = expectedJson;
            this.ExpectedToon = expectedToon;
            this.ShouldError = shouldError;
            this.ReaderOptions = readerOptions;
            this.WriterOptions = writerOptions;
        }

        public string FileName { get; }

        public string Name { get; }

        public string Input { get; }

        public string ExpectedJson { get; }

        public string ExpectedToon { get; }

        public bool ShouldError { get; }

        public ToonReaderOptions ReaderOptions { get; }

        public ToonWriterOptions WriterOptions { get; }

        public override string ToString()
        {
            return $"{this.FileName}: {this.Name}";
        }
    }
}
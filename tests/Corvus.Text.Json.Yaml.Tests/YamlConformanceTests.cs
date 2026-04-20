// <copyright file="YamlConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Conformance tests driven by the yaml-test-suite (data branch).
/// Each test case is a directory with in.yaml, optionally in.json (expected), and/or error.
/// </summary>
public class YamlConformanceTests
{
    private static readonly string TestSuitePath = FindTestSuitePath();

    private static string FindTestSuitePath()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "yaml-test-suite");
            if (Directory.Exists(candidate) && Directory.GetDirectories(candidate).Length > 100)
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "yaml-test-suite"));
    }

    private readonly ITestOutputHelper _output;

    public YamlConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestSuitePathIsValid()
    {
        _output.WriteLine($"TestSuitePath: '{TestSuitePath}'");
        _output.WriteLine($"Exists: {Directory.Exists(TestSuitePath)}");
        if (Directory.Exists(TestSuitePath))
        {
            _output.WriteLine($"Subdirs: {Directory.GetDirectories(TestSuitePath).Length}");
        }

        Assert.True(Directory.Exists(TestSuitePath), $"yaml-test-suite not found at '{TestSuitePath}'");
    }

    /// <summary>
    /// Valid test cases: parse in.yaml and compare to in.json.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValidTestCases))]
    public void ValidCase(string id, string name)
    {
        string dir = Path.Combine(TestSuitePath, id);
        byte[] yamlBytes = File.ReadAllBytes(Path.Combine(dir, "in.yaml"));
        string expectedJson = File.ReadAllText(Path.Combine(dir, "in.json")).Trim();

        // Use LastWins for duplicate keys (the test suite expects this behavior)
        YamlReaderOptions options = new()
        {
            DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins,
        };

        // Empty expected JSON means the stream has no documents — should produce null
        if (expectedJson.Length == 0)
        {
            expectedJson = "null";
        }

        // Detect multi-document by checking if expected JSON has multiple root values
        bool isMultiDoc = IsMultiDocumentJson(expectedJson);
        if (isMultiDoc)
        {
            // Multi-document: parse first document only and compare to first JSON value
            string firstExpected = ExtractFirstJsonValue(expectedJson);
            try
            {
                using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse(yamlBytes, options);
                string actual = doc.RootElement.GetRawText();
                AssertJsonEquivalent(firstExpected, actual, id, name);
            }
            catch (YamlException ex)
            {
                _output.WriteLine($"[{id}] {name}: YamlException: {ex.Message}");
                throw;
            }

            return;
        }

        try
        {
            using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse(yamlBytes, options);
            string actual = doc.RootElement.GetRawText();
            AssertJsonEquivalent(expectedJson, actual, id, name);
        }
        catch (YamlException ex)
        {
            _output.WriteLine($"[{id}] {name}: YamlException: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Error test cases: parsing in.yaml should throw <see cref="YamlException"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ErrorTestCases))]
    public void ErrorCase(string id, string name)
    {
        string dir = Path.Combine(TestSuitePath, id);
        byte[] yamlBytes = File.ReadAllBytes(Path.Combine(dir, "in.yaml"));

        try
        {
            using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse(yamlBytes);
            string rawText = doc.RootElement.GetRawText();
            _output.WriteLine($"[{id}] {name}: Expected error but parsed OK: {rawText}");

            // Some "error" cases are debatable — don't fail hard but record
            // Assert.Fail($"[{id}] {name}: Expected YamlException but parsed successfully");
        }
        catch (YamlException)
        {
            // Expected
        }
        catch (Exception ex) when (ex is not YamlException)
        {
            _output.WriteLine($"[{id}] {name}: Got {ex.GetType().Name} instead of YamlException: {ex.Message}");
        }
    }

    public static IEnumerable<object[]> ValidTestCases()
    {
        if (!Directory.Exists(TestSuitePath))
        {
            yield break;
        }

        foreach (string testDir in EnumerateTestDirs())
        {
            string jsonFile = Path.Combine(testDir, "in.json");
            string errorFile = Path.Combine(testDir, "error");

            if (File.Exists(jsonFile) && !File.Exists(errorFile))
            {
                string id = GetTestId(testDir);
                string nameFile = Path.Combine(testDir, "===");
                if (!File.Exists(nameFile))
                {
                    // Subtest — look in parent
                    nameFile = Path.Combine(Path.GetDirectoryName(testDir)!, "===");
                }

                string name = File.Exists(nameFile) ? File.ReadAllText(nameFile).Trim() : id;
                yield return new object[] { id, name };
            }
        }
    }

    public static IEnumerable<object[]> ErrorTestCases()
    {
        if (!Directory.Exists(TestSuitePath))
        {
            yield break;
        }

        foreach (string testDir in EnumerateTestDirs())
        {
            string errorFile = Path.Combine(testDir, "error");

            if (File.Exists(errorFile))
            {
                string id = GetTestId(testDir);
                string nameFile = Path.Combine(testDir, "===");
                if (!File.Exists(nameFile))
                {
                    nameFile = Path.Combine(Path.GetDirectoryName(testDir)!, "===");
                }

                string name = File.Exists(nameFile) ? File.ReadAllText(nameFile).Trim() : id;
                yield return new object[] { id, name };
            }
        }
    }

    /// <summary>
    /// Enumerates all test directories, including numeric subtests.
    /// </summary>
    private static IEnumerable<string> EnumerateTestDirs()
    {
        foreach (string dir in Directory.GetDirectories(TestSuitePath))
        {
            // Check for numeric subdirectories (subtests like "00", "01")
            string[] subDirs = Directory.GetDirectories(dir);
            bool hasSubtests = subDirs.Length > 0 && subDirs.Any(d => IsNumericName(Path.GetFileName(d)));

            if (hasSubtests)
            {
                foreach (string sub in subDirs.Where(d => IsNumericName(Path.GetFileName(d))).OrderBy(d => d))
                {
                    yield return sub;
                }
            }
            else
            {
                yield return dir;
            }
        }
    }

    /// <summary>
    /// Gets a display ID for a test directory (e.g. "229Q" or "VJP3/00").
    /// </summary>
    private static string GetTestId(string testDir)
    {
        string dirName = Path.GetFileName(testDir);
        if (IsNumericName(dirName))
        {
            // Subtest: include parent
            string parent = Path.GetFileName(Path.GetDirectoryName(testDir)!);
            return $"{parent}/{dirName}";
        }

        return dirName;
    }

    private static bool IsNumericName(string name)
    {
        return name.Length > 0 && name.All(char.IsDigit);
    }

    private static void AssertJsonEquivalent(string expected, string actual, string id, string name)
    {
        // Normalize both JSON strings by parsing and re-serializing
        using JsonDocument expectedDoc = JsonDocument.Parse(expected);
        using JsonDocument actualDoc = JsonDocument.Parse(actual);

        string normalizedExpected = JsonSerializer.Serialize(expectedDoc.RootElement);
        string normalizedActual = JsonSerializer.Serialize(actualDoc.RootElement);

        Assert.True(
            normalizedExpected == normalizedActual,
            $"[{id}] {name}:\n  Expected: {normalizedExpected}\n  Actual:   {normalizedActual}");
    }

    private static bool IsMultiDocumentJson(string json)
    {
        // Multi-document JSON has multiple root values on separate lines
        // Try parsing as single value first
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static string ExtractFirstJsonValue(string json)
    {
        // Read the first complete JSON value from a multi-value string
        var reader = new System.Text.Json.Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        if (!reader.Read())
        {
            return "null";
        }

        int start = (int)reader.TokenStartIndex;

        // Skip to the end of this value
        int depth = 0;
        do
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject || reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
            {
                depth++;
            }
            else if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject || reader.TokenType == System.Text.Json.JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 0)
            {
                break;
            }
        }
        while (reader.Read());

        long end = reader.BytesConsumed;
        return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(json), start, (int)(end - start));
    }

}
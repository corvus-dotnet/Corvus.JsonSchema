// <copyright file="YamlConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using global::System.Text.Json;
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
            // Multi-document: use MultiAsArray mode and compare all documents
            options = options with
            {
                DocumentMode = YamlDocumentMode.MultiAsArray,
            };

            try
            {
                using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse<JsonElement>(yamlBytes, options);
                Corvus.Text.Json.JsonElement root = doc.RootElement;

                // The result should be a JSON array of documents
                var expectedValues = ExtractAllJsonValues(expectedJson);

                // Verify length matches
                int actualLength = 0;
                foreach (Corvus.Text.Json.JsonElement _ in root.EnumerateArray())
                {
                    actualLength++;
                }

                Assert.Equal(expectedValues.Count, actualLength);

                // Compare each document
                int i = 0;
                foreach (Corvus.Text.Json.JsonElement element in root.EnumerateArray())
                {
                    string actual = element.GetRawText();
                    AssertJsonEquivalent(expectedValues[i], actual, id, $"{name} (doc {i})");
                    i++;
                }
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
            using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse<JsonElement>(yamlBytes, options);
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
            using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = YamlDocument.Parse<JsonElement>(yamlBytes);
            string rawText = doc.RootElement.GetRawText();
            _output.WriteLine($"[{id}] {name}: Expected error but parsed OK: {rawText}");
            Assert.Fail($"[{id}] {name}: Expected YamlException but parsed successfully: {rawText}");
        }
        catch (YamlException)
        {
            // Expected
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

        // Use deep comparison that is order-independent for object keys
        Assert.True(
            JsonElementDeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"[{id}] {name}:\n  Expected: {JsonSerializer.Serialize(expectedDoc.RootElement)}\n  Actual:   {JsonSerializer.Serialize(actualDoc.RootElement)}");
    }

    private static bool JsonElementDeepEquals(System.Text.Json.JsonElement a, System.Text.Json.JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                int countA = 0;
                foreach (var _ in a.EnumerateObject()) countA++;
                int countB = 0;
                foreach (var _ in b.EnumerateObject()) countB++;
                if (countA != countB) return false;

                foreach (System.Text.Json.JsonProperty prop in a.EnumerateObject())
                {
                    if (!b.TryGetProperty(prop.Name, out System.Text.Json.JsonElement bVal))
                        return false;
                    if (!JsonElementDeepEquals(prop.Value, bVal))
                        return false;
                }

                return true;

            case System.Text.Json.JsonValueKind.Array:
                int lenA = a.GetArrayLength();
                int lenB = b.GetArrayLength();
                if (lenA != lenB) return false;

                var enumA = a.EnumerateArray();
                var enumB = b.EnumerateArray();
                while (enumA.MoveNext() && enumB.MoveNext())
                {
                    if (!JsonElementDeepEquals(enumA.Current, enumB.Current))
                        return false;
                }

                return true;

            case System.Text.Json.JsonValueKind.String:
                return a.GetString() == b.GetString();

            case System.Text.Json.JsonValueKind.Number:
                // Compare by value, not raw text. On .NET Framework,
                // Utf8JsonWriter formats doubles with more digits
                // (e.g. 0.27800000000000002 vs 0.278 on .NET Core).
                return a.GetDouble() == b.GetDouble();

            default:
                return true; // True, False, Null
        }
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
        catch (global::System.Text.Json.JsonException)
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

    private static List<string> ExtractAllJsonValues(string json)
    {
        var values = new List<string>();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        int offset = 0;

        while (offset < bytes.Length)
        {
            // Skip whitespace
            while (offset < bytes.Length && (bytes[offset] == (byte)' ' || bytes[offset] == (byte)'\n'
                || bytes[offset] == (byte)'\r' || bytes[offset] == (byte)'\t'))
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                break;
            }

            var reader = new System.Text.Json.Utf8JsonReader(bytes.AsSpan(offset));
            if (!reader.Read())
            {
                break;
            }

            int start = offset + (int)reader.TokenStartIndex;

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

            int consumed = offset + (int)reader.BytesConsumed;
            values.Add(Encoding.UTF8.GetString(bytes, start, consumed - start));
            offset = consumed;
        }

        return values;
    }

}
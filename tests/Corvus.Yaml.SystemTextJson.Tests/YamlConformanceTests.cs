// <copyright file="YamlConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Yaml;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Yaml.SystemTextJson.Tests;

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

        YamlReaderOptions options = new()
        {
            DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins,
        };

        if (expectedJson.Length == 0)
        {
            expectedJson = "null";
        }

        bool isMultiDoc = IsMultiDocumentJson(expectedJson);
        if (isMultiDoc)
        {
            options = options with
            {
                DocumentMode = YamlDocumentMode.MultiAsArray,
            };

            try
            {
                using JsonDocument doc = YamlDocument.Parse(yamlBytes, options);
                JsonElement root = doc.RootElement;

                var expectedValues = ExtractAllJsonValues(expectedJson);

                int actualLength = root.GetArrayLength();
                Assert.Equal(expectedValues.Count, actualLength);

                int i = 0;
                foreach (JsonElement element in root.EnumerateArray())
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
            using JsonDocument doc = YamlDocument.Parse(yamlBytes, options);
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
            using JsonDocument doc = YamlDocument.Parse(yamlBytes);
            string rawText = doc.RootElement.GetRawText();
            _output.WriteLine($"[{id}] {name}: Expected error but parsed OK: {rawText}");
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

    private static IEnumerable<string> EnumerateTestDirs()
    {
        foreach (string dir in Directory.GetDirectories(TestSuitePath))
        {
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

    private static string GetTestId(string testDir)
    {
        string dirName = Path.GetFileName(testDir);
        if (IsNumericName(dirName))
        {
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
        using JsonDocument expectedDoc = JsonDocument.Parse(expected);
        using JsonDocument actualDoc = JsonDocument.Parse(actual);

        Assert.True(
            JsonElementDeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"[{id}] {name}:\n  Expected: {JsonSerializer.Serialize(expectedDoc.RootElement)}\n  Actual:   {JsonSerializer.Serialize(actualDoc.RootElement)}");
    }

    private static bool JsonElementDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                int countA = 0;
                foreach (var _ in a.EnumerateObject()) countA++;
                int countB = 0;
                foreach (var _ in b.EnumerateObject()) countB++;
                if (countA != countB) return false;

                foreach (JsonProperty prop in a.EnumerateObject())
                {
                    if (!b.TryGetProperty(prop.Name, out JsonElement bVal))
                        return false;
                    if (!JsonElementDeepEquals(prop.Value, bVal))
                        return false;
                }

                return true;

            case JsonValueKind.Array:
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

            case JsonValueKind.String:
                return a.GetString() == b.GetString();

            case JsonValueKind.Number:
                // Compare by value, not raw text. On .NET Framework,
                // Utf8JsonWriter formats doubles with more digits
                // (e.g. 0.27800000000000002 vs 0.278 on .NET Core).
                return a.GetDouble() == b.GetDouble();

            default:
                return true;
        }
    }

    private static bool IsMultiDocumentJson(string json)
    {
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

    private static List<string> ExtractAllJsonValues(string json)
    {
        var values = new List<string>();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        int offset = 0;

        while (offset < bytes.Length)
        {
            while (offset < bytes.Length && (bytes[offset] == (byte)' ' || bytes[offset] == (byte)'\n'
                || bytes[offset] == (byte)'\r' || bytes[offset] == (byte)'\t'))
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                break;
            }

            var reader = new Utf8JsonReader(bytes.AsSpan(offset));
            if (!reader.Read())
            {
                break;
            }

            int start = offset + (int)reader.TokenStartIndex;

            int depth = 0;
            do
            {
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    depth++;
                }
                else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
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
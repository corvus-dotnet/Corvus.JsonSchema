// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for JsonDocumentBuilder complex object processing, specifically targeting the ProcessComplexObject method
/// through its public call tree: CreateBuilder -> BuildRentedMetadataDb -> AppendLocalElement -> ProcessComplexObject.
/// </summary>
public static class JsonDocumentBuilderComplexObjectTests
{
    /// <summary>
    /// Test cases for nested object structures that will trigger ProcessComplexObject.
    /// </summary>
    public static IEnumerable<object[]> NestedObjectTestCases { get; } =
        new List<object[]>
        {
            // Simple empty object
            new object[] { "{}" },
            // Object with nested empty object
            new object[] { "{\"prop\": {}}" },
            // Multiple levels of nesting
            new object[] { "{\"outer\": {\"inner\": {\"deep\": \"value\"}}}" },
            // Deep nesting (5 levels)
            new object[] { "{\"a\": {\"b\": {\"c\": {\"d\": {\"e\": \"nested\"}}}}}" },
            // Multiple properties with nested objects
            new object[] { "{\"first\": {\"nested1\": \"value1\"}, \"second\": {\"nested2\": \"value2\"}}" },
            // Mixed value types with objects
            new object[] { "{\"str\": \"value\", \"num\": 42, \"obj\": {\"nested\": true}, \"bool\": false}" }
        };

    /// <summary>
    /// Test cases for nested array structures that will trigger ProcessComplexObject.
    /// </summary>
    public static IEnumerable<object[]> NestedArrayTestCases { get; } =
        new List<object[]>
        {
            // Simple empty array
            new object[] { "[]" },
            // Array with nested empty array
            new object[] { "[{}]" },
            // Array with nested arrays
            new object[] { "[[]]" },
            // Multiple levels of array nesting
            new object[] { "[1, [2, [3, [4, 5]]]]" },
            // Arrays with objects
            new object[] { "[{\"arr\": [1, 2, 3]}, {\"nested\": [{\"prop\": \"value\"}]}]" },
            // Complex mixed structure
            new object[] { "[\"string\", 123, true, null, [], {}]" }
        };

    /// <summary>
    /// Test cases for mixed complex structures combining objects and arrays.
    /// </summary>
    public static IEnumerable<object[]> MixedComplexStructureTestCases { get; } =
        new List<object[]>
        {
            // Object with both object and array properties
            new object[] { "{\"obj\": {}, \"arr\": []}" },
            // Nested mixed structures
            new object[] { "{\"data\": [{\"items\": [1, 2, 3]}, {\"meta\": {\"count\": 3}}]}" },
            // Array containing objects with arrays
            new object[] { "[{\"obj1\": {\"arr1\": []}}, [{\"obj2\": {\"prop\": \"val\"}}]]" },
            // Complex real-world-like structure
            new object[] { "{\"users\": [{\"id\": 1, \"profile\": {\"tags\": [\"admin\", \"user\"]}}, {\"id\": 2, \"profile\": {\"tags\": []}}]}" }
        };

    [Theory]
    [MemberData(nameof(NestedObjectTestCases))]
    public static void ProcessComplexObject_NestedObjects_PreservesStructure(string json)
    {
        // Arrange & Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;

        // Verify the structure is preserved by round-tripping
        string roundTrippedJson = rootElement.ToString();

        using var originalParsed = ParsedJsonDocument<JsonElement>.Parse(json);
        using var roundTrippedParsed = ParsedJsonDocument<JsonElement>.Parse(roundTrippedJson);

        AssertJsonStructuresEqual(originalParsed.RootElement, roundTrippedParsed.RootElement);
    }

    [Theory]
    [MemberData(nameof(NestedArrayTestCases))]
    public static void ProcessComplexObject_NestedArrays_PreservesStructure(string json)
    {
        // Arrange & Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;

        // Verify the structure is preserved by round-tripping
        string roundTrippedJson = rootElement.ToString();

        using var originalParsed = ParsedJsonDocument<JsonElement>.Parse(json);
        using var roundTrippedParsed = ParsedJsonDocument<JsonElement>.Parse(roundTrippedJson);

        AssertJsonStructuresEqual(originalParsed.RootElement, roundTrippedParsed.RootElement);
    }

    [Theory]
    [MemberData(nameof(MixedComplexStructureTestCases))]
    public static void ProcessComplexObject_MixedComplexStructures_PreservesStructure(string json)
    {
        // Arrange & Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;

        // Verify the structure is preserved by round-tripping
        string roundTrippedJson = rootElement.ToString();

        using var originalParsed = ParsedJsonDocument<JsonElement>.Parse(json);
        using var roundTrippedParsed = ParsedJsonDocument<JsonElement>.Parse(roundTrippedJson);

        AssertJsonStructuresEqual(originalParsed.RootElement, roundTrippedParsed.RootElement);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(50)]
    [InlineData(100)]
    public static void ProcessComplexObject_LargeObjects_HandlesPropertyMaps(int propertyCount)
    {
        // Arrange
        string json = GenerateLargeObjectJson(propertyCount);

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;
        Assert.Equal(JsonValueKind.Object, rootElement.ValueKind);

        // Verify all properties are preserved
        int actualPropertyCount = 0;
        foreach (JsonProperty<JsonElement.Mutable> prop in rootElement.EnumerateObject())
        {
            actualPropertyCount++;
            Assert.True(prop.Name.StartsWith("prop"), $"Unexpected property name: {prop.Name}");
            Assert.True(prop.Value.GetString()?.StartsWith("value") == true, $"Unexpected property value for {prop.Name}");
        }

        Assert.Equal(propertyCount, actualPropertyCount);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public static void ProcessComplexObject_DeepNesting_ProcessesAllLevels(int nestingDepth)
    {
        // Arrange
        string json = GenerateDeepNestedJson(nestingDepth);

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable current = builderDoc.RootElement;
        int actualDepth = 0;

        // Navigate through the nested structure
        while (current.ValueKind == JsonValueKind.Object && current.TryGetProperty("level", out JsonElement.Mutable next))
        {
            actualDepth++;
            current = next;
        }

        // The final level should be a string value
        Assert.Equal(JsonValueKind.String, current.ValueKind);
        Assert.Equal("deepest", current.GetString());
        Assert.Equal(nestingDepth, actualDepth);
    }

    [Fact]
    public static void ProcessComplexObject_MultipleDocumentsInWorkspace_TracksCorrectly()
    {
        // Arrange
        using var workspace = JsonWorkspace.Create();

        string json1 = "{\"obj\": {\"nested\": [1, 2, 3]}, \"type\": \"doc1\"}";
        string json2 = "[{\"prop\": \"value\"}, {\"arr\": [], \"type\": \"doc2\"}]";

        // Act
        using var doc1 = ParsedJsonDocument<JsonElement>.Parse(json1);
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse(json2);

        using JsonDocumentBuilder<JsonElement.Mutable> builder1 = doc1.RootElement.CreateBuilder(workspace);
        using JsonDocumentBuilder<JsonElement.Mutable> builder2 = doc2.RootElement.CreateBuilder(workspace);

        // Assert
        // Verify both documents maintain their structure independently
        Assert.Equal(JsonValueKind.Object, builder1.RootElement.ValueKind);
        Assert.Equal(JsonValueKind.Array, builder2.RootElement.ValueKind);

        // Verify specific content
        Assert.True(builder1.RootElement.TryGetProperty("type", out JsonElement.Mutable type1));
        Assert.Equal("doc1", type1.GetString());

        Assert.Equal(2, builder2.RootElement.GetArrayLength());
        JsonElement.Mutable secondArrayElement = builder2.RootElement[1];
        Assert.True(secondArrayElement.TryGetProperty("type", out JsonElement.Mutable type2));
        Assert.Equal("doc2", type2.GetString());

        // Verify documents are different
        Assert.NotEqual(builder1.RootElement.ToString(), builder2.RootElement.ToString());
    }

    [Theory]
    [InlineData(10, 5)]   // 10 properties, 5 levels deep
    [InlineData(20, 10)]  // 20 properties, 10 levels deep
    [InlineData(50, 3)]   // 50 properties, 3 levels deep
    public static void ProcessComplexObject_LargeComplexStructures_PerformsEfficiently(int propertyCount, int nestingDepth)
    {
        // Arrange
        string json = GenerateComplexNestedJson(propertyCount, nestingDepth);

        var stopwatch = Stopwatch.StartNew();

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        stopwatch.Stop();

        // Assert
        Assert.Equal(JsonValueKind.Object, builderDoc.RootElement.ValueKind);

        // Ensure reasonable performance (adjust thresholds as needed)
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Processing took {stopwatch.ElapsedMilliseconds}ms, which exceeds the 5000ms threshold");

        // Verify structure integrity
        int actualPropertyCount = builderDoc.RootElement.EnumerateObject().Count();
        Assert.True(actualPropertyCount >= propertyCount, $"Expected at least {propertyCount} properties, but found {actualPropertyCount}");
    }

    [Theory]
    [InlineData(JsonValueKind.Object, "{}")]
    [InlineData(JsonValueKind.Array, "[]")]
    [InlineData(JsonValueKind.Object, "{\"nested\": {\"deep\": {\"value\": 42}}}")]
    [InlineData(JsonValueKind.Array, "[1, [2, [3, [4]]]]")]
    public static void ProcessComplexObject_ComplexTokenTypes_ProcessesCorrectly(JsonValueKind expectedKind, string json)
    {
        // Arrange & Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;
        Assert.Equal(expectedKind, rootElement.ValueKind);

        // Verify the structure can be serialized back correctly
        string roundTripped = rootElement.ToString();
        using var roundTrippedDoc = ParsedJsonDocument<JsonElement>.Parse(roundTripped);
        Assert.Equal(expectedKind, roundTrippedDoc.RootElement.ValueKind);
    }

    [Fact]
    public static void ProcessComplexObject_WithPropertyMap_CalculatesCorrectEndTokenLength()
    {
        // Arrange - Create a large object that will likely use a property map
        string json = GenerateLargeObjectJson(50);

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;
        Assert.Equal(JsonValueKind.Object, rootElement.ValueKind);

        // Verify all properties are accessible (which means the end token length was calculated correctly)
        int propertyCount = 0;
        foreach (JsonProperty<JsonElement.Mutable> property in rootElement.EnumerateObject())
        {
            propertyCount++;
            // Accessing the property value tests that the metadata structure is correct
            Assert.NotNull(property.Value.GetString());
        }

        Assert.Equal(50, propertyCount);
    }

    [Fact]
    public static void ProcessComplexObject_WithoutPropertyMap_UsesRawLength()
    {
        // Arrange - Create a small object that won't use a property map
        string json = "{\"a\": 1, \"b\": 2, \"c\": 3}";

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;
        Assert.Equal(JsonValueKind.Object, rootElement.ValueKind);

        // Verify all properties are accessible
        Assert.True(rootElement.TryGetProperty("a", out JsonElement.Mutable a));
        Assert.Equal(1, a.GetInt32());
        Assert.True(rootElement.TryGetProperty("b", out JsonElement.Mutable b));
        Assert.Equal(2, b.GetInt32());
        Assert.True(rootElement.TryGetProperty("c", out JsonElement.Mutable c));
        Assert.Equal(3, c.GetInt32());
    }

    [Fact]
    public static void ProcessComplexObject_ExternalDocumentReferences_DefersCorrectly()
    {
        // This test verifies the external document reference path in ProcessComplexObject
        // We'll create a scenario where we build from an existing document

        // Arrange
        string json = "{\"external\": {\"nested\": [1, 2, 3]}, \"local\": \"value\"}";

        // Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);

        // Create first builder - this establishes the document
        using JsonDocumentBuilder<JsonElement.Mutable> builder1 = sourceDoc.RootElement.CreateBuilder(workspace);

        // Access the external property value (which is an immutable JsonElement)
        JsonElement.Mutable externalProperty = builder1.RootElement.GetProperty("external");

        // Verify the external property is properly structured (this exercises ProcessComplexObject)
        Assert.Equal(JsonValueKind.Object, externalProperty.ValueKind);
        Assert.True(externalProperty.TryGetProperty("nested", out JsonElement.Mutable nested));
        Assert.Equal(JsonValueKind.Array, nested.ValueKind);
        Assert.Equal(3, nested.GetArrayLength());
        Assert.Equal(1, nested[0].GetInt32());
        Assert.Equal(2, nested[1].GetInt32());
        Assert.Equal(3, nested[2].GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"a\":{\"b\":{\"c\":[]}}}")]
    [InlineData("[1,[2,[3,[]]]]")]
    public static void ProcessComplexObject_EmptyComplexStructures_HandledCorrectly(string json)
    {
        // Arrange & Act
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = sourceDoc.RootElement.CreateBuilder(workspace);

        // Assert
        JsonElement.Mutable rootElement = builderDoc.RootElement;

        // Verify the document can be serialized and maintains structure
        string serialized = rootElement.ToString();
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);

        // Parse the serialized result to ensure it's valid JSON
        using var roundTripped = ParsedJsonDocument<JsonElement>.Parse(serialized);
        Assert.Equal(rootElement.ValueKind, roundTripped.RootElement.ValueKind);
    }

    /// <summary>
    /// Generates a JSON object with the specified number of properties to test property map handling.
    /// </summary>
    private static string GenerateLargeObjectJson(int propertyCount)
    {
        var properties = new List<string>();
        for (int i = 0; i < propertyCount; i++)
        {
            properties.Add($"\"prop{i}\": \"value{i}\"");
        }
        return "{" + string.Join(", ", properties) + "}";
    }

    /// <summary>
    /// Generates deeply nested JSON to test recursive ProcessComplexObject calls.
    /// </summary>
    private static string GenerateDeepNestedJson(int nestingDepth)
    {
        if (nestingDepth <= 0)
        {
            return "\"deepest\"";
        }

        string inner = GenerateDeepNestedJson(nestingDepth - 1);
        return $"{{\"level\": {inner}}}";
    }

    /// <summary>
    /// Generates complex nested JSON with both wide (many properties) and deep (nested objects) structures.
    /// </summary>
    private static string GenerateComplexNestedJson(int propertyCount, int nestingDepth)
    {
        var sb = new StringBuilder();
        sb.Append("{");

        // Add regular properties
        for (int i = 0; i < propertyCount; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"\"prop{i}\": \"value{i}\"");
        }

        // Add nested structure
        if (nestingDepth > 0)
        {
            if (propertyCount > 0) sb.Append(", ");
            sb.Append("\"nested\": ");
            sb.Append(GenerateDeepNestedJson(nestingDepth));
        }

        // Add array with objects
        if (propertyCount > 0) sb.Append(", ");
        sb.Append("\"arrayWithObjects\": [");
        for (int i = 0; i < Math.Min(3, propertyCount); i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{{\"item{i}\": {i}, \"data\": [1, 2, 3]}}");
        }
        sb.Append("]");

        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// Helper method to recursively compare JSON element structures.
    /// </summary>
    private static void AssertJsonStructuresEqual(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProps = expected.EnumerateObject().ToList();
                var actualProps = actual.EnumerateObject().ToList();
                Assert.Equal(expectedProps.Count, actualProps.Count);

                foreach (JsonProperty<JsonElement> expectedProp in expectedProps)
                {
                    JsonProperty<JsonElement> actualProp = actualProps.FirstOrDefault(p => p.Name == expectedProp.Name);
                    Assert.True(actualProp.Value.ValueKind != JsonValueKind.Undefined, $"Property '{expectedProp.Name}' not found in actual JSON");
                    AssertJsonStructuresEqual(expectedProp.Value, actualProp.Value);
                }
                break;

            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                for (int i = 0; i < expected.GetArrayLength(); i++)
                {
                    AssertJsonStructuresEqual(expected[i], actual[i]);
                }
                break;

            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;

            case JsonValueKind.Number:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;

            case JsonValueKind.Null:
                // Both are null, nothing to compare
                break;

            default:
                throw new ArgumentOutOfRangeException($"Unexpected JsonValueKind: {expected.ValueKind}");
        }
    }
}
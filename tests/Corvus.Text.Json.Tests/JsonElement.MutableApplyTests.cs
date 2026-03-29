//-----------------------------------------------------------------------
// <copyright file="JsonElement.MutableApplyTests.cs" company="Endjin">
// Copyright (c) Endjin. All rights reserved.
// </copyright>
//-----------------------------------------------------------

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonElement.Mutable.Apply{T}"/>.
/// </summary>
public class JsonElementMutableApplyTests
{
    [Fact]
    public void Apply_EmptySourceToEmptyTarget_TargetRemainsEmpty()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        Assert.Equal("{}", targetDoc.RootElement.ToString());
    }

    [Fact]
    public void Apply_EmptyMutableSourceToEmptyTarget_TargetRemainsEmpty()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);
        using JsonDocumentBuilder<JsonElement.Mutable> sourceDoc = sourceParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply mutable source to mutable target
        targetDoc.RootElement.Apply(sourceDoc.RootElement);

        // Assert
        Assert.Equal("{}", targetDoc.RootElement.ToString());
    }

    [Fact]
    public void Apply_NewPropertiesToEmptyTarget_AddsAllProperties()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"name": "John", "age": 30}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;
        Assert.True(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.Equal("John", nameProperty.GetString());
        Assert.True(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.Equal(30, ageProperty.GetInt32());
    }

    [Fact]
    public void Apply_NewMutablePropertiesToEmptyTarget_AddsAllProperties()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"name": "John", "age": 30}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);
        using JsonDocumentBuilder<JsonElement.Mutable> sourceDoc = sourceParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply mutable source to mutable target
        targetDoc.RootElement.Apply(sourceDoc.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;
        Assert.True(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.Equal("John", nameProperty.GetString());
        Assert.True(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.Equal(30, ageProperty.GetInt32());
    }

    [Fact]
    public void Apply_NewPropertiesToExistingTarget_AddsNewProperties()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("""{"existing": "value"}""");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"name": "John", "age": 30}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;
        Assert.True(result.TryGetProperty("existing", out JsonElement.Mutable existingProperty));
        Assert.Equal("value", existingProperty.GetString());
        Assert.True(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.Equal("John", nameProperty.GetString());
        Assert.True(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.Equal(30, ageProperty.GetInt32());
    }

    [Fact]
    public void Apply_ReplaceExistingProperties_ReplacesValues()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("""{"name": "Jane", "age": 25}""");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"name": "John", "city": "New York"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;
        Assert.True(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.Equal("John", nameProperty.GetString()); // Replaced
        Assert.True(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.Equal(25, ageProperty.GetInt32()); // Preserved
        Assert.True(result.TryGetProperty("city", out JsonElement.Mutable cityProperty));
        Assert.Equal("New York", cityProperty.GetString()); // Added
    }

    [Fact]
    public void Apply_VariousPropertyTypes_HandlesAllTypes()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""
        {
            "stringProp": "text",
            "numberProp": 42,
            "boolProp": true,
            "arrayProp": [1, 2, 3],
            "objectProp": {"nested": "value"},
            "nullProp": null
        }
        """);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;

        Assert.True(result.TryGetProperty("stringProp", out JsonElement.Mutable stringProp));
        Assert.Equal("text", stringProp.GetString());

        Assert.True(result.TryGetProperty("numberProp", out JsonElement.Mutable numberProp));
        Assert.Equal(42, numberProp.GetInt32());

        Assert.True(result.TryGetProperty("boolProp", out JsonElement.Mutable boolProp));
        Assert.True(boolProp.GetBoolean());

        Assert.True(result.TryGetProperty("arrayProp", out JsonElement.Mutable arrayProp));
        Assert.Equal(JsonValueKind.Array, arrayProp.ValueKind);
        Assert.Equal(3, arrayProp.GetArrayLength());

        Assert.True(result.TryGetProperty("objectProp", out JsonElement.Mutable objectProp));
        Assert.Equal(JsonValueKind.Object, objectProp.ValueKind);
        Assert.True(objectProp.TryGetProperty("nested", out JsonElement.Mutable nestedProp));
        Assert.Equal("value", nestedProp.GetString());

        Assert.True(result.TryGetProperty("nullProp", out JsonElement.Mutable nullProp));
        Assert.Equal(JsonValueKind.Null, nullProp.ValueKind);
    }

    [Fact]
    public void Apply_SourceRemainsUnchanged_SourceIsNotModified()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("""{"target": "value"}""");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"source": "value"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);
        string originalSourceString = sourceParsed.RootElement.ToString();

        // Act - Apply immutable source to mutable target
        targetDoc.RootElement.Apply(sourceParsed.RootElement);

        // Assert
        Assert.Equal(originalSourceString, sourceParsed.RootElement.ToString());
    }

    [Fact]
    public void Apply_MutableSourceRemainsUnchanged_SourceIsNotModified()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("""{"target": "value"}""");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"source": "value"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);
        using JsonDocumentBuilder<JsonElement.Mutable> sourceDoc = sourceParsed.RootElement.CreateBuilder(workspace);
        string originalSourceString = sourceDoc.RootElement.ToString();

        // Act - Apply mutable source to mutable target
        targetDoc.RootElement.Apply(sourceDoc.RootElement);

        // Assert
        Assert.Equal(originalSourceString, sourceDoc.RootElement.ToString());
    }

    [Fact]
    public void Apply_MultipleSuccessiveApplications_AccumulatesChanges()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("""{"initial": "value"}""");
        using var source1Parsed = ParsedJsonDocument<JsonElement>.Parse("""{"first": "application"}""");
        using var source2Parsed = ParsedJsonDocument<JsonElement>.Parse("""{"second": "application", "first": "overwritten"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act - Apply immutable sources to mutable target
        targetDoc.RootElement.Apply(source1Parsed.RootElement);
        targetDoc.RootElement.Apply(source2Parsed.RootElement);

        // Assert
        JsonElement.Mutable result = targetDoc.RootElement;
        Assert.True(result.TryGetProperty("initial", out JsonElement.Mutable initialProp));
        Assert.Equal("value", initialProp.GetString());
        Assert.True(result.TryGetProperty("first", out JsonElement.Mutable firstProp));
        Assert.Equal("overwritten", firstProp.GetString()); // Overwritten by second apply
        Assert.True(result.TryGetProperty("second", out JsonElement.Mutable secondProp));
        Assert.Equal("application", secondProp.GetString());
    }

    [Fact]
    public void Apply_ToNonObjectElement_ThrowsInvalidOperationException()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"property": "value"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act & Assert - Apply immutable source to non-object mutable target
        Assert.Throws<InvalidOperationException>(() => targetDoc.RootElement.Apply(sourceParsed.RootElement));
    }

    [Fact]
    public void Apply_FromNonObjectElement_ValidatesSourceIsObject()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act & Assert - Apply non-object source should fail
        Assert.ThrowsAny<InvalidOperationException>(() => targetDoc.RootElement.Apply(sourceParsed.RootElement));
    }
}
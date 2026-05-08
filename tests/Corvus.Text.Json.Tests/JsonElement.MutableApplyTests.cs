//-----------------------------------------------------------------------
// <copyright file="JsonElement.MutableApplyTests.cs" company="Endjin">
// Copyright (c) Endjin. All rights reserved.
// </copyright>
//-----------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonElement.Mutable.Apply{T}"/>.
/// </summary>
[TestClass]
public class JsonElementMutableApplyTests
{
    [TestMethod]
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
        Assert.AreEqual("{}", targetDoc.RootElement.ToString());
    }

    [TestMethod]
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
        Assert.AreEqual("{}", targetDoc.RootElement.ToString());
    }

    [TestMethod]
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
        Assert.IsTrue(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.AreEqual("John", nameProperty.GetString());
        Assert.IsTrue(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.AreEqual(30, ageProperty.GetInt32());
    }

    [TestMethod]
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
        Assert.IsTrue(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.AreEqual("John", nameProperty.GetString());
        Assert.IsTrue(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.AreEqual(30, ageProperty.GetInt32());
    }

    [TestMethod]
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
        Assert.IsTrue(result.TryGetProperty("existing", out JsonElement.Mutable existingProperty));
        Assert.AreEqual("value", existingProperty.GetString());
        Assert.IsTrue(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.AreEqual("John", nameProperty.GetString());
        Assert.IsTrue(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.AreEqual(30, ageProperty.GetInt32());
    }

    [TestMethod]
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
        Assert.IsTrue(result.TryGetProperty("name", out JsonElement.Mutable nameProperty));
        Assert.AreEqual("John", nameProperty.GetString()); // Replaced
        Assert.IsTrue(result.TryGetProperty("age", out JsonElement.Mutable ageProperty));
        Assert.AreEqual(25, ageProperty.GetInt32()); // Preserved
        Assert.IsTrue(result.TryGetProperty("city", out JsonElement.Mutable cityProperty));
        Assert.AreEqual("New York", cityProperty.GetString()); // Added
    }

    [TestMethod]
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

        Assert.IsTrue(result.TryGetProperty("stringProp", out JsonElement.Mutable stringProp));
        Assert.AreEqual("text", stringProp.GetString());

        Assert.IsTrue(result.TryGetProperty("numberProp", out JsonElement.Mutable numberProp));
        Assert.AreEqual(42, numberProp.GetInt32());

        Assert.IsTrue(result.TryGetProperty("boolProp", out JsonElement.Mutable boolProp));
        Assert.IsTrue(boolProp.GetBoolean());

        Assert.IsTrue(result.TryGetProperty("arrayProp", out JsonElement.Mutable arrayProp));
        Assert.AreEqual(JsonValueKind.Array, arrayProp.ValueKind);
        Assert.AreEqual(3, arrayProp.GetArrayLength());

        Assert.IsTrue(result.TryGetProperty("objectProp", out JsonElement.Mutable objectProp));
        Assert.AreEqual(JsonValueKind.Object, objectProp.ValueKind);
        Assert.IsTrue(objectProp.TryGetProperty("nested", out JsonElement.Mutable nestedProp));
        Assert.AreEqual("value", nestedProp.GetString());

        Assert.IsTrue(result.TryGetProperty("nullProp", out JsonElement.Mutable nullProp));
        Assert.AreEqual(JsonValueKind.Null, nullProp.ValueKind);
    }

    [TestMethod]
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
        Assert.AreEqual(originalSourceString, sourceParsed.RootElement.ToString());
    }

    [TestMethod]
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
        Assert.AreEqual(originalSourceString, sourceDoc.RootElement.ToString());
    }

    [TestMethod]
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
        Assert.IsTrue(result.TryGetProperty("initial", out JsonElement.Mutable initialProp));
        Assert.AreEqual("value", initialProp.GetString());
        Assert.IsTrue(result.TryGetProperty("first", out JsonElement.Mutable firstProp));
        Assert.AreEqual("overwritten", firstProp.GetString()); // Overwritten by second apply
        Assert.IsTrue(result.TryGetProperty("second", out JsonElement.Mutable secondProp));
        Assert.AreEqual("application", secondProp.GetString());
    }

    [TestMethod]
    public void Apply_ToNonObjectElement_ThrowsInvalidOperationException()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("""{"property": "value"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act & Assert - Apply immutable source to non-object mutable target
        Assert.ThrowsExactly<InvalidOperationException>(() => targetDoc.RootElement.Apply(sourceParsed.RootElement));
    }

    [TestMethod]
    public void Apply_FromNonObjectElement_ValidatesSourceIsObject()
    {
        // Arrange
        using var targetParsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var sourceParsed = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> targetDoc = targetParsed.RootElement.CreateBuilder(workspace);

        // Act & Assert - Apply non-object source should fail
        Assert.Throws<InvalidOperationException>(() => targetDoc.RootElement.Apply(sourceParsed.RootElement));
    }
}

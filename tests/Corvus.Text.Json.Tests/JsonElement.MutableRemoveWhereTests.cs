// <copyright file="JsonElement.MutableRemoveWhereTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Tests;

using System;
using Corvus.Text.Json;
using Xunit;

/// <summary>
/// Tests for <see cref="JsonElement.Mutable.RemoveWhere{T}(JsonPredicate{T})"/>.
/// </summary>
public static class JsonElementMutableRemoveWhereTests
{
    #region Basic Functionality Tests

    [Fact]
    public static void RemoveWhere_EmptyArray_NoChanges()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => true);

        // Assert
        Assert.Equal(0, mutableArray.GetArrayLength());
    }

    [Fact]
    public static void RemoveWhere_RemoveAllElements_EmptyArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => true);

        // Assert
        Assert.Equal(0, mutableArray.GetArrayLength());
    }

    [Fact]
    public static void RemoveWhere_RemoveNoElements_NoChanges()
    {
        // Arrange
        const string json = "[1, 2, 3, 4, 5]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;
        string originalJson = mutableArray.GetRawText();

        // Act
        mutableArray.RemoveWhere((in element) => false);

        // Assert
        Assert.Equal(5, mutableArray.GetArrayLength());
        Assert.Equal(originalJson, mutableArray.GetRawText());
    }

    [Fact]
    public static void RemoveWhere_RemoveEvenNumbers_Success()
    {
        // Arrange
        const string json = "[1, 2, 3, 4, 5, 6]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(3, mutableArray[1].GetInt32());
        Assert.Equal(5, mutableArray[2].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_RemoveFirstElement_Success()
    {
        // Arrange
        const string json = "[\"first\", \"second\", \"third\"]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && element.GetString() == "first");

        // Assert
        Assert.Equal(2, mutableArray.GetArrayLength());
        Assert.Equal("second", mutableArray[0].GetString());
        Assert.Equal("third", mutableArray[1].GetString());
    }

    [Fact]
    public static void RemoveWhere_RemoveLastElement_Success()
    {
        // Arrange
        const string json = "[\"first\", \"second\", \"third\"]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && element.GetString() == "third");

        // Assert
        Assert.Equal(2, mutableArray.GetArrayLength());
        Assert.Equal("first", mutableArray[0].GetString());
        Assert.Equal("second", mutableArray[1].GetString());
    }

    [Fact]
    public static void RemoveWhere_RemoveMiddleElement_Success()
    {
        // Arrange
        const string json = "[\"first\", \"second\", \"third\"]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && element.GetString() == "second");

        // Assert
        Assert.Equal(2, mutableArray.GetArrayLength());
        Assert.Equal("first", mutableArray[0].GetString());
        Assert.Equal("third", mutableArray[1].GetString());
    }

    #endregion

    #region Consecutive Elements Tests

    [Fact]
    public static void RemoveWhere_ConsecutiveElementsAtStart_Success()
    {
        // Arrange
        const string json = "[1, 2, 3, 10, 11, 12]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove elements less than 10
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() < 10);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(10, mutableArray[0].GetInt32());
        Assert.Equal(11, mutableArray[1].GetInt32());
        Assert.Equal(12, mutableArray[2].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_ConsecutiveElementsAtEnd_Success()
    {
        // Arrange
        const string json = "[1, 2, 3, 10, 11, 12]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove elements greater than 5
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() > 5);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(2, mutableArray[1].GetInt32());
        Assert.Equal(3, mutableArray[2].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_ConsecutiveElementsInMiddle_Success()
    {
        // Arrange
        const string json = "[1, 10, 11, 12, 2]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove elements greater than 5
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() > 5);

        // Assert
        Assert.Equal(2, mutableArray.GetArrayLength());
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(2, mutableArray[1].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_MultipleConsecutiveRanges_Success()
    {
        // Arrange
        const string json = "[1, 2, 5, 6, 9, 10]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove even numbers
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(5, mutableArray[1].GetInt32());
        Assert.Equal(9, mutableArray[2].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_AlternatingPattern_Success()
    {
        // Arrange
        const string json = "[1, 2, 3, 4, 5, 6, 7, 8]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove even numbers (alternating pattern)
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Assert
        Assert.Equal(4, mutableArray.GetArrayLength());
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(3, mutableArray[1].GetInt32());
        Assert.Equal(5, mutableArray[2].GetInt32());
        Assert.Equal(7, mutableArray[3].GetInt32());
    }

    #endregion

    #region Type-Based Tests

    [Fact]
    public static void RemoveWhere_RemoveAllStrings_Success()
    {
        // Arrange
        const string json = "[\"hello\", 42, \"world\", true, \"test\", null]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => element.ValueKind == JsonValueKind.String);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(42, mutableArray[0].GetInt32());
        Assert.True(mutableArray[1].GetBoolean());
        Assert.Equal(JsonValueKind.Null, mutableArray[2].ValueKind);
    }

    [Fact]
    public static void RemoveWhere_RemoveAllNumbers_Success()
    {
        // Arrange
        const string json = "[\"hello\", 42, \"world\", 3.14, \"test\", 0]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => element.ValueKind == JsonValueKind.Number);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal("hello", mutableArray[0].GetString());
        Assert.Equal("world", mutableArray[1].GetString());
        Assert.Equal("test", mutableArray[2].GetString());
    }

    [Fact]
    public static void RemoveWhere_RemoveNullValues_Success()
    {
        // Arrange
        const string json = "[\"hello\", null, \"world\", null, \"test\"]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => element.ValueKind == JsonValueKind.Null);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal("hello", mutableArray[0].GetString());
        Assert.Equal("world", mutableArray[1].GetString());
        Assert.Equal("test", mutableArray[2].GetString());
    }

    #endregion

    #region Complex Object Tests

    [Fact]
    public static void RemoveWhere_ComplexObjects_RemoveByProperty()
    {
        // Arrange
        const string json = """
            [
                {"name": "Alice", "age": 30},
                {"name": "Bob", "age": 25},
                {"name": "Charlie", "age": 35}
            ]
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove objects where age > 30
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("age", out JsonElement age) &&
            age.GetInt32() > 30);

        // Assert
        Assert.Equal(2, mutableArray.GetArrayLength());
        Assert.Equal("Alice", mutableArray[0].GetProperty("name").GetString());
        Assert.Equal("Bob", mutableArray[1].GetProperty("name").GetString());
    }

    [Fact]
    public static void RemoveWhere_NestedArrays_RemoveEmptyArrays()
    {
        // Arrange
        const string json = """
            [
                [1, 2, 3],
                [],
                [4, 5],
                [],
                [6]
            ]
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove empty arrays
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0);

        // Assert
        Assert.Equal(3, mutableArray.GetArrayLength());
        Assert.Equal(3, mutableArray[0].GetArrayLength());
        Assert.Equal(2, mutableArray[1].GetArrayLength());
        Assert.Equal(1, mutableArray[2].GetArrayLength());
    }

    #endregion

    #region Nested Array Tests

    [Fact]
    public static void RemoveWhere_NestedArrayInObject_RemovesCorrectElementsAndPreservesObject()
    {
        // Arrange
        const string json = """
            {
                "items": [1, 2, 3, 4, 5, 6],
                "name": "test",
                "active": true
            }
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable itemsArray = root.GetProperty("items");

        // Act - Remove even numbers
        itemsArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Assert
        Assert.Equal(3, itemsArray.GetArrayLength());
        Assert.Equal(1, itemsArray[0].GetInt32());
        Assert.Equal(3, itemsArray[1].GetInt32());
        Assert.Equal(5, itemsArray[2].GetInt32());

        // Verify other properties are preserved
        Assert.Equal("test", builderDoc.RootElement.GetProperty("name").GetString());
        Assert.True(builderDoc.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal(3, builderDoc.RootElement.GetPropertyCount());
    }

    [Fact]
    public static void RemoveWhere_NestedArrayWithComplexObjects_RemovesCorrectElements()
    {
        // Arrange
        const string json = """
            {
                "users": [
                    {"id": 1, "name": "Alice", "active": true},
                    {"id": 2, "name": "Bob", "active": false},
                    {"id": 3, "name": "Charlie", "active": true},
                    {"id": 4, "name": "David", "active": false}
                ],
                "total": 4,
                "department": "Engineering"
            }
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable usersArray = root.GetProperty("users");

        // Act - Remove inactive users
        usersArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("active", out JsonElement active) &&
            !active.GetBoolean());

        // Assert
        Assert.Equal(2, usersArray.GetArrayLength());
        Assert.Equal("Alice", usersArray[0].GetProperty("name").GetString());
        Assert.Equal(1, usersArray[0].GetProperty("id").GetInt32());
        Assert.True(usersArray[0].GetProperty("active").GetBoolean());
        Assert.Equal("Charlie", usersArray[1].GetProperty("name").GetString());
        Assert.Equal(3, usersArray[1].GetProperty("id").GetInt32());
        Assert.True(usersArray[1].GetProperty("active").GetBoolean());

        // Verify other properties are preserved
        Assert.Equal(4, builderDoc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Engineering", builderDoc.RootElement.GetProperty("department").GetString());
        Assert.Equal(3, builderDoc.RootElement.GetPropertyCount());
    }

    [Fact]
    public static void RemoveWhere_MultipleNestedArraysInObject_RemovesCorrectElementsFromEach()
    {
        // Arrange
        const string json = """
            {
                "id": 123,
                "tags": ["important", "spam", "urgent", "spam", "normal"],
                "categories": [
                    {"name": "cat1", "items": [1, 2, 3, 4, 5]},
                    {"name": "cat2", "items": [10, 20, 30]}
                ],
                "metadata": {
                    "scores": [100, 50, 200, 75, 300],
                    "flags": [true, false, true, false, true]
                }
            }
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act - Multiple RemoveWhere operations on different nested arrays
        // Remove "spam" tags
        builderDoc.RootElement.GetProperty("tags").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && element.GetString() == "spam");

        // Remove even numbers from cat1 items
        builderDoc.RootElement.GetProperty("categories")[0].GetProperty("items").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Remove scores below 100
        builderDoc.RootElement.GetProperty("metadata").GetProperty("scores").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() < 100);

        // Remove false flags
        builderDoc.RootElement.GetProperty("metadata").GetProperty("flags").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.False);

        // Assert
        // Verify tags array
        JsonElement.Mutable tagsArray = builderDoc.RootElement.GetProperty("tags");
        Assert.Equal(3, tagsArray.GetArrayLength());
        Assert.Equal("important", tagsArray[0].GetString());
        Assert.Equal("urgent", tagsArray[1].GetString());
        Assert.Equal("normal", tagsArray[2].GetString());

        // Verify cat1 items (only odd numbers remain)
        JsonElement.Mutable cat1Items = builderDoc.RootElement.GetProperty("categories")[0].GetProperty("items");
        Assert.Equal(3, cat1Items.GetArrayLength());
        Assert.Equal(1, cat1Items[0].GetInt32());
        Assert.Equal(3, cat1Items[1].GetInt32());
        Assert.Equal(5, cat1Items[2].GetInt32());

        // Verify cat2 items unchanged
        JsonElement.Mutable cat2Items = builderDoc.RootElement.GetProperty("categories")[1].GetProperty("items");
        Assert.Equal(3, cat2Items.GetArrayLength());
        Assert.Equal(10, cat2Items[0].GetInt32());
        Assert.Equal(20, cat2Items[1].GetInt32());
        Assert.Equal(30, cat2Items[2].GetInt32());

        // Verify scores (only >= 100 remain)
        JsonElement.Mutable scoresArray = builderDoc.RootElement.GetProperty("metadata").GetProperty("scores");
        Assert.Equal(3, scoresArray.GetArrayLength());
        Assert.Equal(100, scoresArray[0].GetInt32());
        Assert.Equal(200, scoresArray[1].GetInt32());
        Assert.Equal(300, scoresArray[2].GetInt32());

        // Verify flags (only true values remain)
        JsonElement.Mutable flagsArray = builderDoc.RootElement.GetProperty("metadata").GetProperty("flags");
        Assert.Equal(3, flagsArray.GetArrayLength());
        Assert.True(flagsArray[0].GetBoolean());
        Assert.True(flagsArray[1].GetBoolean());
        Assert.True(flagsArray[2].GetBoolean());

        // Verify other properties unchanged
        Assert.Equal(123, builderDoc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("cat1", builderDoc.RootElement.GetProperty("categories")[0].GetProperty("name").GetString());
        Assert.Equal("cat2", builderDoc.RootElement.GetProperty("categories")[1].GetProperty("name").GetString());
    }

    [Fact]
    public static void RemoveWhere_NestedArrayInArray_RemovesCorrectElementsFromInnerArray()
    {
        // Arrange
        const string json = """
            [
                [1, 2, 3, 4, 5, 6],
                ["keep", "remove", "keep", "remove"],
                [true, false, true, false, true]
            ]
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act - Remove even numbers from first array
        builderDoc.RootElement[0].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Act - Remove "remove" strings from second array
        builderDoc.RootElement[1].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && element.GetString() == "remove");

        // Act - Remove false values from third array
        builderDoc.RootElement[2].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.False);

        // Assert
        // Verify first inner array (odd numbers remain)
        Assert.Equal(3, builderDoc.RootElement[0].GetArrayLength());
        Assert.Equal(1, builderDoc.RootElement[0][0].GetInt32());
        Assert.Equal(3, builderDoc.RootElement[0][1].GetInt32());
        Assert.Equal(5, builderDoc.RootElement[0][2].GetInt32());

        // Verify second inner array (only "keep" strings remain)
        Assert.Equal(2, builderDoc.RootElement[1].GetArrayLength());
        Assert.Equal("keep", builderDoc.RootElement[1][0].GetString());
        Assert.Equal("keep", builderDoc.RootElement[1][1].GetString());

        // Verify third inner array (only true values remain)
        Assert.Equal(3, builderDoc.RootElement[2].GetArrayLength());
        Assert.True(builderDoc.RootElement[2][0].GetBoolean());
        Assert.True(builderDoc.RootElement[2][1].GetBoolean());
        Assert.True(builderDoc.RootElement[2][2].GetBoolean());

        // Verify outer array still has 3 elements
        Assert.Equal(3, builderDoc.RootElement.GetArrayLength());
    }

    [Fact]
    public static void RemoveWhere_ThreeLevelNestedArray_RemovesCorrectElementsFromDeepestLevel()
    {
        // Arrange
        const string json = """
            [
                [
                    [1, 2, 3, 4],
                    [5, 6, 7, 8]
                ],
                [
                    [9, 10, 11, 12],
                    [13, 14, 15, 16]
                ]
            ]
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act - Remove even numbers from the first deepest array
        builderDoc.RootElement[0][0].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Act - Remove numbers > 10 from the last deepest array
        builderDoc.RootElement[1][1].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() > 10);

        // Assert
        // Verify first deepest array (odd numbers remain)
        Assert.Equal(2, builderDoc.RootElement[0][0].GetArrayLength());
        Assert.Equal(1, builderDoc.RootElement[0][0][0].GetInt32());
        Assert.Equal(3, builderDoc.RootElement[0][0][1].GetInt32());

        // Verify second array in first group unchanged
        Assert.Equal(4, builderDoc.RootElement[0][1].GetArrayLength());
        Assert.Equal(5, builderDoc.RootElement[0][1][0].GetInt32());
        Assert.Equal(6, builderDoc.RootElement[0][1][1].GetInt32());
        Assert.Equal(7, builderDoc.RootElement[0][1][2].GetInt32());
        Assert.Equal(8, builderDoc.RootElement[0][1][3].GetInt32());

        // Verify first array in second group unchanged
        Assert.Equal(4, builderDoc.RootElement[1][0].GetArrayLength());
        Assert.Equal(9, builderDoc.RootElement[1][0][0].GetInt32());
        Assert.Equal(10, builderDoc.RootElement[1][0][1].GetInt32());
        Assert.Equal(11, builderDoc.RootElement[1][0][2].GetInt32());
        Assert.Equal(12, builderDoc.RootElement[1][0][3].GetInt32());

        // Verify last deepest array (only numbers <= 10 remain)
        Assert.Equal(0, builderDoc.RootElement[1][1].GetArrayLength()); // All numbers were > 10

        // Verify overall structure integrity
        Assert.Equal(2, builderDoc.RootElement.GetArrayLength());
        Assert.Equal(2, builderDoc.RootElement[0].GetArrayLength());
        Assert.Equal(2, builderDoc.RootElement[1].GetArrayLength());
    }

    [Fact]
    public static void RemoveWhere_ComplexMixedNestedStructure_RemovesCorrectElements()
    {
        // Arrange
        const string json = """
            {
                "data": {
                    "numbers": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                    "nested": {
                        "items": ["a", "b", "c", "d", "e"]
                    }
                },
                "arrays": [
                    [100, 200, 300],
                    {
                        "values": [true, false, true, false]
                    }
                ],
                "status": "active"
            }
            """;
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act - Multiple operations on deeply nested arrays
        // Remove even numbers from data.numbers
        builderDoc.RootElement.GetProperty("data").GetProperty("numbers").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Remove vowels from data.nested.items
        builderDoc.RootElement.GetProperty("data").GetProperty("nested").GetProperty("items").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.String && "aeiou".Contains(element.GetString()));

        // Remove numbers < 250 from arrays[0]
        builderDoc.RootElement.GetProperty("arrays")[0].RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() < 250);

        // Remove false values from arrays[1].values
        builderDoc.RootElement.GetProperty("arrays")[1].GetProperty("values").RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.False);

        // Assert
        // Verify data.numbers (odd numbers remain)
        JsonElement.Mutable numbersArray = builderDoc.RootElement.GetProperty("data").GetProperty("numbers");
        Assert.Equal(5, numbersArray.GetArrayLength());
        Assert.Equal(1, numbersArray[0].GetInt32());
        Assert.Equal(3, numbersArray[1].GetInt32());
        Assert.Equal(5, numbersArray[2].GetInt32());
        Assert.Equal(7, numbersArray[3].GetInt32());
        Assert.Equal(9, numbersArray[4].GetInt32());

        // Verify data.nested.items (consonants remain)
        JsonElement.Mutable itemsArray = builderDoc.RootElement.GetProperty("data").GetProperty("nested").GetProperty("items");
        Assert.Equal(3, itemsArray.GetArrayLength());
        Assert.Equal("b", itemsArray[0].GetString());
        Assert.Equal("c", itemsArray[1].GetString());
        Assert.Equal("d", itemsArray[2].GetString());

        // Verify arrays[0] (numbers >= 250 remain)
        JsonElement.Mutable firstArray = builderDoc.RootElement.GetProperty("arrays")[0];
        Assert.Equal(1, firstArray.GetArrayLength());
        Assert.Equal(300, firstArray[0].GetInt32());

        // Verify arrays[1].values (only true values remain)
        JsonElement.Mutable valuesArray = builderDoc.RootElement.GetProperty("arrays")[1].GetProperty("values");
        Assert.Equal(2, valuesArray.GetArrayLength());
        Assert.True(valuesArray[0].GetBoolean());
        Assert.True(valuesArray[1].GetBoolean());

        // Verify overall structure and unchanged properties
        Assert.Equal("active", builderDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal(2, builderDoc.RootElement.GetProperty("data").GetPropertyCount());
        Assert.Equal(2, builderDoc.RootElement.GetProperty("arrays").GetArrayLength());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public static void RemoveWhere_SingleElement_RemoveIt()
    {
        // Arrange
        const string json = "[42]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => element.ValueKind == JsonValueKind.Number);

        // Assert
        Assert.Equal(0, mutableArray.GetArrayLength());
        Assert.Equal("[]", mutableArray.GetRawText());
    }

    [Fact]
    public static void RemoveWhere_SingleElement_KeepIt()
    {
        // Arrange
        const string json = "[42]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act
        mutableArray.RemoveWhere((in element) => element.ValueKind == JsonValueKind.String);

        // Assert
        Assert.Equal(1, mutableArray.GetArrayLength());
        Assert.Equal(42, mutableArray[0].GetInt32());
    }

    [Fact]
    public static void RemoveWhere_LargeArray_PerformanceTest()
    {
        // Arrange - Create an array with 1000 elements
        var jsonBuilder = new System.Text.StringBuilder("[");
        for (int i = 0; i < 1000; i++)
        {
            if (i > 0) jsonBuilder.Append(", ");
            jsonBuilder.Append(i);
        }
        jsonBuilder.Append("]");

        using var document = ParsedJsonDocument<JsonElement>.Parse(jsonBuilder.ToString());
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act - Remove even numbers
        mutableArray.RemoveWhere((in element) =>
            element.ValueKind == JsonValueKind.Number && element.GetInt32() % 2 == 0);

        // Assert
        Assert.Equal(500, mutableArray.GetArrayLength());
        // Verify first few odd numbers remain
        Assert.Equal(1, mutableArray[0].GetInt32());
        Assert.Equal(3, mutableArray[1].GetInt32());
        Assert.Equal(5, mutableArray[2].GetInt32());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public static void RemoveWhere_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        const string json = "[1, 2, 3]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            mutableArray.RemoveWhere(null!));
    }

    [Fact]
    public static void RemoveWhere_NotAnArray_ThrowsInvalidOperationException()
    {
        // Arrange
        const string json = "\"not an array\"";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableElement = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            mutableElement.RemoveWhere((in element) => true));
    }

    [Fact]
    public static void RemoveWhere_ObjectInsteadOfArray_ThrowsInvalidOperationException()
    {
        // Arrange
        const string json = "{\"key\": \"value\"}";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableElement = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            mutableElement.RemoveWhere((in element) => true));
    }

    [Fact]
    public static void RemoveWhere_NullValue_ThrowsInvalidOperationException()
    {
        // Arrange
        const string json = "null";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableElement = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            mutableElement.RemoveWhere((in element) => true));
    }

    #endregion

    #region Predicate Exception Tests

    [Fact]
    public static void RemoveWhere_PredicateThrowsException_PropagatesException()
    {
        // Arrange
        const string json = "[1, 2, 3]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            mutableArray.RemoveWhere((in element) =>
                throw new InvalidOperationException("Test exception")));
    }

    [Fact]
    public static void RemoveWhere_PredicateThrowsOnSecondElement_PartialProcessing()
    {
        // Arrange
        const string json = "[1, 2, 3]";
        using var document = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = document.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable mutableArray = builderDoc.RootElement;
        int callCount = 0;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            mutableArray.RemoveWhere((in element) =>
            {
                callCount++;
                if (callCount == 2)
                    throw new InvalidOperationException("Test exception");
                return false;
            }));

        // The array should remain unchanged due to the exception
        Assert.Equal(3, mutableArray.GetArrayLength());
    }

    #endregion
}
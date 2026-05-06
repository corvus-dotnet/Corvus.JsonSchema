// <copyright file="CoverageBatch7Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 7: targeting JsonElementExtensions, JsonDocumentBuilderSnapshot,
/// JsonElement.Mutable.CreateBuilder, and JsonPointer resolution.
/// </summary>
public static class CoverageBatch7Tests
{
    #region JsonElementExtensions — IsNotNull with undefined/null elements (lines 29-31)

    /// <summary>
    /// Exercises <c>IsNotNull</c> when the element is undefined (ParentDocument is null).
    /// </summary>
    [Fact]
    public static void IsNotNull_UndefinedElement_ReturnsFalse()
    {
        JsonElement undefined = default;
        Assert.False(undefined.IsNotNull());
    }

    /// <summary>
    /// Exercises <c>IsNotNull</c> when the element is JSON null.
    /// </summary>
    [Fact]
    public static void IsNotNull_NullElement_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("null"u8.ToArray());
        Assert.False(doc.RootElement.IsNotNull());
    }

    /// <summary>
    /// Exercises <c>IsNotNull</c> when the element has a valid value.
    /// </summary>
    [Fact]
    public static void IsNotNull_ValidElement_ReturnsTrue()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("42"u8.ToArray());
        Assert.True(doc.RootElement.IsNotNull());
    }

    #endregion

    #region JsonElementExtensions — IsNotNullOrUndefined (lines 42-44)

    /// <summary>
    /// Exercises <c>IsNotNullOrUndefined</c> when the element is undefined.
    /// </summary>
    [Fact]
    public static void IsNotNullOrUndefined_UndefinedElement_ReturnsFalse()
    {
        JsonElement undefined = default;
        Assert.False(undefined.IsNotNullOrUndefined());
    }

    /// <summary>
    /// Exercises <c>IsNotNullOrUndefined</c> when the element is JSON null.
    /// </summary>
    [Fact]
    public static void IsNotNullOrUndefined_NullElement_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("null"u8.ToArray());
        Assert.False(doc.RootElement.IsNotNullOrUndefined());
    }

    /// <summary>
    /// Exercises <c>IsNotNullOrUndefined</c> when the element has a valid value.
    /// </summary>
    [Fact]
    public static void IsNotNullOrUndefined_ValidElement_ReturnsTrue()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());
        Assert.True(doc.RootElement.IsNotNullOrUndefined());
    }

    #endregion

    #region JsonDocumentBuilderSnapshot — double Dispose (lines 74-75)

    /// <summary>
    /// Exercises <c>JsonDocumentBuilderSnapshot.Dispose</c> when called a second time
    /// (lines 74-75: Interlocked.Exchange returns null on second call).
    /// </summary>
    [Fact]
    public static void Snapshot_DoubleDispose_DoesNotThrow()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"key":"value"}"""u8.ToArray());

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);

        JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();
        snapshot.Dispose();
        snapshot.Dispose(); // Second dispose should be a no-op (lines 74-75)
    }

    #endregion

    #region JsonElement.Mutable — CreateBuilder with undefined Source (lines 1771-1772)

    /// <summary>
    /// Exercises <c>CreateBuilder</c> with an undefined Source (lines 1771-1772).
    /// </summary>
    [Fact]
    public static void CreateBuilder_UndefinedSource_ThrowsArgument()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement.Source source = default;

        try
        {
            JsonElement.CreateBuilder(workspace, source);
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    #endregion

    #region JsonElementExtensions.JsonPointer — short pointer uses stackalloc (lines 72-75)

    /// <summary>
    /// Exercises the JSON Pointer resolution with a short char-based pointer
    /// that goes through the stackalloc path (lines 72-75 return rented array null path).
    /// </summary>
    [Fact]
    public static void JsonPointer_ShortPointer_StackAllocPath()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"a":{"b":42}}"""u8.ToArray());

        // Use a short pointer "/a/b" — fits in stackalloc
        bool result = doc.RootElement.TryResolvePointer("/a/b", out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(42, resolved.GetInt32());
    }

    /// <summary>
    /// Exercises the JSON Pointer resolution with a long char-based pointer
    /// that requires ArrayPool rental (lines 73-75: rented array is returned).
    /// </summary>
    [Fact]
    public static void JsonPointer_LongPointer_ArrayPoolPath()
    {
        // Build a nested structure with a property name long enough to exceed stackalloc threshold (256 bytes)
        string longPropName = new('x', 300);
        string json = $"{{\"{longPropName}\":42}}";

        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));

        string pointer = $"/{longPropName}";
        bool result = doc.RootElement.TryResolvePointer(pointer, out JsonElement resolved);
        Assert.True(result);
        Assert.Equal(42, resolved.GetInt32());
    }

    #endregion

    #region ParsedJsonDocument — TryGetLineAndOffset edge cases

    /// <summary>
    /// Exercises <c>TryGetLineAndOffset</c> for elements at various positions
    /// including multi-line documents.
    /// </summary>
    [Fact]
    public static void TryGetLineAndOffset_MultiLine_CorrectPositions()
    {
        string json = "{\n  \"a\": 1,\n  \"b\": 2\n}";
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(json);

        // Root is at line 1
        Assert.True(doc.RootElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(1, line);
        Assert.Equal(1, charOffset);

        // "b" value is on line 3
        JsonElement bVal = doc.RootElement.GetProperty("b"u8);
        Assert.True(bVal.TryGetLineAndOffset(out line, out charOffset));
        Assert.Equal(3, line);
    }

    #endregion

    #region StackHelper — dead code verification

    // StackHelper.TryEnsureSufficientExecutionStack has ZERO callers.
    // It is dead code (imported polyfill from dotnet/runtime, never used).
    // No test can be written to cover it since it's never called from any code path.

    #endregion
}

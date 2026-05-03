// <copyright file="JsonDocumentBuilderCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests targeting specific uncovered paths in <see cref="JsonDocumentBuilder{T}"/>.
/// </summary>
public static class JsonDocumentBuilderCoverageTests
{
    /// <summary>
    /// Verifies that <see cref="JsonProperty{TValue}.WriteTo"/> works correctly
    /// for a simple (non-escaped) property name on a builder-backed element.
    /// This exercises <c>IJsonDocument.WritePropertyName</c> → <c>WritePropertyNameUnsafe</c>
    /// (the else branch — no escape sequences).
    /// </summary>
    [Fact]
    public static void PropertyWriteTo_SimplePropertyName_WritesCorrectly()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice","age":30}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate and write each property individually via JsonProperty.WriteTo
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();

        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        // Parse the result and verify
        using ParsedJsonDocument<JsonElement> result = ParsedJsonDocument<JsonElement>.Parse(output.WrittenMemory);
        Assert.True(result.RootElement.TryGetProperty("name"u8, out JsonElement nameVal));
        Assert.Equal("Alice", nameVal.GetString());
        Assert.True(result.RootElement.TryGetProperty("age"u8, out JsonElement ageVal));
        Assert.Equal(30, ageVal.GetInt32());
    }

    /// <summary>
    /// Verifies that <see cref="JsonProperty{TValue}.WriteTo"/> works correctly
    /// for an escaped property name on a builder-backed element.
    /// This exercises <c>IJsonDocument.WritePropertyName</c> → <c>WritePropertyNameUnsafe</c>
    /// → <c>UnescapeString</c> → <c>ClearAndReturn</c> (the if branch — has escape sequences).
    /// </summary>
    [Fact]
    public static void PropertyWriteTo_EscapedPropertyName_WritesCorrectly()
    {
        // Property name with escape sequences triggers HasComplexChildren=true
        ReadOnlyMemory<byte> json = """{"a\"b":1,"hello\nworld":2}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate and write each property individually via JsonProperty.WriteTo
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();

        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        // Parse the result and verify the property names are accessible by their unescaped values
        using ParsedJsonDocument<JsonElement> result = ParsedJsonDocument<JsonElement>.Parse(output.WrittenMemory);
        Assert.True(result.RootElement.TryGetProperty("a\"b", out JsonElement val1));
        Assert.Equal(1, val1.GetInt32());
        Assert.True(result.RootElement.TryGetProperty("hello\nworld", out JsonElement val2));
        Assert.Equal(2, val2.GetInt32());
    }

    /// <summary>
    /// Verifies that the generic <c>IJsonDocument.TryGetNamedPropertyValue&lt;TElement&gt;</c>
    /// with a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> finds an existing property
    /// when the builder is accessed through the <see cref="IJsonDocument"/> interface.
    /// </summary>
    [Fact]
    public static void TryGetNamedPropertyValue_Generic_CharSpan_FindsProperty()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice","age":30}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        // Access through IJsonDocument interface to call the generic TryGetNamedPropertyValue<TElement>
        IJsonDocument builderAsDoc = builder;
        bool found = builderAsDoc.TryGetNamedPropertyValue<JsonElement>(0, "name".AsSpan(), out JsonElement value);

        Assert.True(found);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal("Alice", value.GetString());
    }

    /// <summary>
    /// Verifies that the generic <c>IJsonDocument.TryGetNamedPropertyValue&lt;TElement&gt;</c>
    /// with a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> returns false for a missing property.
    /// </summary>
    [Fact]
    public static void TryGetNamedPropertyValue_Generic_CharSpan_ReturnsFalseForMissing()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice"}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        IJsonDocument builderAsDoc = builder;
        bool found = builderAsDoc.TryGetNamedPropertyValue<JsonElement>(0, "missing".AsSpan(), out JsonElement value);

        Assert.False(found);
        Assert.Equal(default, value);
    }
}

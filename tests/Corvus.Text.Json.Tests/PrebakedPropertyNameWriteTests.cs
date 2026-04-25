// <copyright file="PrebakedPropertyNameWriteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that the prebaked property name write paths produce correct JSON
/// across different encoder and formatting configurations.
/// </summary>
/// <remarks>
/// <para>
/// Generated types emit pre-escaped, pre-quoted property name blobs (escaped with
/// <see cref="JavaScriptEncoder.Default"/>). At write time, the write loop selects
/// one of three paths based on encoder and indentation settings:
/// </para>
/// <list type="bullet">
/// <item>Default encoder + minimized → <c>WritePrebakedPropertyName</c> (fastest: single memcpy).</item>
/// <item>Default encoder + indented → <c>WriteRawPropertyName</c> (skips escaping, handles indentation).</item>
/// <item>Non-default encoder → <c>WritePropertyName</c> (re-escapes with configured encoder).</item>
/// </list>
/// </remarks>
public class PrebakedPropertyNameWriteTests
{
    /// <summary>
    /// Default encoder, minimized output → exercises <c>WritePrebakedPropertyName</c> fast path.
    /// </summary>
    [Fact]
    public void WriteTo_DefaultEncoder_Minimized_ProducesCorrectJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        var buffer = new ArrayBufferWriter<byte>(1024);
        var options = new JsonWriterOptions { Indented = false };

        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            builder.WriteTo(writer);
        }

        string json = JsonReaderHelper.TranscodeHelper(buffer.WrittenSpan);
        Assert.Contains("\"name\":\"Alice\"", json);
        Assert.Contains("\"age\":30", json);
        Assert.Contains("\"email\":\"alice@example.com\"", json);
        Assert.Contains("\"isActive\":true", json);
    }

    /// <summary>
    /// Default encoder, indented output → exercises <c>WriteRawPropertyName</c> path.
    /// </summary>
    [Fact]
    public void WriteTo_DefaultEncoder_Indented_ProducesCorrectJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        var buffer = new ArrayBufferWriter<byte>(1024);
        var options = new JsonWriterOptions { Indented = true };

        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            builder.WriteTo(writer);
        }

        string json = JsonReaderHelper.TranscodeHelper(buffer.WrittenSpan);
        Assert.Contains("\"name\": \"Alice\"", json);
        Assert.Contains("\"age\": 30", json);
        Assert.Contains("\"email\": \"alice@example.com\"", json);
        Assert.Contains("\"isActive\": true", json);
    }

    /// <summary>
    /// Non-default encoder (UnsafeRelaxedJsonEscaping), minimized output → exercises
    /// the re-escape fallback path via <c>WritePropertyName</c>.
    /// </summary>
    [Fact]
    public void WriteTo_UnsafeRelaxedEncoder_Minimized_ProducesCorrectJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        var buffer = new ArrayBufferWriter<byte>(1024);
        var options = new JsonWriterOptions
        {
            Indented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            builder.WriteTo(writer);
        }

        string json = JsonReaderHelper.TranscodeHelper(buffer.WrittenSpan);
        Assert.Contains("\"name\":\"Alice\"", json);
        Assert.Contains("\"age\":30", json);
        Assert.Contains("\"email\":\"alice@example.com\"", json);
        Assert.Contains("\"isActive\":true", json);
    }

    /// <summary>
    /// Non-default encoder, indented output → exercises the re-escape fallback path
    /// with indentation handling.
    /// </summary>
    [Fact]
    public void WriteTo_UnsafeRelaxedEncoder_Indented_ProducesCorrectJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        var buffer = new ArrayBufferWriter<byte>(1024);
        var options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            builder.WriteTo(writer);
        }

        string json = JsonReaderHelper.TranscodeHelper(buffer.WrittenSpan);
        Assert.Contains("\"name\": \"Alice\"", json);
        Assert.Contains("\"age\": 30", json);
        Assert.Contains("\"email\": \"alice@example.com\"", json);
        Assert.Contains("\"isActive\": true", json);
    }

    /// <summary>
    /// Verifies that all encoder/formatting paths produce byte-identical minimized output for
    /// pure ASCII property names (where all encoders agree on escaping).
    /// </summary>
    [Fact]
    public void WriteTo_AllEncoders_ProduceIdenticalMinimizedOutput()
    {
        using var workspace1 = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder1 =
            ObjectWithMixedProperties.CreateBuilder(workspace1, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        using var workspace2 = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder2 =
            ObjectWithMixedProperties.CreateBuilder(workspace2, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        // Default encoder (null) → fast path
        var buffer1 = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer1, new JsonWriterOptions { Indented = false }))
        {
            builder1.WriteTo(writer);
        }

        // UnsafeRelaxedJsonEscaping → re-escape fallback path
        var buffer2 = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer2, new JsonWriterOptions
        {
            Indented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            builder2.WriteTo(writer);
        }

        Assert.True(
            buffer1.WrittenSpan.SequenceEqual(buffer2.WrittenSpan),
            "Default and UnsafeRelaxed encoders should produce identical output for pure ASCII property names.");
    }

    /// <summary>
    /// Output from the fast path round-trips correctly: build → write → parse → verify.
    /// </summary>
    [Fact]
    public void WriteTo_FastPath_RoundTripsCorrectly()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, age: 30, name: "Alice", email: "alice@example.com", isActive: true);

        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            builder.WriteTo(writer);
        }

        string json = JsonReaderHelper.TranscodeHelper(buffer.WrittenSpan);

        // Parse the output back and verify all properties are present
        using var parsed = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(json);
        Assert.True(parsed.RootElement.Name.ValueEquals("Alice"));
        Assert.Equal("30", parsed.RootElement.Age.ToString());
        Assert.True(parsed.RootElement.Email.ValueEquals("alice@example.com"));
        Assert.True((bool)parsed.RootElement.IsActive);
    }
}

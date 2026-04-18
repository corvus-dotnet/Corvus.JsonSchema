// <copyright file="JsonWorkspaceWriterRentalTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

public static class JsonWorkspaceWriterRentalTests
{
    [Fact]
    public static void RentWriterAndBuffer_UsesWorkspaceOptions()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter bufferWriter);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("key"u8, "value"u8);
            writer.WriteEndObject();
            writer.Flush();

            string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());

            // Default workspace options are compact (Indented = false)
            Assert.Equal("""{"key":"value"}""", json);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    [Fact]
    public static void RentWriterAndBuffer_WithExplicitCompactOptions_ProducesCompactOutput()
    {
        // Create workspace with indented options
        JsonWriterOptions indentedOptions = new() { Indented = true };
        using JsonWorkspace workspace = JsonWorkspace.Create(options: indentedOptions);

        // Rent with explicit compact options override
        JsonWriterOptions compactOptions = new() { Indented = false };
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(compactOptions, 256, out IByteBufferWriter bufferWriter);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("key"u8, "value"u8);
            writer.WriteEndObject();
            writer.Flush();

            string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());

            // Must be compact despite workspace being configured for indented
            Assert.Equal("""{"key":"value"}""", json);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    [Fact]
    public static void RentWriterAndBuffer_WithExplicitIndentedOptions_ProducesIndentedOutput()
    {
        // Create workspace with default (compact) options
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Rent with explicit indented options override
        JsonWriterOptions indentedOptions = new() { Indented = true };
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(indentedOptions, 256, out IByteBufferWriter bufferWriter);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("key"u8, "value"u8);
            writer.WriteEndObject();
            writer.Flush();

            string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());

            // Must be indented despite workspace being configured for compact
            Assert.Contains("\n", json);
            Assert.Contains("\"key\"", json);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    [Fact]
    public static void RentWriterAndBuffer_WithOptionsOverride_DoesNotAffectSubsequentDefaultRent()
    {
        JsonWriterOptions indentedOptions = new() { Indented = true };
        using JsonWorkspace workspace = JsonWorkspace.Create(options: indentedOptions);

        // First: rent with compact override
        JsonWriterOptions compactOptions = new() { Indented = false };
        Utf8JsonWriter writer1 = workspace.RentWriterAndBuffer(compactOptions, 256, out IByteBufferWriter buffer1);
        try
        {
            writer1.WriteStartArray();
            writer1.WriteNumberValue(1);
            writer1.WriteEndArray();
            writer1.Flush();
            Assert.Equal("[1]", Encoding.UTF8.GetString(buffer1.WrittenSpan.ToArray()));
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer1, buffer1);
        }

        // Second: rent with default workspace options (should be indented)
        Utf8JsonWriter writer2 = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer2);
        try
        {
            writer2.WriteStartArray();
            writer2.WriteNumberValue(1);
            writer2.WriteEndArray();
            writer2.Flush();
            string json2 = Encoding.UTF8.GetString(buffer2.WrittenSpan.ToArray());

            // Workspace options are indented, so this should contain newlines
            Assert.Contains("\n", json2);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer2, buffer2);
        }
    }
}

// <copyright file="CosmosJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// Corvus.Text.Json helpers for talking to Azure Cosmos DB through its <em>stream</em> item and query APIs. The
/// Cosmos stream surface bypasses the SDK's own (reflection) serializer entirely: the store writes a document's
/// canonical JSON bytes (produced by a generated schema type) and reads responses by parsing the raw payload with
/// the Corvus.Text.Json reader — so no <c>System.Text.Json.JsonSerializer</c> or hand-rolled POCO is ever involved.
/// </summary>
internal static class CosmosJson
{
    private static readonly byte[] DocumentsPropertyUtf8 = Encoding.UTF8.GetBytes("Documents");
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>
    /// Writes a generated document's JSON straight into a fresh stream for a Cosmos stream write — no intermediate
    /// <c>byte[]</c>. The caller owns and disposes the stream; it is positioned at the start.
    /// </summary>
    /// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
    /// <param name="value">The document to serialize.</param>
    /// <returns>A readable stream over the document's UTF-8 JSON, positioned at the start.</returns>
    public static MemoryStream WriteToStream<T>(in T value)
        where T : IJsonElement
    {
        var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            value.WriteTo(writer);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Reads a Cosmos response content stream fully into an owned UTF-8 buffer.</summary>
    /// <param name="stream">The response content stream (may be <see langword="null"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The UTF-8 payload (empty if the stream is <see langword="null"/> or empty).</returns>
    public static async ValueTask<ReadOnlyMemory<byte>> ReadAllAsync(Stream? stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Returns the raw JSON of each element of a Cosmos query page's top-level <c>Documents</c> array. Each slice is
    /// itself a complete JSON value (object or scalar), ready to hand to a generated type's <c>FromJson</c> or to one
    /// of the projection readers below.
    /// </summary>
    /// <param name="page">The query page payload (<c>{"Documents":[ ... ],"_count": n}</c>).</param>
    /// <returns>The element payloads, in document order.</returns>
    public static List<ReadOnlyMemory<byte>> ReadDocuments(ReadOnlyMemory<byte> page)
    {
        var elements = new List<ReadOnlyMemory<byte>>();
        if (page.IsEmpty)
        {
            return elements;
        }

        var reader = new Utf8JsonReader(page.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return elements;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isDocuments = reader.ValueTextEquals(DocumentsPropertyUtf8);
            reader.Read();
            if (isDocuments)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        int end = (int)reader.BytesConsumed;
                        elements.Add(page.Slice(start, end - start));
                    }
                }

                return elements;
            }

            reader.Skip();
        }

        return elements;
    }

    /// <summary>Reads a string property from a projection element, or <see langword="null"/> if absent/null.</summary>
    /// <param name="element">The element JSON.</param>
    /// <param name="propertyUtf8">The UTF-8 property name.</param>
    /// <returns>The string value, or <see langword="null"/>.</returns>
    public static string? GetString(ReadOnlyMemory<byte> element, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(element.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(propertyUtf8);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }

            reader.Skip();
        }

        return null;
    }

    /// <summary>Reads an integer property from a projection element, or <see langword="null"/> if absent/not a number.</summary>
    /// <param name="element">The element JSON.</param>
    /// <param name="propertyUtf8">The UTF-8 property name.</param>
    /// <returns>The numeric value, or <see langword="null"/>.</returns>
    public static long? GetInt64(ReadOnlyMemory<byte> element, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(element.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(propertyUtf8);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : null;
            }

            reader.Skip();
        }

        return null;
    }

    /// <summary>Reads a bare scalar element (a <c>SELECT VALUE</c> projection) as an <see cref="long"/>.</summary>
    /// <param name="element">The element JSON.</param>
    /// <returns>The numeric value, or <see langword="null"/> if the element is null or not a number.</returns>
    public static long? AsInt64OrNull(ReadOnlyMemory<byte> element)
    {
        var reader = new Utf8JsonReader(element.Span);
        return reader.Read() && reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : null;
    }
}
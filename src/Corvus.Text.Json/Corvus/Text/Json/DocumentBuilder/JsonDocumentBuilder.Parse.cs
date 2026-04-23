// <copyright file="JsonDocumentBuilder.Parse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class JsonDocumentBuilder<T>
    where T : struct, IMutableJsonElement<T>
{
    private const int UnseekableStreamInitialRentSize = 4096;

    /// <summary>
    /// Parses UTF-8 encoded JSON directly into a mutable document builder, avoiding the
    /// intermediate <see cref="ParsedJsonDocument{T}"/> allocation and tree walk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the two-step <c>ParsedJsonDocument.Parse → workspace.CreateBuilder</c> workflow,
    /// this method performs a single pass over the input, storing the raw UTF-8 bytes directly
    /// as the builder's value backing. MetadataDb rows reference offsets into the raw bytes
    /// (the same convention as <see cref="ParsedJsonDocument{T}"/>), so no per-value copies
    /// or DynamicValue headers are needed during parsing. Subsequent mutations append
    /// DynamicValue entries after the raw region.
    /// </para>
    /// <para>
    /// The returned builder must be disposed when no longer needed.
    /// </para>
    /// <para>
    /// Because the input is considered to be text, a UTF-8 Byte-Order-Mark (BOM) must not be present.
    /// </para>
    /// </remarks>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="utf8Json">UTF-8 encoded JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonDocumentBuilder{T}"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException">
    /// <paramref name="utf8Json"/> does not represent a valid single JSON value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> contains unsupported options.
    /// </exception>
    public static JsonDocumentBuilder<T> Parse(
        JsonWorkspace workspace,
        ReadOnlyMemory<byte> utf8Json,
        JsonDocumentOptions options = default)
    {
        ReadOnlySpan<byte> span = utf8Json.Span;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(256, span.Length));
        span.CopyTo(buffer);
        return ParseCore(workspace, buffer, span.Length, options.GetReaderOptions());
    }

    /// <summary>
    /// Parses text representing a single JSON value directly into a mutable document builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is transcoded from UTF-16 to UTF-8 before parsing. For best performance,
    /// prefer the <see cref="Parse(JsonWorkspace, ReadOnlyMemory{byte}, JsonDocumentOptions)"/>
    /// overload if you already have UTF-8 data.
    /// </para>
    /// </remarks>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="json">JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonDocumentBuilder{T}"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException">
    /// <paramref name="json"/> does not represent a valid single JSON value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> contains unsupported options.
    /// </exception>
    public static JsonDocumentBuilder<T> Parse(
        JsonWorkspace workspace,
        [StringSyntax(StringSyntaxAttribute.Json)] ReadOnlyMemory<char> json,
        JsonDocumentOptions options = default)
    {
        ReadOnlySpan<char> jsonChars = json.Span;
        int expectedByteCount = JsonReaderHelper.GetUtf8ByteCount(jsonChars);
        byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(Math.Max(256, expectedByteCount));

        int actualByteCount;
        try
        {
            actualByteCount = JsonReaderHelper.GetUtf8FromText(jsonChars, utf8Bytes);
            Debug.Assert(expectedByteCount == actualByteCount);
        }
        catch
        {
            utf8Bytes.AsSpan(0, expectedByteCount).Clear();
            ArrayPool<byte>.Shared.Return(utf8Bytes);
            throw;
        }

        // Ownership of utf8Bytes transfers to ParseCore (becomes _valueBacking).
        return ParseCore(workspace, utf8Bytes, actualByteCount, options.GetReaderOptions());
    }

    /// <summary>
    /// Parses a string representing a single JSON value directly into a mutable document builder.
    /// </summary>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="json">JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonDocumentBuilder{T}"/> representation of the JSON value.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="json"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="JsonException">
    /// <paramref name="json"/> does not represent a valid single JSON value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> contains unsupported options.
    /// </exception>
    public static JsonDocumentBuilder<T> Parse(
        JsonWorkspace workspace,
        [StringSyntax(StringSyntaxAttribute.Json)] string json,
        JsonDocumentOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Parse(workspace, json.AsMemory(), options);
    }

    /// <summary>
    /// Parses a <see cref="Stream"/> of UTF-8 encoded data representing a single JSON value
    /// directly into a mutable document builder. The Stream will be read to completion.
    /// </summary>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="utf8Json">UTF-8 encoded JSON data to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonDocumentBuilder{T}"/> representation of the JSON value.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="utf8Json"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="JsonException">
    /// <paramref name="utf8Json"/> does not represent a valid single JSON value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> contains unsupported options.
    /// </exception>
    public static JsonDocumentBuilder<T> Parse(
        JsonWorkspace workspace,
        Stream utf8Json,
        JsonDocumentOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);

        ArraySegment<byte> drained = ReadStreamToEnd(utf8Json);
        Debug.Assert(drained.Array != null);

        // Ownership of drained.Array transfers to ParseCore (becomes _valueBacking).
        return ParseCore(workspace, drained.Array!, drained.Count, options.GetReaderOptions());
    }

    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided reader
    /// directly into a mutable document builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the <see cref="Utf8JsonReader.TokenType"/> property of <paramref name="reader"/>
    /// is <see cref="JsonTokenType.PropertyName"/> or <see cref="JsonTokenType.None"/>, the
    /// reader will be advanced by one call to <see cref="Utf8JsonReader.Read"/> to determine
    /// the start of the value.
    /// </para>
    /// <para>
    /// Upon completion of this method, <paramref name="reader"/> will be positioned at the
    /// final token in the JSON value. If an exception is thrown, the reader is reset to
    /// the state it was in when the method was called.
    /// </para>
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="reader">The reader to read.</param>
    /// <returns>A <see cref="JsonDocumentBuilder{T}"/> representation of the JSON value.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="reader"/> is using unsupported options.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The current <paramref name="reader"/> token does not start or represent a value.
    /// </exception>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    public static JsonDocumentBuilder<T> ParseValue(
        JsonWorkspace workspace,
        ref Utf8JsonReader reader)
    {
        // Extract the value bytes from the reader, then parse them with our core loop.
        // This follows the same approach as ParsedJsonDocument.TryParseValue.
        Utf8JsonReader restore = reader;

        ReadOnlySpan<byte> valueSpan = default;
        ReadOnlySequence<byte> valueSequence = default;

        try
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.None:
                case JsonTokenType.PropertyName:
                {
                    if (!reader.Read())
                    {
                        ThrowHelper.ThrowJsonReaderException(
                            ref reader,
                            ExceptionResource.ExpectedJsonTokens);
                    }

                    break;
                }
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                {
                    long startingOffset = reader.TokenStartIndex;

                    if (!reader.TrySkip())
                    {
                        ThrowHelper.ThrowJsonReaderException(
                            ref reader,
                            ExceptionResource.ExpectedJsonTokens);
                    }

                    long totalLength = reader.BytesConsumed - startingOffset;
                    ReadOnlySequence<byte> sequence = reader.OriginalSequence;

                    if (sequence.IsEmpty)
                    {
                        valueSpan = reader.OriginalSpan.Slice(
                            checked((int)startingOffset),
                            checked((int)totalLength));
                    }
                    else
                    {
                        valueSequence = sequence.Slice(startingOffset, totalLength);
                    }

                    break;
                }

                case JsonTokenType.False:
                case JsonTokenType.True:
                case JsonTokenType.Null:
                case JsonTokenType.Number:
                {
                    if (reader.HasValueSequence)
                    {
                        valueSequence = reader.ValueSequence;
                    }
                    else
                    {
                        valueSpan = reader.ValueSpan;
                    }

                    break;
                }

                case JsonTokenType.String:
                {
                    ReadOnlySequence<byte> sequence = reader.OriginalSequence;

                    if (sequence.IsEmpty)
                    {
                        int payloadLength = reader.ValueSpan.Length + 2;
                        valueSpan = reader.OriginalSpan.Slice((int)reader.TokenStartIndex, payloadLength);
                    }
                    else
                    {
                        long payloadLength = 2;

                        if (reader.HasValueSequence)
                        {
                            payloadLength += reader.ValueSequence.Length;
                        }
                        else
                        {
                            payloadLength += reader.ValueSpan.Length;
                        }

                        valueSequence = sequence.Slice(reader.TokenStartIndex, payloadLength);
                    }

                    break;
                }

                default:
                {
                    Debug.Assert(!reader.HasValueSequence);
                    byte displayByte = reader.ValueSpan[0];

                    ThrowHelper.ThrowJsonReaderException(
                        ref reader,
                        ExceptionResource.ExpectedStartOfValueNotFound,
                        displayByte);

                    break;
                }
            }
        }
        catch
        {
            reader = restore;
            throw;
        }

        int length = valueSpan.IsEmpty ? checked((int)valueSequence.Length) : valueSpan.Length;
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Max(256, length));

        try
        {
            if (valueSpan.IsEmpty)
            {
                valueSequence.CopyTo(rented);
            }
            else
            {
                valueSpan.CopyTo(rented);
            }
        }
        catch
        {
            rented.AsSpan(0, length).Clear();
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }

        // Ownership of rented transfers to ParseCore (becomes _valueBacking).
        return ParseCore(workspace, rented, length, reader.CurrentState.Options);
    }

    /// <summary>
    /// Creates a builder, registers it in the workspace, and runs the core parse loop
    /// to populate MetadataDb with raw-offset rows. The provided buffer becomes the
    /// builder's <c>_valueBacking</c> (ownership is transferred to the builder).
    /// </summary>
    /// <param name="workspace">The workspace that will own this builder.</param>
    /// <param name="utf8JsonBuffer">
    /// A rented buffer containing the raw UTF-8 JSON at <c>[0..rawLength)</c>.
    /// Ownership transfers to the returned builder; the caller must not return it to the pool.
    /// </param>
    /// <param name="rawLength">The number of valid JSON bytes in <paramref name="utf8JsonBuffer"/>.</param>
    /// <param name="readerOptions">Reader options for the <see cref="Utf8JsonReader"/>.</param>
    /// <returns>A populated <see cref="JsonDocumentBuilder{T}"/>.</returns>
    private static JsonDocumentBuilder<T> ParseCore(
        JsonWorkspace workspace,
        byte[] utf8JsonBuffer,
        int rawLength,
        JsonReaderOptions readerOptions)
    {
        JsonDocumentBuilder<T> result = new(workspace);
        int parentWorkspaceIndex = workspace.GetDocumentIndex(result);
        result._parentWorkspaceIndex = parentWorkspaceIndex;

        // Initialize MetadataDb with the same size heuristic as ParsedJsonDocument.
        result._parsedData = MetadataDb.CreateRented(rawLength, convertToAlloc: false);

        // The rented buffer already contains the raw JSON at [0..rawLength).
        // Use it directly as _valueBacking — no separate buffer allocation needed.
        // Subsequent mutations will append DynamicValue entries at _valueOffset (>= rawLength),
        // growing the buffer via Enlarge() if needed.
        result._valueBacking = utf8JsonBuffer;
        result._rawJsonLength = rawLength;
        result._valueOffset = rawLength;

        var stack = new StackRowStack(JsonDocumentOptions.DefaultMaxDepth * StackRow.Size);

        try
        {
            result.ParseTokens(utf8JsonBuffer.AsSpan(0, rawLength), readerOptions, ref stack);
        }
        catch
        {
            result.Dispose();
            throw;
        }
        finally
        {
            stack.Dispose();
        }

        return result;
    }

    /// <summary>
    /// The core parse loop. Reads tokens from the <see cref="Utf8JsonReader"/> and populates
    /// <see cref="JsonDocument._parsedData"/> with local-reference rows whose offsets point
    /// directly into the raw JSON bytes in <see cref="JsonDocument._valueBacking"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is structurally identical to <c>ParsedJsonDocument.Parse(ReadOnlySpan, ...)</c>.
    /// The raw JSON bytes have already been copied into <c>_valueBacking[0.._rawJsonLength)</c>,
    /// so token offsets from <see cref="Utf8JsonReader.TokenStartIndex"/> reference the correct
    /// positions directly. No per-value copying or DynamicValue headers are needed.
    /// </para>
    /// <para>
    /// Container rows (StartObject, EndObject, StartArray, EndArray) do not store values;
    /// only their structural metadata (item count, row count, complex-children flag) is recorded.
    /// </para>
    /// </remarks>
    private void ParseTokens(
        ReadOnlySpan<byte> utf8JsonSpan,
        JsonReaderOptions readerOptions,
        ref StackRowStack stack)
    {
        bool inArray = false;
        int arrayItemsOrPropertyCount = 0;
        int numberOfRowsForMembers = 0;
        int numberOfRowsForValues = 0;

        Utf8JsonReader reader = new(
            utf8JsonSpan,
            isFinalBlock: true,
            new JsonReaderState(options: readerOptions));

        while (reader.Read())
        {
            JsonTokenType tokenType = reader.TokenType;

            // Since the input payload is contained within a Span,
            // token start index can never be larger than int.MaxValue.
            Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
            int tokenStart = (int)reader.TokenStartIndex;

            if (tokenType == JsonTokenType.StartObject)
            {
                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                numberOfRowsForValues++;
                _parsedData.Append(tokenType, tokenStart, DbRow.UnknownSize);
                var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForMembers + 1);
                stack.Push(row);
                arrayItemsOrPropertyCount = 0;
                numberOfRowsForMembers = 0;
            }
            else if (tokenType == JsonTokenType.EndObject)
            {
                int rowIndex = _parsedData.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartObject);

                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                _parsedData.SetLength(rowIndex, arrayItemsOrPropertyCount);

                int newRowIndex = _parsedData.Length;
                _parsedData.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                _parsedData.SetNumberOfRows(rowIndex, numberOfRowsForMembers);
                _parsedData.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                StackRow row = stack.Pop();
                arrayItemsOrPropertyCount = row.SizeOrLength;
                numberOfRowsForMembers += row.NumberOfRows;
            }
            else if (tokenType == JsonTokenType.StartArray)
            {
                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                numberOfRowsForMembers++;
                _parsedData.Append(tokenType, tokenStart, DbRow.UnknownSize);
                var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1);
                stack.Push(row);
                arrayItemsOrPropertyCount = 0;
                numberOfRowsForValues = 0;
            }
            else if (tokenType == JsonTokenType.EndArray)
            {
                int rowIndex = _parsedData.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartArray);

                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                _parsedData.SetLength(rowIndex, arrayItemsOrPropertyCount);
                _parsedData.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                if ((uint)(arrayItemsOrPropertyCount + 1) != (uint)numberOfRowsForValues)
                {
                    _parsedData.SetHasComplexChildren(rowIndex);
                }

                int newRowIndex = _parsedData.Length;
                _parsedData.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                _parsedData.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                StackRow row = stack.Pop();
                arrayItemsOrPropertyCount = row.SizeOrLength;
                numberOfRowsForValues += row.NumberOfRows;
            }
            else if (tokenType == JsonTokenType.PropertyName)
            {
                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                arrayItemsOrPropertyCount++;

                // Adding 1 to skip the start quote will never overflow.
                Debug.Assert(tokenStart < int.MaxValue);

                _parsedData.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                if (reader.ValueIsEscaped)
                {
                    _parsedData.SetHasComplexChildren(_parsedData.Length - DbRow.Size);
                }

                Debug.Assert(!inArray);
            }
            else
            {
                Debug.Assert(tokenType >= JsonTokenType.String && tokenType <= JsonTokenType.Null);
                numberOfRowsForValues++;
                numberOfRowsForMembers++;

                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                if (tokenType == JsonTokenType.String)
                {
                    // Adding 1 to skip the start quote will never overflow.
                    Debug.Assert(tokenStart < int.MaxValue);

                    _parsedData.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                    if (reader.ValueIsEscaped)
                    {
                        _parsedData.SetHasComplexChildren(_parsedData.Length - DbRow.Size);
                    }
                }
                else
                {
                    _parsedData.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                }
            }

            inArray = reader.IsInArray;
        }

        Debug.Assert(reader.BytesConsumed == utf8JsonSpan.Length);
    }

    /// <summary>
    /// Reads a stream to completion, stripping a UTF-8 BOM if present.
    /// </summary>
    private static ArraySegment<byte> ReadStreamToEnd(Stream stream)
    {
        int written = 0;
        byte[]? rented = null;

        ReadOnlySpan<byte> utf8Bom = JsonConstants.Utf8Bom;

        try
        {
            if (stream.CanSeek)
            {
                long expectedLength = Math.Max(utf8Bom.Length, stream.Length - stream.Position) + 1;
                rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
            }
            else
            {
                rented = ArrayPool<byte>.Shared.Rent(UnseekableStreamInitialRentSize);
            }

            int lastRead;

            // Read up to 3 bytes to see if it's the UTF-8 BOM
            do
            {
                Debug.Assert(rented.Length >= utf8Bom.Length);

                lastRead = stream.Read(
                    rented,
                    written,
                    utf8Bom.Length - written);

                written += lastRead;
            }
            while (lastRead > 0 && written < utf8Bom.Length);

            // If we have 3 bytes, and they're the BOM, reset the write position to 0.
            if ((uint)written == (uint)utf8Bom.Length &&
                utf8Bom.SequenceEqual(rented.AsSpan(0, utf8Bom.Length)))
            {
                written = 0;
            }

            do
            {
                if ((uint)rented.Length == (uint)written)
                {
                    byte[] toReturn = rented;
                    rented = ArrayPool<byte>.Shared.Rent(checked(toReturn.Length * 2));
                    Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);

                    // The data in this rented buffer may contain content, clear it before returning.
                    toReturn.AsSpan(0, written).Clear();
                    ArrayPool<byte>.Shared.Return(toReturn);
                }

                lastRead = stream.Read(rented, written, rented.Length - written);
                written += lastRead;
            }
            while (lastRead > 0);

            return new ArraySegment<byte>(rented, 0, written);
        }
        catch
        {
            if (rented != null)
            {
                rented.AsSpan(0, written).Clear();
                ArrayPool<byte>.Shared.Return(rented);
            }

            throw;
        }
    }
}
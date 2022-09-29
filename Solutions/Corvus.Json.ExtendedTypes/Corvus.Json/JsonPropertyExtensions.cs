// <copyright file="JsonPropertyExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Extensions to JsonProperty to provide raw string processing.
/// </summary>
public static class JsonPropertyExtensions
{
    /// <summary>
    ///   Attempts to represent the JSON property name as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON element to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetName<TState, TResult>(this JsonProperty property, in Utf8Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        return property.TryGetName(parser, state, true, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON element to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="decode">Indicates whether the UTF8 JSON string should be decoded.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetName<TState, TResult>(this JsonProperty property, in Utf8Parser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? value)
    {
        return property.ProcessRawTextForName(new Utf8ParserStateWrapper<TState, TResult>(parser, state, decode), ProcessRawText, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON element to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetName<TState, TResult>(this JsonProperty property, in Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        return property.ProcessRawTextForName(new ParserStateWrapper<TState, TResult>(parser, state), ProcessRawText, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string property as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON property to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   This value's <see cref="JsonProperty.Value"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetNameAndStringValue<TState, TResult>(this JsonProperty property, in Utf8PropertyParser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        return property.TryGetNameAndStringValue(parser, state, true, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string property as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON element to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="decode">Indicates whether the UTF8 JSON string should be decoded.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   This value's <see cref="JsonProperty.Value"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetNameAndStringValue<TState, TResult>(this JsonProperty property, in Utf8PropertyParser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? value)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException();
        }

        return property.ProcessRawTextForNameAndString(new Utf8PropertyParserStateWrapper<TState, TResult>(parser, state, decode), ProcessRawTextForNameAndString, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="property">The JSON element to extend.</param>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   This value's <see cref="JsonProperty.Value"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetNameAndStringValue<TState, TResult>(this JsonProperty property, in PropertyParser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException();
        }

        return property.ProcessRawTextForNameAndString(new PropertyParserStateWrapper<TState, TResult>(parser, state), ProcessRawText, out value);
    }

    private static bool ProcessRawText<TState, TResult>(ReadOnlySpan<byte> rawInput, in Utf8ParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? value)
    {
        if (!state.Decode)
        {
            return state.Parser(rawInput, state.State, out value);
        }
        else
        {
            int idx = rawInput.IndexOf(JsonConstants.BackSlash);

            if (idx < 0)
            {
                return state.Parser(rawInput, state.State, out value);
            }

            byte[]? sourceArray = null;
            int length = rawInput.Length;
            Span<byte> sourceUnescaped = length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (sourceArray = ArrayPool<byte>.Shared.Rent(length));
            JsonReaderHelper.Unescape(rawInput, sourceUnescaped, 0, out int written);
            sourceUnescaped = sourceUnescaped[..written];

            try
            {
                return state.Parser(sourceUnescaped, state.State, out value);
            }
            finally
            {
                if (sourceArray != null)
                {
                    ArrayPool<byte>.Shared.Return(sourceArray, true);
                }
            }
        }
    }

    private static bool ProcessRawText<TState, TResult>(ReadOnlySpan<byte> rawInput, in ParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? result)
    {
        int idx = rawInput.IndexOf(JsonConstants.BackSlash);

        if (idx >= 0)
        {
            // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
            int length = rawInput.Length;
            byte[]? pooledName = null;

            Span<byte> utf8Unescaped =
                length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[JsonConstants.StackallocThreshold] :
                (pooledName = ArrayPool<byte>.Shared.Rent(length));

            JsonReaderHelper.Unescape(rawInput, utf8Unescaped, idx, out int written);
            utf8Unescaped = utf8Unescaped[..written];

            try
            {
                return ProcessDecodedText(utf8Unescaped, state, out result);
            }
            finally
            {
                if (pooledName != null)
                {
                    ArrayPool<byte>.Shared.Return(pooledName, true);
                }
            }
        }
        else
        {
            return ProcessDecodedText(rawInput, state, out result);
        }
    }

    private static bool ProcessDecodedText<TState, TResult>(ReadOnlySpan<byte> decodedUtf8String, in ParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? value)
    {
        char[]? sourceTranscodedArray = null;
        int length = checked(decodedUtf8String.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
        Span<char> sourceTranscoded = length <= JsonConstants.StackallocThreshold ?
        stackalloc char[JsonConstants.StackallocThreshold] :
        (sourceTranscodedArray = ArrayPool<char>.Shared.Rent(length));
        int writtenTranscoded = JsonReaderHelper.TranscodeHelper(decodedUtf8String, sourceTranscoded);
        sourceTranscoded = sourceTranscoded[..writtenTranscoded];

        bool success = false;
        if (state.Parser(sourceTranscoded, state.State, out TResult? tmp))
        {
            value = tmp;
            success = true;
        }
        else
        {
            value = default;
        }

        if (sourceTranscodedArray != null)
        {
            ArrayPool<char>.Shared.Return(sourceTranscodedArray, true);
        }

        return success;
    }

    private static bool ProcessRawTextForNameAndString<TState, TResult>(ReadOnlySpan<byte> rawName, ReadOnlySpan<byte> rawValue, in Utf8PropertyParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? value)
    {
        if (!state.Decode)
        {
            return state.Parser(rawName, rawValue, state.State, out value);
        }
        else
        {
            int idx = rawValue.IndexOf(JsonConstants.BackSlash);
            int idx2 = rawName.IndexOf(JsonConstants.BackSlash);

            if (idx >= 0 && idx2 >= 0)
            {
                return ProcessEncodedNameAndEncodedValue(rawName, rawValue, state, out value, idx, idx2);
            }
            else if (idx >= 0 && idx2 < 0)
            {
                return ProcessDecodedNameAndEncodedValue(rawName, rawValue, state, out value, idx);
            }
            else if (idx2 >= 0 && idx < 0)
            {
                return ProcessEncodedNameAndDecodedValue(rawName, rawValue, state, out value, idx2);
            }
            else
            {
                return state.Parser(rawName, rawValue, state.State, out value);
            }
        }
    }

    private static bool ProcessEncodedNameAndEncodedValue<TState, TResult>(ReadOnlySpan<byte> encodedName, ReadOnlySpan<byte> encodedValue, Utf8PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx, int idx2)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int nameLength = encodedName.Length;
        byte[]? pooledName = null;

        Span<byte> utf8UnescapedName =
            nameLength <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledName = ArrayPool<byte>.Shared.Rent(nameLength));

        JsonReaderHelper.Unescape(encodedName, utf8UnescapedName, idx, out int writtenName);
        utf8UnescapedName = utf8UnescapedName[..writtenName];

        int valueLength = encodedValue.Length;
        byte[]? pooledValue = null;

        Span<byte> utf8UnescapedValue =
            valueLength <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledValue = ArrayPool<byte>.Shared.Rent(valueLength));

        JsonReaderHelper.Unescape(encodedValue, utf8UnescapedValue, idx2, out int writtenValue);
        utf8UnescapedValue = utf8UnescapedValue[..writtenValue];

        try
        {
            return state.Parser(utf8UnescapedName, utf8UnescapedValue, state.State, out result);
        }
        finally
        {
            if (pooledValue is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledValue, true);
            }

            if (pooledName is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledName, true);
            }
        }
    }

    private static bool ProcessDecodedNameAndEncodedValue<TState, TResult>(ReadOnlySpan<byte> decodedName, ReadOnlySpan<byte> encodedValue, Utf8PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int length = encodedValue.Length;
        byte[]? pooledValue = null;

        Span<byte> utf8UnescapedValue =
            length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledValue = ArrayPool<byte>.Shared.Rent(length));

        JsonReaderHelper.Unescape(encodedValue, utf8UnescapedValue, idx, out int written);
        utf8UnescapedValue = utf8UnescapedValue[..written];

        try
        {
            return state.Parser(decodedName, utf8UnescapedValue, state.State, out result);
        }
        finally
        {
            if (pooledValue != null)
            {
                ArrayPool<byte>.Shared.Return(pooledValue, true);
            }
        }
    }

    private static bool ProcessEncodedNameAndDecodedValue<TState, TResult>(ReadOnlySpan<byte> encodedName, ReadOnlySpan<byte> decodedValue, Utf8PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int length = encodedName.Length;
        byte[]? pooledName = null;

        Span<byte> utf8UnescapedName =
            length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledName = ArrayPool<byte>.Shared.Rent(length));

        JsonReaderHelper.Unescape(encodedName, utf8UnescapedName, idx, out int written);
        utf8UnescapedName = utf8UnescapedName[..written];

        try
        {
            return state.Parser(utf8UnescapedName, decodedValue, state.State, out result);
        }
        finally
        {
            if (pooledName != null)
            {
                ArrayPool<byte>.Shared.Return(pooledName, true);
            }
        }
    }

    private static bool ProcessRawText<TState, TResult>(ReadOnlySpan<byte> rawName, ReadOnlySpan<byte> rawInput, in PropertyParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? result)
    {
        int idx = rawInput.IndexOf(JsonConstants.BackSlash);
        int idx2 = rawName.IndexOf(JsonConstants.BackSlash);

        if (idx >= 0 && idx2 >= 0)
        {
            return ProcessEncodedNameAndEncodedValue(rawName, rawInput, state, out result, idx, idx2);
        }
        else if (idx >= 0 && idx2 < 0)
        {
            return ProcessDecodedNameAndEncodedValue(rawName, rawInput, state, out result, idx);
        }
        else if (idx2 >= 0 && idx < 0)
        {
            return ProcessEncodedNameAndDecodedValue(rawName, rawInput, state, out result, idx2);
        }
        else
        {
            return ProcessDecodedNameAndValue(rawName, rawInput, state, out result);
        }
    }

    private static bool ProcessEncodedNameAndEncodedValue<TState, TResult>(ReadOnlySpan<byte> encodedName, ReadOnlySpan<byte> encodedValue, PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx, int idx2)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int nameLength = encodedName.Length;
        byte[]? pooledName = null;

        Span<byte> utf8UnescapedName =
            nameLength <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledName = ArrayPool<byte>.Shared.Rent(nameLength));

        JsonReaderHelper.Unescape(encodedName, utf8UnescapedName, idx, out int writtenName);
        utf8UnescapedName = utf8UnescapedName[..writtenName];

        int valueLength = encodedValue.Length;
        byte[]? pooledValue = null;

        Span<byte> utf8UnescapedValue =
            valueLength <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledValue = ArrayPool<byte>.Shared.Rent(valueLength));

        JsonReaderHelper.Unescape(encodedValue, utf8UnescapedValue, idx2, out int writtenValue);
        utf8UnescapedValue = utf8UnescapedValue[..writtenValue];

        try
        {
            return ProcessDecodedNameAndValue(utf8UnescapedName, utf8UnescapedValue, state, out result);
        }
        finally
        {
            if (pooledValue is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledValue, true);
            }

            if (pooledName is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledName, true);
            }
        }
    }

    private static bool ProcessDecodedNameAndEncodedValue<TState, TResult>(ReadOnlySpan<byte> decodedName, ReadOnlySpan<byte> encodedValue, PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int length = encodedValue.Length;
        byte[]? pooledValue = null;

        Span<byte> utf8UnescapedValue =
            length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledValue = ArrayPool<byte>.Shared.Rent(length));

        JsonReaderHelper.Unescape(encodedValue, utf8UnescapedValue, idx, out int written);
        utf8UnescapedValue = utf8UnescapedValue[..written];

        try
        {
            return ProcessDecodedNameAndValue(decodedName, utf8UnescapedValue, state, out result);
        }
        finally
        {
            if (pooledValue != null)
            {
                ArrayPool<byte>.Shared.Return(pooledValue, true);
            }
        }
    }

    private static bool ProcessEncodedNameAndDecodedValue<TState, TResult>(ReadOnlySpan<byte> encodedName, ReadOnlySpan<byte> decodedValue, PropertyParserStateWrapper<TState, TResult> state, out TResult? result, int idx)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int length = encodedName.Length;
        byte[]? pooledName = null;

        Span<byte> utf8UnescapedName =
            length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (pooledName = ArrayPool<byte>.Shared.Rent(length));

        JsonReaderHelper.Unescape(encodedName, utf8UnescapedName, idx, out int written);
        utf8UnescapedName = utf8UnescapedName[..written];

        try
        {
            return ProcessDecodedNameAndValue(utf8UnescapedName, decodedValue, state, out result);
        }
        finally
        {
            if (pooledName != null)
            {
                ArrayPool<byte>.Shared.Return(pooledName, true);
            }
        }
    }

    private static bool ProcessDecodedNameAndValue<TState, TResult>(ReadOnlySpan<byte> decodedName, ReadOnlySpan<byte> decodedValue, in PropertyParserStateWrapper<TState, TResult> state, [NotNullWhen(true)] out TResult? value)
    {
        char[]? transcodedNameArray = null;
        int nameLength = checked(decodedName.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
        Span<char> transcodedName = nameLength <= JsonConstants.StackallocThreshold ?
        stackalloc char[JsonConstants.StackallocThreshold] :
        (transcodedNameArray = ArrayPool<char>.Shared.Rent(nameLength));
        int writtenName = JsonReaderHelper.TranscodeHelper(decodedName, transcodedName);
        transcodedName = transcodedName[..writtenName];

        char[]? transcodedValueArray = null;
        int valueLength = checked(decodedValue.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
        Span<char> transcodedValue = valueLength <= JsonConstants.StackallocThreshold ?
        stackalloc char[JsonConstants.StackallocThreshold] :
        (transcodedValueArray = ArrayPool<char>.Shared.Rent(valueLength));
        int writtenValue = JsonReaderHelper.TranscodeHelper(decodedValue, transcodedValue);
        transcodedValue = transcodedValue[..writtenValue];

        bool success = false;
        if (state.Parser(transcodedName, transcodedValue, state.State, out TResult? tmp))
        {
            value = tmp;
            success = true;
        }
        else
        {
            value = default;
        }

        if (transcodedNameArray != null)
        {
            ArrayPool<char>.Shared.Return(transcodedNameArray, true);
        }

        if (transcodedValueArray != null)
        {
            ArrayPool<char>.Shared.Return(transcodedValueArray, true);
        }

        return success;
    }

    /// <summary>
    /// Wraps up the state for the UTF8 parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct Utf8PropertyParserStateWrapper<TState, TResult>(Utf8PropertyParser<TState, TResult> Parser, in TState State, bool Decode);
    /// <summary>
    /// Wraps up the state for the parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct PropertyParserStateWrapper<TState, TResult>(PropertyParser<TState, TResult> Parser, in TState State);

    /// <summary>
    /// Wraps up the state for the UTF8 parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct Utf8ParserStateWrapper<TState, TResult>(Utf8Parser<TState, TResult> Parser, in TState State, bool Decode);
    /// <summary>
    /// Wraps up the state for the parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct ParserStateWrapper<TState, TResult>(Parser<TState, TResult> Parser, in TState State);
}
// <copyright file="JsonElementExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Extensions to JsonElement to provide raw string processing.
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="element">The JSON element to extend.</param>
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
    ///   This value's <see cref="JsonElement.ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetValue<TState, TResult>(this JsonElement element, in Utf8Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        return element.TryGetValue(parser, state, true, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="element">The JSON element to extend.</param>
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
    ///   This value's <see cref="JsonElement.ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetValue<TState, TResult>(this JsonElement element, in Utf8Parser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? value)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException();
        }

        return element.ProcessRawText(new Utf8ParserStateWrapper<TState, TResult>(parser, state, decode), ProcessRawText, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="element">The JSON element to extend.</param>
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
    ///   This value's <see cref="JsonElement.ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public static bool TryGetValue<TState, TResult>(this JsonElement element, in Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException();
        }

        return element.ProcessRawText(new ParserStateWrapper<TState, TResult>(parser, state), ProcessRawText, out value);
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
                    sourceUnescaped.Clear();
                    ArrayPool<byte>.Shared.Return(sourceArray);
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
                    utf8Unescaped.Clear();
                    ArrayPool<byte>.Shared.Return(pooledName);
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
            sourceTranscoded.Clear();
            ArrayPool<char>.Shared.Return(sourceTranscodedArray);
        }

        return success;
    }

    /// <summary>
    /// Wraps up the state for the UTF8 parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct Utf8ParserStateWrapper<TState, TResult>(Utf8Parser<TState, TResult> Parser, in TState State, bool Decode);
    /// <summary>
    /// Wraps up the state for the parser and the parser's native state into a compound state entity.
    /// </summary>
    private readonly record struct ParserStateWrapper<TState, TResult>(Parser<TState, TResult> Parser, in TState State);
}
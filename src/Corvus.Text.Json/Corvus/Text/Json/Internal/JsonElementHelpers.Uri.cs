// <copyright file="JsonElementHelpers.Uri.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON element URI operations.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Tries to format a <see cref="Utf8Uri"/> as a display (human-readable) or canonical (percent-encoded) string.
    /// </summary>
    /// <param name="uri">The URI to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;
    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="result">The formatted string, or <see langword="null"/> if formatting failed.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatUri(Utf8Uri uri, bool isDisplay, [NotNullWhen(true)] out string? result)
    {
        int maxLen = isDisplay ? uri.OriginalUri.Length : uri.OriginalUri.Length * 3;
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            if (isDisplay)
            {
                if (uri.TryFormatDisplay(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }
            else
            {
                if (uri.TryFormatCanonical(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }

            result = null;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8Uri"/> as a display (human-readable) or canonical (percent-encoded) string
    /// into a <see cref="Span{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="uri">The URI to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatUri(Utf8Uri uri, bool isDisplay, Span<char> destination, out int charsWritten)
    {
        byte[]? rentedBuffer = null;
        Span<byte> buffer = destination.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(destination.Length));
        try
        {
            if (isDisplay)
            {
                if (uri.TryFormatDisplay(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }
            else
            {
                if (uri.TryFormatCanonical(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }

            charsWritten = 0;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8UriReference"/> as a display (human-readable) or canonical (percent-encoded) string.
    /// </summary>
    /// <param name="uriReference">The URI reference to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="result">The formatted string, or <see langword="null"/> if formatting failed.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatUriReference(Utf8UriReference uriReference, bool isDisplay, [NotNullWhen(true)] out string? result)
    {
        int maxLen = isDisplay ? uriReference.OriginalUriReference.Length : uriReference.OriginalUriReference.Length * 3;
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            if (isDisplay)
            {
                if (uriReference.TryFormatDisplay(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }
            else
            {
                if (uriReference.TryFormatCanonical(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }

            result = null;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8UriReference"/> as a display (human-readable) or canonical (percent-encoded) string
    /// into a <see cref="Span{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="uriReference">The URI reference to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatUriReference(Utf8UriReference uriReference, bool isDisplay, Span<char> destination, out int charsWritten)
    {
        byte[]? rentedBuffer = null;
        Span<byte> buffer = destination.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(destination.Length));
        try
        {
            if (isDisplay)
            {
                if (uriReference.TryFormatDisplay(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }
            else
            {
                if (uriReference.TryFormatCanonical(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }

            charsWritten = 0;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8Iri"/> as a display (human-readable) or canonical (percent-encoded) string.
    /// </summary>
    /// <param name="iri">The IRI to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="result">The formatted string, or <see langword="null"/> if formatting failed.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatIri(Utf8Iri iri, bool isDisplay, [NotNullWhen(true)] out string? result)
    {
        int maxLen = isDisplay ? iri.OriginalIri.Length : iri.OriginalIri.Length * 3;
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            if (isDisplay)
            {
                if (iri.TryFormatDisplay(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }
            else
            {
                if (iri.TryFormatCanonical(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }

            result = null;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8Iri"/> as a display (human-readable) or canonical (percent-encoded) string
    /// into a <see cref="Span{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="iri">The IRI to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatIri(Utf8Iri iri, bool isDisplay, Span<char> destination, out int charsWritten)
    {
        byte[]? rentedBuffer = null;
        Span<byte> buffer = destination.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(destination.Length));
        try
        {
            if (isDisplay)
            {
                if (iri.TryFormatDisplay(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }
            else
            {
                if (iri.TryFormatCanonical(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }

            charsWritten = 0;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8IriReference"/> as a display (human-readable) or canonical (percent-encoded) string.
    /// </summary>
    /// <param name="iriReference">The IRI reference to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="result">The formatted string, or <see langword="null"/> if formatting failed.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatIriReference(Utf8IriReference iriReference, bool isDisplay, [NotNullWhen(true)] out string? result)
    {
        int maxLen = isDisplay ? iriReference.OriginalIriReference.Length : iriReference.OriginalIriReference.Length * 3;
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLen));
        try
        {
            if (isDisplay)
            {
                if (iriReference.TryFormatDisplay(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }
            else
            {
                if (iriReference.TryFormatCanonical(buffer, out int written))
                {
                    result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
                    return true;
                }
            }

            result = null;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Tries to format a <see cref="Utf8IriReference"/> as a display (human-readable) or canonical (percent-encoded) string
    /// into a <see cref="Span{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="iriReference">The IRI reference to format.</param>
    /// <param name="isDisplay">
    /// <see langword="true"/> to produce the display form with percent-encoded sequences decoded;

    /// <see langword="false"/> to produce the canonical form with all required characters percent-encoded.
    /// </param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public static bool TryFormatIriReference(Utf8IriReference iriReference, bool isDisplay, Span<char> destination, out int charsWritten)
    {
        byte[]? rentedBuffer = null;
        Span<byte> buffer = destination.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(destination.Length));
        try
        {
            if (isDisplay)
            {
                if (iriReference.TryFormatDisplay(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }
            else
            {
                if (iriReference.TryFormatCanonical(buffer, out int written))
                    return JsonReaderHelper.TryTranscode(buffer.Slice(0, written), destination, out charsWritten);
            }

            charsWritten = 0;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
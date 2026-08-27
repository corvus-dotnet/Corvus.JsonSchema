// <copyright file="JsonDocument.JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Derives the JSON Pointer (RFC 6901) of an element relative to the document root
/// by walking the metadata database from the element back to its ancestors.
/// </summary>
public abstract partial class JsonDocument
{
    // Segment stack encoding: a value with the sign bit set is an array index
    // (stored in the low 31 bits); a value with the sign bit clear is the byte
    // offset of a PropertyName row in the metadata database. Array indices are
    // bounded by the 28-bit row count and row byte offsets by the maximum array
    // length, so both fit in 31 bits, and a PropertyName row can never be at
    // offset 0 (the root row).
    private const int ArrayIndexSegmentFlag = unchecked((int)0x8000_0000);

    private const int InitialSegmentStackCapacity = 64;

    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of the element at the specified index,
    /// relative to the document root, as UTF-8 bytes.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="utf8Destination">The destination for the UTF-8 pointer text.</param>
    /// <param name="bytesWritten">The number of bytes written on success; 0 on failure.</param>
    /// <param name="bytesRequired">The total number of bytes the pointer requires, whether or not it fit.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the destination was too small.</returns>
    private protected bool TryGetJsonPointerUnsafe(int index, Span<byte> utf8Destination, out int bytesWritten, out int bytesRequired)
    {
        int[]? rentedSegments = null;
        Span<int> segments = stackalloc int[InitialSegmentStackCapacity];
        try
        {
            int segmentCount = CollectJsonPointerSegments(index, ref segments, ref rentedSegments);

            int written = 0;
            int required = 0;
            bool fits = true;

            // Segments were collected leaf-first; emit them root-first.
            for (int i = segmentCount - 1; i >= 0; i--)
            {
                required++;
                if (fits && written < utf8Destination.Length)
                {
                    utf8Destination[written++] = (byte)'/';
                }
                else
                {
                    fits = false;
                }

                int segment = segments[i];
                if (segment < 0)
                {
                    EmitIndexSegment(segment & ~ArrayIndexSegmentFlag, utf8Destination, ref written, ref required, ref fits);
                }
                else
                {
                    EmitNameSegment(segment, utf8Destination, ref written, ref required, ref fits);
                }
            }

            bytesRequired = required;
            if (fits)
            {
                bytesWritten = written;
                return true;
            }

            bytesWritten = 0;
            return false;
        }
        finally
        {
            if (rentedSegments is not null)
            {
                ArrayPool<int>.Shared.Return(rentedSegments);
            }
        }
    }

    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of the element at the specified index,
    /// relative to the document root, as UTF-16 characters.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="destination">The destination for the pointer text.</param>
    /// <param name="charsWritten">The number of characters written on success; 0 on failure.</param>
    /// <param name="charsRequired">The total number of characters the pointer requires, whether or not it fit.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the destination was too small.</returns>
    private protected bool TryGetJsonPointerUnsafe(int index, Span<char> destination, out int charsWritten, out int charsRequired)
    {
        int[]? rentedSegments = null;
        Span<int> segments = stackalloc int[InitialSegmentStackCapacity];
        try
        {
            int segmentCount = CollectJsonPointerSegments(index, ref segments, ref rentedSegments);

            int written = 0;
            int required = 0;
            bool fits = true;

            // Segments were collected leaf-first; emit them root-first.
            for (int i = segmentCount - 1; i >= 0; i--)
            {
                required++;
                if (fits && written < destination.Length)
                {
                    destination[written++] = '/';
                }
                else
                {
                    fits = false;
                }

                int segment = segments[i];
                if (segment < 0)
                {
                    EmitIndexSegment(segment & ~ArrayIndexSegmentFlag, destination, ref written, ref required, ref fits);
                }
                else
                {
                    EmitNameSegment(segment, destination, ref written, ref required, ref fits);
                }
            }

            charsRequired = required;
            if (fits)
            {
                charsWritten = written;
                return true;
            }

            charsWritten = 0;
            return false;
        }
        finally
        {
            if (rentedSegments is not null)
            {
                ArrayPool<int>.Shared.Return(rentedSegments);
            }
        }
    }

    /// <summary>
    /// Walks the metadata database from the element at <paramref name="index"/> back to the
    /// document root, collecting one segment per ancestry level, leaf-first.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="segments">The segment stack (grown into a rented array when exceeded).</param>
    /// <param name="rentedSegments">The rented backing array for the segment stack, if any.</param>
    /// <returns>The number of segments collected.</returns>
    private int CollectJsonPointerSegments(int index, ref Span<int> segments, ref int[]? rentedSegments)
    {
        int count = 0;
        int cur = index;

        // A PropertyName element designates the property itself; its pointer ends with
        // the property's name segment, and the walk continues from the containing object.
        if (cur != 0 && _parsedData.GetJsonTokenType(cur) == JsonTokenType.PropertyName)
        {
            PushSegment(ref segments, ref rentedSegments, ref count, cur);
            cur = FindUnclosedContainerStart(cur - DbRow.Size, out _);
            Debug.Assert(_parsedData.GetJsonTokenType(cur) == JsonTokenType.StartObject, "A PropertyName row must be contained in an object");
        }

        while (cur != 0)
        {
            int prev = cur - DbRow.Size;
            if (_parsedData.GetJsonTokenType(prev) == JsonTokenType.PropertyName)
            {
                // The parent is an object and the row before the element is its name.
                PushSegment(ref segments, ref rentedSegments, ref count, prev);
                cur = FindUnclosedContainerStart(prev - DbRow.Size, out _);
                Debug.Assert(_parsedData.GetJsonTokenType(cur) == JsonTokenType.StartObject, "A named value must be contained in an object");
            }
            else
            {
                // The parent is an array; count the sibling values that precede the element.
                cur = FindUnclosedContainerStart(prev, out int precedingValues);
                Debug.Assert(_parsedData.GetJsonTokenType(cur) == JsonTokenType.StartArray, "An unnamed value must be contained in an array");
                PushSegment(ref segments, ref rentedSegments, ref count, precedingValues | ArrayIndexSegmentFlag);
            }
        }

        return count;
    }

    /// <summary>
    /// Scans backwards from <paramref name="pos"/> to the start row of the container that
    /// encloses it, skipping complete sibling containers via their End rows. End rows always
    /// carry a locally-correct row count (external references mirror their full structure),
    /// so the scan never leaves this metadata database.
    /// </summary>
    /// <param name="pos">The byte offset of the row at which to start scanning.</param>
    /// <param name="precedingValues">The number of complete values skipped before reaching the container start.</param>
    /// <returns>The byte offset of the enclosing container's start row.</returns>
    private int FindUnclosedContainerStart(int pos, out int precedingValues)
    {
        int count = 0;
        while (true)
        {
            Debug.Assert(pos >= 0, "The scan must terminate at an enclosing container start");
            DbRow row = _parsedData.Get(pos);
            JsonTokenType tokenType = row.TokenType;
            if (tokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                // Jump over the complete container to the row before its start.
                pos -= (row.NumberOfRows + 1) * DbRow.Size;
                count++;
            }
            else if (tokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                // A start row reached directly (not via its end row) is unclosed at this
                // position, so it is the enclosing container.
                precedingValues = count;
                return pos;
            }
            else
            {
                // A simple value, or a PropertyName row (only encountered when the caller
                // is scanning inside an object and discards the count).
                if (tokenType != JsonTokenType.PropertyName)
                {
                    count++;
                }

                pos -= DbRow.Size;
            }
        }
    }

    private static void PushSegment(ref Span<int> segments, ref int[]? rentedSegments, ref int count, int value)
    {
        if (count == segments.Length)
        {
            int[] newRented = ArrayPool<int>.Shared.Rent(segments.Length * 2);
            segments.CopyTo(newRented);
            if (rentedSegments is not null)
            {
                ArrayPool<int>.Shared.Return(rentedSegments);
            }

            rentedSegments = newRented;
            segments = newRented;
        }

        segments[count++] = value;
    }

    private static void EmitIndexSegment(int arrayIndex, Span<byte> destination, ref int written, ref int required, ref bool fits)
    {
        Span<byte> digits = stackalloc byte[10];
        bool formatted = Utf8Formatter.TryFormat(arrayIndex, digits, out int digitCount);
        Debug.Assert(formatted, "A non-negative int always formats into 10 digits");

        required += digitCount;
        if (fits && written + digitCount <= destination.Length)
        {
            digits.Slice(0, digitCount).CopyTo(destination.Slice(written));
            written += digitCount;
        }
        else
        {
            fits = false;
        }
    }

    private static void EmitIndexSegment(int arrayIndex, Span<char> destination, ref int written, ref int required, ref bool fits)
    {
        Span<byte> digits = stackalloc byte[10];
        bool formatted = Utf8Formatter.TryFormat(arrayIndex, digits, out int digitCount);
        Debug.Assert(formatted, "A non-negative int always formats into 10 digits");

        required += digitCount;
        if (fits && written + digitCount <= destination.Length)
        {
            for (int i = 0; i < digitCount; i++)
            {
                destination[written + i] = (char)digits[i];
            }

            written += digitCount;
        }
        else
        {
            fits = false;
        }
    }

    private void EmitNameSegment(int nameRowOffset, Span<byte> destination, ref int written, ref int required, ref bool fits)
    {
        DbRow nameRow = _parsedData.Get(nameRowOffset);
        Debug.Assert(nameRow.TokenType == JsonTokenType.PropertyName, "Name segments must reference PropertyName rows");
        ReadOnlySpan<byte> raw = GetRawSimpleValueFromRowUnsafe(in nameRow).Span;

        if (!nameRow.HasComplexChildren)
        {
            EscapeCopy(raw, destination, ref written, ref required, ref fits);
            return;
        }

        byte[]? rented = null;
        Span<byte> unescapeBuffer = raw.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            JsonReaderHelper.Unescape(raw, unescapeBuffer, out int unescapedLength);
            EscapeCopy(unescapeBuffer.Slice(0, unescapedLength), destination, ref written, ref required, ref fits);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private void EmitNameSegment(int nameRowOffset, Span<char> destination, ref int written, ref int required, ref bool fits)
    {
        DbRow nameRow = _parsedData.Get(nameRowOffset);
        Debug.Assert(nameRow.TokenType == JsonTokenType.PropertyName, "Name segments must reference PropertyName rows");
        ReadOnlySpan<byte> raw = GetRawSimpleValueFromRowUnsafe(in nameRow).Span;

        byte[]? rentedUnescape = null;
        char[]? rentedChars = null;
        Span<byte> unescapeBuffer = raw.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedUnescape = ArrayPool<byte>.Shared.Rent(raw.Length));

        // The transcoded name has at most one char per UTF-8 byte, and unescaping
        // never lengthens the name, so the raw length bounds the char count.
        Span<char> charBuffer = raw.Length <= JsonConstants.StackallocCharThreshold
            ? stackalloc char[JsonConstants.StackallocCharThreshold]
            : (rentedChars = ArrayPool<char>.Shared.Rent(raw.Length));
        try
        {
            scoped ReadOnlySpan<byte> name = raw;
            if (nameRow.HasComplexChildren)
            {
                JsonReaderHelper.Unescape(raw, unescapeBuffer, out int unescapedLength);
                name = unescapeBuffer.Slice(0, unescapedLength);
            }

            int charCount = JsonReaderHelper.TranscodeHelper(name, charBuffer);
            EscapeCopy(charBuffer.Slice(0, charCount), destination, ref written, ref required, ref fits);
        }
        finally
        {
            if (rentedUnescape is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedUnescape);
            }

            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }
    }

    private static void EscapeCopy(ReadOnlySpan<byte> name, Span<byte> destination, ref int written, ref int required, ref bool fits)
    {
        for (int i = 0; i < name.Length; i++)
        {
            byte b = name[i];
            if (b is (byte)'~' or (byte)'/')
            {
                required += 2;
                if (fits && written + 2 <= destination.Length)
                {
                    destination[written] = (byte)'~';
                    destination[written + 1] = b == (byte)'~' ? (byte)'0' : (byte)'1';
                    written += 2;
                }
                else
                {
                    fits = false;
                }
            }
            else
            {
                required++;
                if (fits && written < destination.Length)
                {
                    destination[written++] = b;
                }
                else
                {
                    fits = false;
                }
            }
        }
    }

    private static void EscapeCopy(ReadOnlySpan<char> name, Span<char> destination, ref int written, ref int required, ref bool fits)
    {
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '~' or '/')
            {
                required += 2;
                if (fits && written + 2 <= destination.Length)
                {
                    destination[written] = '~';
                    destination[written + 1] = c == '~' ? '0' : '1';
                    written += 2;
                }
                else
                {
                    fits = false;
                }
            }
            else
            {
                required++;
                if (fits && written < destination.Length)
                {
                    destination[written++] = c;
                }
                else
                {
                    fits = false;
                }
            }
        }
    }
}
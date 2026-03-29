// <copyright file="JsonReaderHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using Corvus.Text.Json.Internal;
using NodaTime;

namespace Corvus.Text.Json;

internal static partial class JsonReaderHelper
{
    private const string SpecialCharacters = ". '/\"[]()\t\n\r\f\b\\\u0085\u2028\u2029";

#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> s_specialCharacters = SearchValues.Create(SpecialCharacters);

    public static bool ContainsSpecialCharacters(this ReadOnlySpan<char> text) =>
        text.ContainsAny(s_specialCharacters);

#else
    public static bool ContainsSpecialCharacters(this ReadOnlySpan<char> text) =>
        text.IndexOfAny(SpecialCharacters.AsSpan()) >= 0;
#endif

    public static (int, int) CountNewLines(ReadOnlySpan<byte> data)
    {
        int lastLineFeedIndex = data.LastIndexOf(JsonConstants.LineFeed);
        int newLines = 0;

        if (lastLineFeedIndex >= 0)
        {
            newLines = 1;
            data = data.Slice(0, lastLineFeedIndex);
#if NET8_0_OR_GREATER
            newLines += data.Count(JsonConstants.LineFeed);
#else
            int pos;
            while ((pos = data.IndexOf(JsonConstants.LineFeed)) >= 0)
            {
                newLines++;
                data = data.Slice(pos + 1);
            }
#endif
        }

        return (newLines, lastLineFeedIndex);
    }

    // Returns true if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
    // Otherwise, return false.
    public static bool IsTokenTypePrimitive(JsonTokenType tokenType) =>
        (tokenType - JsonTokenType.String) <= (JsonTokenType.Null - JsonTokenType.String);

    // A hex digit is valid if it is in the range: [0..9] | [A..F] | [a..f]
    // Otherwise, return false.
    public static bool IsHexDigit(byte nextByte) => HexConverter.IsHexChar(nextByte);

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool isEscaped, out DateTime value)
    {
        if (!JsonHelpers.IsValidDateTimeOffsetParseLength(segment.Length))
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (isEscaped)
        {
            return TryGetEscapedDateTime(segment, out value);
        }

        Debug.Assert(segment.IndexOf(JsonConstants.BackSlash) == -1);

        if (JsonHelpers.TryParseAsISO(segment, out DateTime tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetEscapedDateTime(ReadOnlySpan<byte> source, out DateTime value)
    {
        Debug.Assert(source.Length <= JsonConstants.MaximumEscapedDateTimeOffsetParseLength);
        Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

        Unescape(source, sourceUnescaped, out int written);
        Debug.Assert(written > 0);

        sourceUnescaped = sourceUnescaped.Slice(0, written);
        Debug.Assert(!sourceUnescaped.IsEmpty);

        if (JsonHelpers.IsValidUnescapedDateTimeOffsetParseLength(sourceUnescaped.Length)
            && JsonHelpers.TryParseAsISO(sourceUnescaped, out DateTime tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool isEscaped, out DateTimeOffset value)
    {
        if (!JsonHelpers.IsValidDateTimeOffsetParseLength(segment.Length))
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (isEscaped)
        {
            return TryGetEscapedDateTimeOffset(segment, out value);
        }

        Debug.Assert(segment.IndexOf(JsonConstants.BackSlash) == -1);

        if (JsonHelpers.TryParseAsISO(segment, out DateTimeOffset tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetEscapedDateTimeOffset(ReadOnlySpan<byte> source, out DateTimeOffset value)
    {
        Debug.Assert(source.Length <= JsonConstants.MaximumEscapedDateTimeOffsetParseLength);
        Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

        Unescape(source, sourceUnescaped, out int written);
        Debug.Assert(written > 0);

        sourceUnescaped = sourceUnescaped.Slice(0, written);
        Debug.Assert(!sourceUnescaped.IsEmpty);

        if (JsonHelpers.IsValidUnescapedDateTimeOffsetParseLength(sourceUnescaped.Length)
            && JsonHelpers.TryParseAsISO(sourceUnescaped, out DateTimeOffset tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out OffsetDateTime value)
    {
        if (segment.Length > JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (hasComplexChildren)
        {
            Debug.Assert(segment.Length <= JsonConstants.MaximumEscapedDateTimeOffsetParseLength);
            Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

            JsonReaderHelper.Unescape(segment, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            return JsonElementHelpers.TryParseOffsetDateTime(segment, out value);
        }

        return JsonElementHelpers.TryParseOffsetDateTime(segment, out value);
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out OffsetDate value)
    {
        if (segment.Length > JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        if (hasComplexChildren)
        {
            Debug.Assert(segment.Length <= JsonConstants.MaximumEscapedDateTimeOffsetParseLength);
            Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

            JsonReaderHelper.Unescape(segment, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            return JsonElementHelpers.TryParseOffsetDate(segment, out value);
        }

        return JsonElementHelpers.TryParseOffsetDate(segment, out value);
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out OffsetTime value)
    {
        if (segment.Length > JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        if (hasComplexChildren)
        {
            Debug.Assert(segment.Length <= JsonConstants.MaximumEscapedDateTimeOffsetParseLength);
            Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

            JsonReaderHelper.Unescape(segment, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            return JsonElementHelpers.TryParseOffsetTime(segment, out value);
        }

        return JsonElementHelpers.TryParseOffsetTime(segment, out value);
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out LocalDate value)
    {
        if (segment.Length > JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (hasComplexChildren)
        {
            Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

            JsonReaderHelper.Unescape(segment, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            return JsonElementHelpers.TryParseLocalDate(segment, out value);
        }

        return JsonElementHelpers.TryParseLocalDate(segment, out value);
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out Period value)
    {
        if (segment.Length > JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        if (hasComplexChildren)
        {
            Span<byte> sourceUnescaped = stackalloc byte[JsonConstants.MaximumEscapedDateTimeOffsetParseLength];

            JsonReaderHelper.Unescape(segment, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            return Period.TryParse(segment, out value);
        }

        return Period.TryParse(segment, out value);
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out Guid value)
    {
        if ((uint)segment.Length > (uint)JsonConstants.MaximumEscapedGuidLength)
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (hasComplexChildren)
        {
            return JsonReaderHelper.TryGetEscapedGuid(segment, out value);
        }

        Debug.Assert(segment.IndexOf(JsonConstants.BackSlash) == -1);

        if (segment.Length == JsonConstants.MaximumFormatGuidLength
            && Utf8Parser.TryParse(segment, out Guid tmp, out _, 'D'))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetEscapedGuid(ReadOnlySpan<byte> source, out Guid value)
    {
        Debug.Assert(source.Length <= JsonConstants.MaximumEscapedGuidLength);

        Span<byte> utf8Unescaped = stackalloc byte[JsonConstants.MaximumEscapedGuidLength];
        Unescape(source, utf8Unescaped, out int written);
        Debug.Assert(written > 0);

        utf8Unescaped = utf8Unescaped.Slice(0, written);
        Debug.Assert(!utf8Unescaped.IsEmpty);

        if (utf8Unescaped.Length == JsonConstants.MaximumFormatGuidLength
            && Utf8Parser.TryParse(utf8Unescaped, out Guid tmp, out _, 'D'))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

#if NET
    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out DateOnly value)
    {
        if (TryGetValue(segment, hasComplexChildren, out LocalDate localDate))
        {
            value = new DateOnly(localDate.Year, localDate.Month, localDate.Day);
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetValue(ReadOnlySpan<byte> segment, bool hasComplexChildren, out TimeOnly value)
    {
        if (TryGetValue(segment, hasComplexChildren, out OffsetTime offsetTime))
        {
            value = new TimeOnly(offsetTime.TimeOfDay.TickOfDay);
            return true;
        }

        value = default;
        return false;
    }
#endif

    /// <summary>
    /// Encodes the ~ encoding in a pointer.
    /// </summary>
    /// <param name="unencodedFragment">The encoded fragment.</param>
    /// <param name="fragment">The span into which to write the result.</param>
    /// <returns><see langword="true"/> if the value could be written.</returns>
    public static bool TryUnescapeAndEncodePointer(ReadOnlySpan<byte> unencodedFragment, Span<byte> fragment, out int written)
    {
        int idx = unencodedFragment.IndexOf((byte)'\\');
        if (idx < 0)
        {
            return TryEncodePointer(unencodedFragment, fragment, out written);
        }

        if (!TryEncodePointer(unencodedFragment.Slice(0, idx), fragment, out int encodedWritten))
        {
            written = 0;
            return false;
        }

        byte[]? buffer = null;
        int length = unencodedFragment.Length - idx;
        Span<byte> unescapedSegment =
            length > JsonConstants.StackallocByteThreshold
            ? (buffer = ArrayPool<byte>.Shared.Rent(length))
            : stackalloc byte[JsonConstants.StackallocByteThreshold];

        try
        {
            if (!TryUnescape(unencodedFragment.Slice(idx), unescapedSegment, out int unencodedWritten))
            {
                written = 0;
                return false;
            }

            ReadOnlySpan<byte> toEncode = unescapedSegment.Slice(0, unencodedWritten);

            if (!TryEncodePointer(toEncode, fragment.Slice(encodedWritten), out int encodedWritten2))
            {
                written = 0;
                return false;
            }

            written = encodedWritten + encodedWritten2;
            return true;
        }
        finally
        {
            if (buffer is not null)
            {
                // Could contain sensitive data
                buffer.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Encodes the ~ encoding in a pointer.
    /// </summary>
    /// <param name="unencodedFragment">The encoded fragment.</param>
    /// <param name="fragment">The span into which to write the result.</param>
    /// <returns>The length of the decoded fragment.</returns>
    public static bool TryEncodePointer(ReadOnlySpan<byte> unencodedFragment, Span<byte> fragment, out int written)
    {
        int readIndex = 0;
        int writeIndex = 0;

        if (fragment.Length < unencodedFragment.Length)
        {
            written = 0;
            return false;
        }

        while (readIndex < unencodedFragment.Length && writeIndex < fragment.Length)
        {
            if (unencodedFragment[readIndex] == (byte)'~')
            {
                fragment[writeIndex] = (byte)'~';
                if (fragment.Length < writeIndex + 1)
                {
                    written = 0;
                    return false;
                }

                fragment[writeIndex + 1] = (byte)'0';
                readIndex++;
                writeIndex += 2;
            }
            else if (unencodedFragment[readIndex] == '/')
            {
                fragment[writeIndex] = (byte)'~';
                if (fragment.Length < writeIndex + 1)
                {
                    written = 0;
                    return false;
                }

                fragment[writeIndex + 1] = (byte)'1';
                readIndex++;
                writeIndex += 2;
            }
            else
            {
                fragment[writeIndex] = unencodedFragment[readIndex];
                readIndex++;
                writeIndex++;
            }
        }

        written = writeIndex;
        return readIndex == unencodedFragment.Length;
    }
}
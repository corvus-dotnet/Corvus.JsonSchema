// <copyright file="JsonSchemaEvaluation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Text;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    public static readonly JsonSchemaMessageProvider<string> ExpectedConstant = static (expectedValue, buffer, out written) => ExpectedConstantValue(expectedValue, buffer, out written);

    public static readonly JsonSchemaMessageProvider EvaluatedSubschema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_EvaluatedSubschema.AsSpan(), buffer, out written);

    /// <summary>
    /// Creates a schema location for an indexed keyword by appending the index to the base location.
    /// </summary>
    /// <param name="keywordSchemaLocation">The base schema location for the keyword.</param>
    /// <param name="index">The index to append to the location.</param>
    /// <param name="buffer">The buffer to write the resulting location to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool SchemaLocationForIndexedKeyword(ReadOnlySpan<byte> keywordSchemaLocation, int index, Span<byte> buffer, out int written)
    {
        if (buffer.Length < keywordSchemaLocation.Length)
        {
            written = 0;
            return false;
        }

        TryCopyPath(keywordSchemaLocation, buffer, out written);

        if (buffer[written - 1] != (byte)'/')
        {
            if (buffer.Length <= written)
            {
                written = 0;
                return false;
            }

            buffer[written++] = (byte)'/';
        }

        if (!Utf8Formatter.TryFormat(index, buffer[written..], out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;
        return true;
    }

    /// <summary>
    /// Creates a schema location for an indexed keyword by appending the index to the base location and dependency.
    /// </summary>
    /// <param name="keywordSchemaLocation">The base schema location for the keyword.</param>
    /// <param name="dependencyName">The name of the dependency.</param>
    /// <param name="index">The index to append to the location.</param>
    /// <param name="buffer">The buffer to write the resulting location to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool SchemaLocationForIndexedKeywordWithDependency(ReadOnlySpan<byte> keywordSchemaLocation, ReadOnlySpan<byte> dependencyName, int index, Span<byte> buffer, out int written)
    {
        if (!TryCopyPath(keywordSchemaLocation, buffer, out written))
        {
            written = 0;
            return false;
        }

        if (buffer[written - 1] != (byte)'/')
        {
            if (buffer.Length <= written)
            {
                written = 0;
                return false;
            }

            buffer[written++] = (byte)'/';
        }

        if (TryCopyPath(dependencyName, buffer, out int bytesWritten))
        {
            written += bytesWritten;
        }
        else
        {
            written = 0;
            return false;
        }

        if (buffer[written - 1] != (byte)'/')
        {
            if (buffer.Length <= written)
            {
                written = 0;
                return false;
            }

            buffer[written++] = (byte)'/';
        }

        if (!Utf8Formatter.TryFormat(index, buffer[written..], out int bytesWritten2))
        {
            written = 0;
            return false;
        }

        written += bytesWritten2;
        return true;
    }

    /// <summary>
    /// Tries to copy a message to the specified buffer.
    /// </summary>
    /// <param name="readOnlySpan">The message to copy.</param>
    /// <param name="buffer">The buffer to copy the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the copy succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCopyMessage(ReadOnlySpan<byte> readOnlySpan, Span<byte> buffer, out int written)
    {
        if (readOnlySpan.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        readOnlySpan.CopyTo(buffer);
        written = readOnlySpan.Length;
        return true;
    }

    /// <summary>
    /// Tries to copy the path to the output buffer.
    /// </summary>
    /// <remarks>
    /// The path must be a fully canonical URI.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCopyPath(ReadOnlySpan<byte> readOnlySpan, Span<byte> buffer, out int written)
    {
        Debug.Assert(Utf8UriTools.Validate(readOnlySpan, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: true, allowUNCPath: false));

        if (readOnlySpan.Length == 0)
        {
            written = 0;
            return true;
        }

        if (readOnlySpan[0] == (byte)'#')
        {
            readOnlySpan = readOnlySpan[1..];
        }

        if (readOnlySpan.Length == 0)
        {
            written = 0;
            return true;
        }

        if (readOnlySpan[0] == (byte)'/')
        {
            readOnlySpan = readOnlySpan[1..];
        }

        if (readOnlySpan.Length == 0)
        {
            written = 0;
            return true;
        }

        if (readOnlySpan.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        readOnlySpan.CopyTo(buffer);
        written = readOnlySpan.Length;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AppendSingleQuotedValue(ReadOnlySpan<byte> value, Span<byte> buffer, ref int written)
    {
        if (value.Length == 0)
        {
            return true;
        }

        if (buffer.Length < written + value.Length + 4)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)' ';
        buffer[written++] = (byte)'\'';
        value.CopyTo(buffer[written..]);
        written += value.Length;
        buffer[written++] = (byte)'\'';
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AppendSingleQuotedValue(string value, Span<byte> buffer, ref int written)
    {
        if (value.Length == 0)
        {
            return true;
        }

        if (buffer.Length < written + value.Length + 4)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)' ';
        buffer[written++] = (byte)'\'';
        int writtenBytes = JsonReaderHelper.TranscodeHelper(value.AsSpan(), buffer.Slice(written));
        if (writtenBytes == 0)
        {
            return false;
        }

        written += writtenBytes;
        buffer[written++] = (byte)'\'';
        return true;
    }

    private static bool AppendQuotedInteger(int value, Span<byte> buffer, ref int written)
    {
        if (buffer.Length < 3)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'\'';
        if (!Utf8Formatter.TryFormat(value, buffer.Slice(written), out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;

        if (written == buffer.Length)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'\'';
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AppendValueAndExponent(ulong value, int exponent, Span<byte> buffer, ref int written)
    {
        if (buffer.Length < written + 3)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)' ';
        buffer[written++] = (byte)'\'';

        if (!Utf8Formatter.TryFormat(value, buffer[written..], out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;

        if (buffer.Length < written + 3)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'E';

        if (!Utf8Formatter.TryFormat(exponent, buffer[written..], out bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;

        if (buffer.Length < written + 1)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'\'';
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AppendValueAndExponent(BigInteger value, int exponent, Span<byte> buffer, ref int written)
    {
        if (buffer.Length < written + 3)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)' ';
        buffer[written++] = (byte)'\'';

        if (!value.TryFormat(buffer[written..], out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;

        if (buffer.Length < written + 3)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'E';

        if (!Utf8Formatter.TryFormat(exponent, buffer[written..], out bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;

        if (buffer.Length < written + 1)
        {
            written = 0;
            return false;
        }

        buffer[written++] = (byte)'\'';
        return true;
    }

    /// <summary>
    /// Tries to write a message indicating the expected type for a value.
    /// </summary>
    /// <param name="typeName">The name of the expected type.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedType(ReadOnlySpan<byte> typeName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedType.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(typeName, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected type for a value.
    /// </summary>
    /// <param name="divisor">The integral part of the divisor.</param>
    /// <param name="divisor">The exponent of the divisor.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool ExpectedMultipleOfDivisor(string divisor, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedMultipleOf.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(divisor, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating that the format was not recognized.
    /// </summary>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IgnoredUnrecognizedFormat(Span<byte> buffer, out int written)
    {
        return JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_IgnoredUnrecognizedFormat.AsSpan(), buffer, out written);
    }

    /// <summary>
    /// Tries to write a message indicating that the format was not asserted.
    /// </summary>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IgnoredFormatNotAsserted(Span<byte> buffer, out int written)
    {
        return JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_IgnoredFormatNotAsserted.AsSpan(), buffer, out written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IgnoredNotType(ReadOnlySpan<byte> typeName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_IgnoredNotType.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(typeName, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedMatchRegularExpression(string regularExpression, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedMatchRegularExpression.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(regularExpression, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedStringEqualsValue(string expected, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedStringEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(expected, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthEquals(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthNotEquals(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthNotEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthGreaterThan(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthGreaterThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthGreaterThanOrEquals(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthGreaterThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthLessThan(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthLessThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLengthLessThanOrEquals(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLengthLessThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected constant value.
    /// </summary>
    /// <param name="expectedValue">The name of the expected type.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedConstantValue(string expectedValue, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedConstantValue.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(expectedValue, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value was null.
    /// </summary>
    /// <param name="expectedValue">The name of the expected type.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedNullValue(Span<byte> buffer, out int written)
    {
        return JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedNullValue.AsSpan(), buffer, out written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value was boolean true.
    /// </summary>
    /// <param name="expectedValue">The name of the expected type.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedBooleanTrueValue(Span<byte> buffer, out int written)
    {
        return JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedBooleanTrueValue.AsSpan(), buffer, out written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value was boolean false.
    /// </summary>
    /// <param name="expectedValue">The name of the expected type.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedBooleanFalseValue(Span<byte> buffer, out int written)
    {
        return JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedBooleanFalseValue.AsSpan(), buffer, out written);
    }
}
// <copyright file="BigIntegerPolyfills.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Numerics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Polyfills for <see cref="BigInteger"/> methods that are not available in all target frameworks.
/// </summary>
[CLSCompliant(false)]
public static class BigIntegerPolyfills
{
#if !NET
    /// <summary>
    /// Tries to format the value of the current <see cref="BigInteger"/> instance into the provided span of characters.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="destination">The span in which to write the formatted value.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters that were written to the destination.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryFormat(this in BigInteger value, Span<char> destination, out int charsWritten)
    {
        // Fast path for zero and one to avoid
        // allocating a string for these common cases.
        if (value.IsZero)
        {
            if (destination.Length < 1)
            {
                charsWritten = 0;
                return false;
            }

            destination[0] = '0';
            charsWritten = 1;
            return true;
        }

        if (value.IsOne)
        {
            if (destination.Length < 1)
            {
                charsWritten = 0;
                return false;
            }

            destination[0] = '1';
            charsWritten = 1;
            return true;
        }

        // Unfortunately, the guts of BigInteger are not accessible to us
        // and we have to use ToString() to get the string representation of the value.
        string valueString = value.ToString();
        bool result = valueString.AsSpan().TryCopyTo(destination);
        charsWritten = result ? valueString.Length : 0;
        return result;
    }
#endif

#if NET

    /// <summary>
    /// Gets the minimum format buffer length.
    /// </summary>
    /// <param name="bigInteger">The value for which to get the format buffer length.</param>
    /// <param name="minimumLength">The minimum length for a text buffer to format the number.</param>
    /// <returns><see langword="true"/> if the buffer length required for the number can be safely allocated.</returns>
    public static bool TryGetMinimumFormatBufferLength(this in BigInteger bigInteger, out int minimumLength)
    {
        // Up to two characters per nybble
        // So divide by 4 and multiply by 2 => divide by 2 => shift left 1
        long value = bigInteger.GetBitLength() << 1;

        if (bigInteger.Sign < 0)
        {
            // One for the sign
            value++;
        }

        if (value > Array.MaxLength)
        {
            minimumLength = 0;
            return false;
        }

        minimumLength = (int)value;
        return true;
    }

#endif

    /// <summary>
    /// Tries to format the value of the current <see cref="BigInteger"/> instance into the provided span of bytes.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="destination">The span in which to write the formatted value as UTF-8.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes that were written to the destination.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryFormat(this in BigInteger value, Span<byte> destination, out int bytesWritten)
    {
        char[]? charBuffer = null;
        Span<char> charSpan =
            destination.Length < JsonConstants.StackallocCharThreshold
                ? stackalloc char[destination.Length]
                : (charBuffer = ArrayPool<char>.Shared.Rent(destination.Length));

        int charsWritten = 0;
        try
        {
            if (!value.TryFormat(charSpan, out charsWritten))
            {
                bytesWritten = 0;
                return false;
            }

            return JsonReaderHelper.TryGetUtf8FromText(charSpan.Slice(0, charsWritten), destination, out bytesWritten);
        }
        finally
        {
            if (charBuffer is char[] b)
            {
                // We have to clear the array, as it may contain sensitive data
                charSpan.Slice(0, charsWritten).Clear();
                ArrayPool<char>.Shared.Return(b);
            }
        }
    }

    extension(BigInteger i)
    {
        /// <summary>
        /// Tries to parse a span of UTF-8 characters into a <see cref="BigInteger"/> value.
        /// 1</summary>
        /// <param name="segment">The span of UTF-8 bytes to parse.</param>
        /// <param name="value">When this method returns, contains the <see cref="BigInteger"/> value equivalent to the bytes contained in <paramref name="segment"/>, if the conversion succeeded, or zero if the conversion failed.</param>
        /// <returns><see langword="true"/> if the parse operation was successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<byte> segment, out BigInteger value)
        {
#if NET
            char[]? rentedChars = null;
            int desiredLength = Encoding.UTF8.GetMaxCharCount(segment.Length);
            Span<char> buffer =
            segment.Length < JsonConstants.StackallocCharThreshold
                ? stackalloc char[desiredLength]
                : (rentedChars = ArrayPool<char>.Shared.Rent(desiredLength));
            try
            {
                if (JsonReaderHelper.TryGetTextFromUtf8(segment, buffer, out int written))
                {
                    return BigInteger.TryParse(buffer.Slice(0, written), out value);
                }

                value = default;
                return false;
            }
            finally
            {
                if (rentedChars is char[] b)
                {
                    ArrayPool<char>.Shared.Return(b);
                }
            }
#else
            // Sadly, we have to allocate a string here, as BigInteger does not support parsing
            // from a ReadOnlySpan in .NET Standard 2.0.
            string valueString = JsonReaderHelper.GetTextFromUtf8(segment);
            return BigInteger.TryParse(valueString, out value);
#endif
        }

#if !NET
        /// <summary>
        /// Tries to parse a span of characters into a <see cref="BigInteger"/> value.
        /// </summary>
        /// <param name="segment">The span of characters to parse.</param>
        /// <param name="value">When this method returns, contains the <see cref="BigInteger"/> value equivalent to the characters contained in <paramref name="segment"/>, if the conversion succeeded, or zero if the conversion failed.</param>
        /// <returns><see langword="true"/> if the parse operation was successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> segment, out BigInteger value)
        {
            // Sadly, we have to allocate a string here, as BigInteger does not support parsing
            // from a ReadOnlySpan in .NET Standard 2.0.
            return BigInteger.TryParse(segment.ToString(), out value);
        }
#endif
    }
}
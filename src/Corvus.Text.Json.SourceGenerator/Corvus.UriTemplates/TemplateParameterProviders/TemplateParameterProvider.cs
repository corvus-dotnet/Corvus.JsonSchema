// <copyright file="TemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

using Corvus.HighPerformance;

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// Provides helpers for implementers of a <see cref="ITemplateParameterProvider{TParameterPayload}"/>.
/// </summary>
public static class TemplateParameterProvider
{
    private const string UriReservedSymbols = ":/?#[]@!$&'()*+,;=";
    private const string UriUnreservedSymbols = "-._~";
#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> PossibleHexChars = SearchValues.Create("0123456789AaBbCcDdEeF");
#else
    private const string PossibleHexChars = "0123456789AaBbCcDdEeF";
#endif
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - Analysers need to catch up with the new syntax.
    private static readonly char[] HexDigits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

    /// <summary>
    /// Encode a value to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="allowReserved">A value indicating whether to allow reserved symbols.</param>
    public static void Encode(ref ValueStringBuilder output, ReadOnlySpan<char> value, bool allowReserved)
    {
        int length = value.Length;
        for (int i = 0; i < length; ++i)
        {
            char c = value[i];
            if ((c >= 'A' && c <= 'z') ////                                     Alpha
                || (c >= '0' && c <= '9') ////                                  Digit
                || UriUnreservedSymbols.IndexOf(c) != -1 ////                   Unreserved symbols  - These should never be percent encoded
                || (allowReserved && UriReservedSymbols.IndexOf(c) != -1)) //// Reserved symbols - should be included if requested (+)
            {
                output.Append(c);
            }
            else if (allowReserved && c == '%' && IsEscapeSequence(value, i))
            {
                output.Append(value.Slice(i, 3));

                // Skip the next two characters
                i += 2;
            }
            else
            {
                WriteHexDigits(ref output, c);
            }
        }

        static void WriteHexDigits(ref ValueStringBuilder output, char c)
        {
#if NET8_0_OR_GREATER
            Span<char> source = stackalloc char[1];
            source[0] = c;
            Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(1)];
            int encoded = Encoding.UTF8.GetBytes(source, bytes);
            foreach (byte aByte in bytes[..encoded])
            {
                output.Append('%');
                output.Append(HexDigits[(aByte & 240) >> 4]);
                output.Append(HexDigits[aByte & 15]);
            }
#else
            byte[] bytes = ArrayPool<byte>.Shared.Rent(4);
            char[] source = ArrayPool<char>.Shared.Rent(1);

            try
            {
                source[0] = c;
                int encoded = Encoding.UTF8.GetBytes(source, 0, 1, bytes, 0);
                foreach (byte aByte in bytes.AsSpan()[..encoded])
                {
                    output.Append('%');
                    output.Append(HexDigits[(aByte & 240) >> 4]);
                    output.Append(HexDigits[aByte & 15]);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
                ArrayPool<char>.Shared.Return(source);
            }
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEscapeSequence(ReadOnlySpan<char> value, int i)
    {
        if (value.Length <= i + 2)
        {
            return false;
        }

        return IsHex(value[i + 1]) && IsHex(value[i + 2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(char v)
    {
#if NET8_0_OR_GREATER
        return PossibleHexChars.Contains(v);
#else
        return PossibleHexChars.IndexOf(v) >= 0;
#endif
    }
}
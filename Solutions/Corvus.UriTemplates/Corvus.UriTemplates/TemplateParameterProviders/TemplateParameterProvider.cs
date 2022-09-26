// <copyright file="TemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance;

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// Provides helpers for implementers of a <see cref="ITemplateParameterProvider{TParameterPayload}"/>.
/// </summary>
public static class TemplateParameterProvider
{
    private const string UriReservedSymbols = ":/?#[]@!$&'()*+,;=";
    private const string UriUnreservedSymbols = "-._~";
    private static readonly ReadOnlyMemory<char> PossibleHexChars = "0123456789AaBbCcDdEeF".AsMemory();
    private static readonly char[] HexDigits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    /// <summary>
    /// Encode a value to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="allowReserved">A value indicating whether to allow reserved symbols.</param>
    public static void Encode(IBufferWriter<char> output, ReadOnlySpan<char> value, bool allowReserved)
    {
        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];
            if ((c >= 'A' && c <= 'z') ////                                     Alpha
                || (c >= '0' && c <= '9') ////                                  Digit
                || UriUnreservedSymbols.IndexOf(c) != -1 ////                   Unreserved symbols  - These should never be percent encoded
                || (allowReserved && UriReservedSymbols.IndexOf(c) != -1)) //// Reserved symbols - should be included if requested (+)
            {
                output.Write(c);
            }
            else if (allowReserved && c == '%' && IsEscapeSequence(value, i))
            {
                output.Write(value.Slice(i, 3));

                // Skip the next two characters
                i += 2;
            }
            else
            {
                WriteHexDigits(output, c);
            }
        }

        static void WriteHexDigits(IBufferWriter<char> output, char c)
        {
            Span<char> source = stackalloc char[1];
            source[0] = c;
            Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(1)];
            int encoded = Encoding.UTF8.GetBytes(source, bytes);
            foreach (byte abyte in bytes[..encoded])
            {
                output.Write('%');
                output.Write(HexDigits[(abyte & 240) >> 4]);
                output.Write(HexDigits[abyte & 15]);
            }
        }
    }

    private static bool IsEscapeSequence(ReadOnlySpan<char> value, int i)
    {
        if (value.Length <= i + 2)
        {
            return false;
        }

        return IsHex(value[i + 1]) && IsHex(value[i + 2]);
    }

    private static bool IsHex(char v)
    {
#if NETSTANDARD2_1
        Span<char> vSpan = stackalloc char[1];
        vSpan[0] = v;
        return PossibleHexChars.Span.Contains(vSpan, StringComparison.Ordinal);
#else
        return PossibleHexChars.Span.Contains(v);
#endif
    }
}
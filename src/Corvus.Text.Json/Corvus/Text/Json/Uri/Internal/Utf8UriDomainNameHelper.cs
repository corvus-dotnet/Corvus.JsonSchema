// <copyright file="Utf8UriDomainNameHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#if NET

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

#endif

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for validating UTF-8 domain names.
/// This does a similar job to MatchIdnHostname but it doesn't punycode decode and validate the resulting address.
/// </summary>
internal class Utf8UriDomainNameHelper
{
#if NET

    // Takes into account the additional legal domain name characters '-' and '_'
    // Note that '_' char is formally invalid but is historically in use, especially on corpnets
    private static readonly SearchValues<byte> s_validChars =
        SearchValues.Create("-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz."u8);

    // For IRI, we're accepting anything non-ascii (except 0x80-0x9F), so invert the condition to search for invalid ascii characters.
    private static readonly SearchValues<char> s_iriInvalidChars = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" +
        "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F" +
        " !\"#$%&'()*+,/:;<=>?@[\\]^`{|}~\u007F" +
        "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
        "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F");

#else
    // Takes into account the additional legal domain name characters '-' and '_'
    // Note that '_' char is formally invalid but is historically in use, especially on corpnets
    private static ReadOnlySpan<byte> s_validChars => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz."u8;

    // For IRI, we're accepting anything non-ascii (except 0x80-0x9F), so invert the condition to search for invalid ascii characters.
    private static ReadOnlySpan<char> s_iriInvalidChars =>
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" +
        "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F" +
        " !\"#$%&'()*+,/:;<=>?@[\\]^`{|}~\u007F" +
        "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
        "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F";
#endif

    /// <summary>
    /// Determines whether the specified hostname is valid.
    /// </summary>
    /// <param name="hostname">The hostname to validate.</param>
    /// <param name="iri">A value indicating whether IRI parsing is enabled.</param>
    /// <param name="notImplicitFile">A value indicating whether this is not an implicit file URI.</param>
    /// <param name="length">When this method returns, contains the length of the valid hostname portion.</param>
    /// <returns><see langword="true"/> if the hostname is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(ReadOnlySpan<byte> hostname, bool iri, bool notImplicitFile, out int length)
    {
        int invalidCharOrDelimiterIndex = iri
            ? IndexOfAny(hostname, s_iriInvalidChars)
            : Utf8UriTools.IndexOfAnyExcept(hostname, s_validChars);

        if (invalidCharOrDelimiterIndex >= 0)
        {
            byte c = hostname[invalidCharOrDelimiterIndex];

            if (c is (byte)'/' or (byte)'\\' || (notImplicitFile && (c is (byte)':' or (byte)'?' or (byte)'#')))
            {
                hostname = hostname.Slice(0, invalidCharOrDelimiterIndex);
            }
            else
            {
                length = 0;
                return false;
            }
        }

        length = hostname.Length;

        if (length == 0)
        {
            return false;
        }

        // Determines whether a string is a valid domain name label. In keeping
        // with RFC 1123, section 2.1, the requirement that the first character
        // of a label be alphabetic is dropped. Therefore, Domain names are
        // formed as:
        // <label> -> <alphanum> [<alphanum> | <hyphen> | <underscore>] * 62
        // We already verified the content, now verify the lengths of individual labels
        while (true)
        {
            byte firstChar = hostname[0];
            if ((!iri || firstChar < 0xA0) && !Utf8UriTools.IsAsciiLetterOrDigit(firstChar))
            {
                return false;
            }

            int dotIndex = iri
                ? IndexOfIriDot(hostname)
                : hostname.IndexOf((byte)'.');

            int labelLength = dotIndex < 0 ? hostname.Length : dotIndex;

            if (iri)
            {
                ReadOnlySpan<byte> label = hostname.Slice(0, labelLength);
                if (!Ascii.IsValid(label))
                {
                    // Account for the ACE prefix ("xn--")
                    labelLength += 4;

                    foreach (char c in label)
                    {
                        if (c > 0xFF)
                        {
                            // counts for two octets
                            labelLength++;
                        }
                    }
                }
            }

            if (!IsInInclusiveRange((uint)labelLength, 1, 63))
            {
                return false;
            }

            if (dotIndex < 0)
            {
                // We validated the last label
                return true;
            }

            hostname = hostname.Slice(dotIndex + 1);

            if (hostname.IsEmpty)
            {
                // Hostname ended with a dot
                return true;
            }
        }
    }

#if NET

    private static int IndexOfAny(ReadOnlySpan<byte> hostname, SearchValues<char> s_iriInvalidChars)
    {
        for (int i = 0; i < hostname.Length;)
        {
            Rune.DecodeFromUtf8(hostname.Slice(i), out Rune result, out int bytesConsumed);
            if (s_iriInvalidChars.Contains((char)result.Value))
            {
                return i; // Return the start index of the invalid character
            }

            i += bytesConsumed;
        }

        return -1;
    }

#else
    private static int IndexOfAny(ReadOnlySpan<byte> hostname, ReadOnlySpan<char> s_iriInvalidChars)
    {
        for (int i = 0; i < hostname.Length;)
        {
            Rune.DecodeFromUtf8(hostname.Slice(i), out Rune result, out int bytesConsumed);
            if (s_iriInvalidChars.IndexOf((char)result.Value) >= 0)
            {
                return i; // Return the start index of the invalid character
            }

            i += bytesConsumed;
        }

        return -1;
    }
#endif

    private static int IndexOfIriDot(ReadOnlySpan<byte> hostname)
    {
        for (int i = 0; i < hostname.Length;)
        {
            if (hostname[i] == (byte)'.')
            {
                return i;
            }

            if (Utf8UriTools.IsAscii((char)hostname[i]))
            {
                i++;
                continue;
            }

            Rune.DecodeFromUtf8(hostname.Slice(i), out Rune result, out int bytesConsumed);
            if (Globalization.IdnMapping.IsDot((char)result.Value))
            {
                return i;
            }

            i += bytesConsumed;
        }

        return -1;
    }

    private static bool IsInInclusiveRange(uint value, uint min, uint max)
            => (value - min) <= (max - min);
}
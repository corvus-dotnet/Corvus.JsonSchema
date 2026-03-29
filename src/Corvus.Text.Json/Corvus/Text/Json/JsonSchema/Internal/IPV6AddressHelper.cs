// <copyright file="IPV6AddressHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for validating and parsing IPv6 addresses.
/// </summary>
internal static partial class IPv6AddressHelper
{
    /// <summary>
    /// Determines whether a name is a valid IPv6 address enclosed in brackets.
    /// </summary>
    /// <param name="name">Pointer to the string containing possible IPv6 address.</param>
    /// <param name="start">Starting index to check from.</param>
    /// <param name="end">Ending index to check to.</param>
    /// <param name="disallowScope">Whether to disallow scope identifiers.</param>
    /// <returns><see langword="true"/> if the name contains a valid IPv6 address; otherwise, <see langword="false"/>.</returns>
    internal static unsafe bool IsValid(byte* name, int start, int end, bool disallowScope)
    {
        if (name[start] != '[')
        {
            return false;
        }

        end = new ReadOnlySpan<byte>(name + start, end - start).IndexOf((byte)']');
        if (end <= 0)
        {
            return false;
        }

        return IsValidStrict(name, start, end + start + 1, disallowScope);
    }

    /// <summary>
    /// Determines whether a name is a valid IPv6 address with strict validation rules.
    /// </summary>
    /// <param name="name">Pointer to the IPv6 address in string format.</param>
    /// <param name="start">Starting index (must be next to '[' position).</param>
    /// <param name="end">Ending index (the correct name is terminated by ']' character).</param>
    /// <param name="disallowScope">Whether to disallow scope identifiers.</param>
    /// <returns><see langword="true"/> if the name is a valid IPv6 address; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// MUST NOT be used unless all input indexes are verified and trusted.
    /// Start must be next to '[' position, or error is reported.
    /// Rules:
    /// - 8 groups of 16-bit hex numbers, separated by ':'
    /// - a single run of zeros can be compressed using the symbol '::'
    /// - an optional string of a ScopeID delimited by '%'
    /// - the last 32 bits in an address can be represented as an IPv4 address
    /// Difference between IsValid() and IsValidStrict() is that IsValid() expects part of the string to
    /// be ipv6 address where as IsValidStrict() expects strict ipv6 address.
    /// </remarks>
    internal static unsafe bool IsValidStrict(byte* name, int start, int end, bool disallowScope)
    {
        // Number of components in this IPv6 address
        int sequenceCount = 0;

        // Length of the component currently being constructed
        int sequenceLength = 0;
        bool haveCompressor = false;
        bool haveIPv4Address = false;
        bool expectingNumber = true;

        // Start position of the previous component
        int lastSequence = 1;

        bool needsClosingBracket = false;

        // An IPv6 address may begin with a start character ('['). If it does, it must end with an end
        // character (']').
        if (start < end && name[start] == (byte)'[')
        {
            start++;
            needsClosingBracket = true;

            // IsValidStrict() is only called if there is a ':' in the name string, i.e.
            // it is a possible IPv6 address. So, if the string starts with a '[' and
            // the pointer is advanced here there are still more characters to parse.
            Debug.Assert(start < end);
        }

        // Starting with a colon character is only valid if another colon follows.
        if (name[start] == (byte)':' && (start + 1 >= end || name[start + 1] != (byte)':'))
        {
            return false;
        }

        int i;
        for (i = start; i < end; ++i)
        {
            int currentCh = name[i];

            if (HexConverter.IsHexChar(currentCh))
            {
                ++sequenceLength;
                expectingNumber = false;
            }
            else
            {
                if (sequenceLength > 4)
                {
                    return false;
                }

                if (sequenceLength != 0)
                {
                    ++sequenceCount;
                    lastSequence = i - sequenceLength;
                    sequenceLength = 0;
                }

                switch (currentCh)
                {
                    case '%':
                        if (disallowScope)
                        {
                            return false;
                        }

                        // An IPv6 address is separated from its scope by a '%' character. The scope
                        // is terminated by the natural end of the address, the address end character (']')
                        // or the start of the prefix ('/').
                        while (i + 1 < end)
                        {
                            i++;
                            if (name[i] == (byte)']')
                            {
                                goto case ']';
                            }
                            else if (name[i] == (byte)'/')
                            {
                                goto case '/';
                            }
                        }

                        break;

                    case ']':
                        if (!needsClosingBracket)
                        {
                            return false;
                        }

                        needsClosingBracket = false;

                        // If there's more after the closing bracket, it must be a port.
                        // We don't use the port, but we still validate it.
                        if (i + 1 < end && name[i + 1] != (byte)':')
                        {
                            return false;
                        }

                        // If there is a port, it must either be a hexadecimal or decimal number.
                        // If the next two characters are '0x' then it's a hexadecimal number. Skip the prefix.
                        if (i + 3 < end && name[i + 2] == (byte)'0' && name[i + 3] == (byte)'x')
                        {
                            i += 4;
                            for (; i < end; i++)
                            {
                                int ch = name[i];

                                if (!HexConverter.IsHexChar(ch))
                                {
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            i += 2;
                            for (; i < end; i++)
                            {
                                if (!IsAsciiDigit((char)name[i]))
                                {
                                    return false;
                                }
                            }
                        }

                        continue;

                    case ':':

                        // If the next character after a colon is another colon, the address contains a compressor ('::').
                        if ((i > 0) && (name[i - 1] == (byte)':'))
                        {
                            if (haveCompressor)
                            {
                                // can only have one per IPv6 address
                                return false;
                            }

                            haveCompressor = true;
                            expectingNumber = false;
                        }
                        else
                        {
                            expectingNumber = true;
                        }

                        break;

                    case '/':

                        // A prefix in an IPv6 address is invalid.
                        return false;

                    case '.':
                        if (haveIPv4Address)
                        {
                            return false;
                        }

                        i = end;
                        if (!IPv4AddressHelper.IsValid(name, lastSequence, ref i, true, false, false))
                        {
                            return false;
                        }

                        // An IPv4 address takes 2 slots in an IPv6 address. One was just counted meeting the '.'
                        ++sequenceCount;
                        lastSequence = i - sequenceLength;
                        sequenceLength = 0;
                        haveIPv4Address = true;
                        --i;            // it will be incremented back on the next loop
                        break;

                    default:
                        return false;
                }

                sequenceLength = 0;
            }
        }

        if (sequenceLength != 0)
        {
            if (sequenceLength > 4)
            {
                return false;
            }

            ++sequenceCount;
        }

        // These sequence counts are -1 because it is implied in end-of-sequence.
        const int ExpectedSequenceCount = 8;
        return
            !expectingNumber &&
            (haveCompressor ? (sequenceCount < ExpectedSequenceCount) : (sequenceCount == ExpectedSequenceCount)) &&
            !needsClosingBracket;
    }

    /// <summary>
    /// Determines whether the specified character is an ASCII digit (0-9).
    /// </summary>
    /// <param name="v">The character to check.</param>
    /// <returns><see langword="true"/> if the character is an ASCII digit; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(char v)
    {
#if NET
        return char.IsAsciiDigit(v);
#else
        return IsBetween(v, '0', '9');
#endif
    }

#if !NET
    /// <summary>Indicates whether a character is within the specified inclusive range.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <param name="minInclusive">The lower bound, inclusive.</param>
    /// <param name="maxInclusive">The upper bound, inclusive.</param>
    /// <returns>true if <paramref name="c"/> is within the specified range; otherwise, false.</returns>
    /// <remarks>
    /// The method does not validate that <paramref name="maxInclusive"/> is greater than or equal
    /// to <paramref name="minInclusive"/>.  If <paramref name="maxInclusive"/> is less than
    /// <paramref name="minInclusive"/>, the behavior is undefined.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBetween(char c, char minInclusive, char maxInclusive) =>
        (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
#endif
}
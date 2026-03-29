// <copyright file="IPV4AddressHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for validating and parsing IPv4 addresses.
/// </summary>
internal static partial class IPv4AddressHelper
{
    /// <summary>
    /// Represents an invalid IPv4 address parsing result.
    /// </summary>
    internal const long Invalid = -1;

    private const int Decimal = 10;

    private const int Hex = 16;

    private const long MaxIPv4Value = uint.MaxValue; // the native parser cannot handle MaxIPv4Value, only MaxIPv4Value - 1

    private const int NumberOfLabels = 4;

    private const int Octal = 8;

    /// <summary>
    /// Performs validation on a substring to determine if it contains a valid IPv4 address.
    /// Updates the end index to where the IPv4 address ends.
    /// </summary>
    /// <param name="name">Pointer to the string containing possible IPv4 address.</param>
    /// <param name="start">Offset to start checking for IPv4 address.</param>
    /// <param name="end">On input, index of the last character to check; on output, updated to the last character checked.</param>
    /// <param name="allowIPv6">Enables parsing IPv4 addresses embedded in IPv6 address literals.</param>
    /// <param name="notImplicitFile">Do not consider this URI holding an implicit filename.</param>
    /// <param name="unknownScheme">The check is made on an unknown scheme (suppress IPv4 canonicalization).</param>
    /// <returns><see langword="true"/> if the substring contains a valid IPv4 address; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// MUST NOT be used unless all input indexes are verified and trusted.
    /// The address string is terminated by either end of the string, characters ':' '/' '\' '?'
    /// IPv6 can only have canonical IPv4 embedded. Unknown schemes will not attempt parsing of non-canonical IPv4 addresses.
    /// </remarks>
    internal static unsafe bool IsValid(byte* name, int start, ref int end, bool allowIPv6, bool notImplicitFile, bool unknownScheme)
    {
        // IPv6 can only have canonical IPv4 embedded. Unknown schemes will not attempt parsing of non-canonical IPv4 addresses.
        if (allowIPv6 || !unknownScheme)
        {
            return IsValidCanonical(name, start, ref end, allowIPv6, notImplicitFile);
        }
        else
        {
            return ParseNonCanonical(name, start, ref end, notImplicitFile, requireCanonical: true) != Invalid;
        }
    }

    /// <summary>
    /// Checks if the substring is a valid canonical IPv4 address or an IPv4 address embedded in an IPv6 literal.
    /// This method validates against RFC3986 ABNF productions from Section 3.2.2.
    /// </summary>
    /// <param name="name">Pointer to the string containing possible IPv4 address.</param>
    /// <param name="start">Offset to start checking for IPv4 address.</param>
    /// <param name="end">On input, index of the last character to check; on output, updated to the last character checked.</param>
    /// <param name="allowIPv6">Enables parsing IPv4 addresses embedded in IPv6 address literals.</param>
    /// <param name="notImplicitFile">Do not consider this URI holding an implicit filename.</param>
    /// <returns><see langword="true"/> if the substring contains a valid canonical IPv4 address; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Parses ABNF productions from RFC3986, Section 3.2.2:
    /// IP-literal = "[" ( IPv6address / IPvFuture  ) "]"
    /// IPv4address = dec-octet "." dec-octet "." dec-octet "." dec-octet
    /// dec-octet   = DIGIT; 0-9
    /// / %x31-39 DIGIT; 10-99
    /// / "1" 2DIGIT; 100-199
    /// / "2" %x30-34 DIGIT; 200-249
    /// / "25" %x30-35; 250-255
    /// </remarks>
    internal static unsafe bool IsValidCanonical(byte* name, int start, ref int end, bool allowIPv6, bool notImplicitFile)
    {
        int dots = 0;
        long number = 0;
        bool haveNumber = false;
        bool firstCharIsZero = false;

        while (start < end)
        {
            int ch = name[start];

            if (allowIPv6)
            {
                // For an IPv4 address nested inside an IPv6 address, the terminator is either the IPv6 address terminator (']'), prefix ('/') or ScopeId ('%')
                if (ch == ']' || ch == '/' || ch == '%')
                {
                    break;
                }
            }
            else if (ch == '/' || ch == '\\' || (notImplicitFile && (ch == ':' || ch == '?' || ch == '#')))
            {
                // For a normal IPv4 address, the terminator is the prefix ('/' or its counterpart, '\'). If notImplicitFile is set, the terminator
                // is one of the characters which signify the start of the rest of the URI - the port number (':'), query string ('?') or fragment ('#')
                break;
            }

            // An explicit cast to an unsigned integer forces character values preceding '0' to underflow, eliminating one comparison below.
            uint parsedCharacter = (uint)(ch - '0');

            if (parsedCharacter < IPv4AddressHelper.Decimal)
            {
                // A number starting with zero should be interpreted in base 8 / octal
                if (!haveNumber && parsedCharacter == 0)
                {
                    if ((start + 1 < end) && name[start + 1] == (byte)'0')
                    {
                        // 00 is not allowed as a prefix.
                        return false;
                    }

                    firstCharIsZero = true;
                }

                haveNumber = true;
                number = number * IPv4AddressHelper.Decimal + parsedCharacter;
                if (number > byte.MaxValue)
                {
                    return false;
                }
            }
            else if (ch == '.')
            {
                // If the current character is not an integer, it may be the IPv4 component separator ('.')
                if (!haveNumber || (number > 0 && firstCharIsZero))
                {
                    // 0 is not allowed to prefix a number.
                    return false;
                }

                ++dots;
                haveNumber = false;
                number = 0;
                firstCharIsZero = false;
            }
            else
            {
                return false;
            }

            ++start;
        }

        bool res = (dots == 3) && haveNumber;
        if (res)
        {
            end = start;
        }

        return res;
    }

    /// <summary>
    /// Parse any canonical or non-canonical IPv4 formats and return a numeric value between 0 and MaxIPv4Value.
    /// </summary>
    /// <param name="name">Pointer to the string containing possible IPv4 address.</param>
    /// <param name="start">Offset to start parsing from.</param>
    /// <param name="end">On input, index of the last character to check; on output, updated to the last character parsed.</param>
    /// <param name="notImplicitFile">Do not consider this URI holding an implicit filename.</param>
    /// <param name="requireCanonical">Whether to require canonical format.</param>
    /// <returns>A long value between 0 and MaxIPv4Value, or <see cref="Invalid"/> (-1) for failures.</returns>
    /// <remarks>
    /// If the address has less than three dots, and we allow non-canonical form, then only the rightmost section
    /// is assumed to contain the combined value for the missing sections: 0xFF00FFFF == 0xFF.0x00.0xFF.0xFF == 0xFF.0xFFFF
    /// </remarks>
    internal static unsafe long ParseNonCanonical(byte* name, int start, ref int end, bool notImplicitFile, bool requireCanonical)
    {
        int numberBase = IPv4AddressHelper.Decimal;
        int ch = 0;
        Span<long> parts = stackalloc long[3]; // One part per octet. Final octet doesn't have a terminator, so is stored in currentValue.
        long currentValue = 0;
        bool atLeastOneChar = false;

        // Parse one dotted section at a time
        int dotCount = 0; // Limit 3
        int current = start;

        for (; current < end; current++)
        {
            ch = name[current];
            currentValue = 0;

            // Figure out what base this section is in, default to base 10.
            // A number starting with zero should be interpreted in base 8 / octal
            // If the number starts with 0x, it should be interpreted in base 16 / hex
            numberBase = IPv4AddressHelper.Decimal;

            if (ch == '0')
            {
                current++;
                atLeastOneChar = true;
                if (current < end)
                {
                    ch = name[current];

                    if (ch == 'x' || ch == 'X')
                    {
                        numberBase = IPv4AddressHelper.Hex;

                        current++;
                        atLeastOneChar = false;
                    }
                    else
                    {
                        numberBase = IPv4AddressHelper.Octal;
                    }
                }
            }

            // Parse this section
            for (; current < end; current++)
            {
                ch = name[current];
                int digitValue = HexConverter.FromChar(ch);

                if (digitValue >= numberBase)
                {
                    break; // Invalid/terminator
                }

                currentValue = (currentValue * numberBase) + digitValue;

                if (currentValue > MaxIPv4Value) // Overflow
                {
                    return Invalid;
                }

                atLeastOneChar = true;
            }

            if (current < end && ch == '.')
            {
                if (dotCount >= 3 // Max of 3 dots and 4 segments
                    || !atLeastOneChar // No empty segments: 1...1

                    // Only the last segment can be more than 255 (if there are less than 3 dots)
                    || currentValue > 0xFF)
                {
                    return Invalid;
                }

                parts[dotCount] = currentValue;
                dotCount++;
                atLeastOneChar = false;
                continue;
            }

            // We don't get here unless we find an invalid character or a terminator
            break;
        }

        // Terminators
        if (!atLeastOneChar)
        {
            return Invalid;  // Empty trailing segment: 1.1.1.
        }
        else if (current >= end)
        {
            // end of string, allowed
        }
        else if (ch == '/' || ch == '\\' || (notImplicitFile && (ch == ':' || ch == '?' || ch == '#')))
        {
            // For a normal IPv4 address, the terminator is the prefix ('/' or its counterpart, '\'). If notImplicitFile is set, the terminator
            // is one of the characters which signify the start of the rest of the URI - the port number (':'), query string ('?') or fragment ('#')
            end = current;
        }
        else
        {
            // not a valid terminating character
            return Invalid;
        }

        // Parsed, reassemble and check for overflows in the last part. Previous parts have already been checked in the loop
        switch (dotCount)
        {
            case 0: // 0xFFFFFFFF
                return requireCanonical ? Invalid : currentValue;

            case 1: // 0xFF.0xFFFFFF
                Debug.Assert(parts[0] <= 0xFF);
                if (currentValue > 0xffffff || requireCanonical)
                {
                    return Invalid;
                }

                return (parts[0] << 24) | currentValue;

            case 2: // 0xFF.0xFF.0xFFFF
                Debug.Assert(parts[0] <= 0xFF);
                Debug.Assert(parts[1] <= 0xFF);
                if (currentValue > 0xffff || requireCanonical)
                {
                    return Invalid;
                }

                return (parts[0] << 24) | (parts[1] << 16) | currentValue;

            case 3: // 0xFF.0xFF.0xFF.0xFF
                Debug.Assert(parts[0] <= 0xFF);
                Debug.Assert(parts[1] <= 0xFF);
                Debug.Assert(parts[2] <= 0xFF);
                if (currentValue > 0xff)
                {
                    return Invalid;
                }

                return (parts[0] << 24) | (parts[1] << 16) | (parts[2] << 8) | currentValue;

            default:
                return Invalid;
        }
    }
}
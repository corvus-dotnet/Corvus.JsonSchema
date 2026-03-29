// <copyright file="Utf8UriTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Corvus.Text.Json.Internal;

#pragma warning disable RCS1243 // Duplicate word in a comment - ABNF grammar
/// <summary>
/// Provides validation functionality for URI Templates as defined in RFC 6570.
///
/// This implementation follows the complete ABNF grammar from RFC 6570:
///
/// URI-Template = *( literals / expression )
/// literals     = %x21 / %x23-24 / %x26 / %x28-3B / %x3D / %x3F-5B
/// / %x5D / %x5F / %x61-7A / %x7E / ucschar / pct-encoded
/// expression   = "{" [ operator ] variable-list "}"
/// operator     = op-level2 / op-level3 / op-reserve
/// op-level2    = "+" / "#"
/// op-level3    = "." / "/" / ";" / "?" / "&"
/// op-reserve   = "=" / "," / "!" / "@" / "|"
/// variable-list = varspec *( "," varspec )
/// varspec      = varname [ modifier ]
/// varname      = varchar *( ["."] varchar )
/// varchar      = ( ALPHA / DIGIT / "_" / pct-encoded ) *( ALPHA / DIGIT / "_" / "." / pct-encoded )
/// modifier     = prefix / explode
/// prefix       = ":" max-length
/// explode      = "*"
/// max-length   = %x31-39 0*3DIGIT; positive integer up to 9999
/// pct-encoded  = "%" HEXDIG HEXDIG
/// </summary>
#pragma warning restore RCS1243
internal static class Utf8UriTemplate
{
    // Lookup tables for character classification (Tier 1 optimization)
    // Pre-computed at compile time for maximum performance
    // RFC 6570 operators: op-level2 / op-level3 (NOT op-reserve)
    // op-level2 = "+" / "#" (indices 43, 35)
    // op-level3 = "." / "/" / ";" / "?" / "&" (indices 46, 47, 59, 63, 38)
    private static readonly bool[] IsOperatorLookup = {
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 0-15
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 16-31
        false, false, false, true,  false, false, true,  false, false, false, false, true,  false, false, true,  true,  // 32-47  ('#'=35, '&'=38, '+'=43, '.'=46, '/'=47)
        false, false, false, false, false, false, false, false, false, false, false, true,  false, false, false, true,  // 48-63  (';'=59, '?'=63)
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 64-79
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 80-95
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 96-111
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 112-127
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 128-143
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 144-159
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 160-175
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 176-191
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 192-207
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 208-223
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 224-239
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false  // 240-255
    };

    // literals = %x21 / %x23-24 / %x26 / %x28-3B / %x3D / %x3F-5B / %x5D / %x5F / %x61-7A / %x7E / ucschar / pct-encoded
    // RFC 6570 explicitly excludes whitespace characters
    private static readonly bool[] IsLiteralLookup = {
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 0-15
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 16-31
        false, true,  false, true,  true,  false, true,  false, true,  true,  true,  true,  true,  true,  true,  true,  // 32-47  ('!'=33, '#'=35, '$'=36, '&'=38, '('=40, ')'=41, '*'=42, '+'=43, ','=44, '-'=45, '.'=46, '/'=47)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  false, true,  false, true,  // 48-63  ('0'-'9'=48-57, ':'=58, ';'=59, '='=61, '?'=63)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 64-79  ('@'=64, 'A'-'O'=65-79)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  false, true,  false, true,  // 80-95  ('P'-'Z'=80-90, '['=91, ']'=93, '_'=95)
        false, true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 96-111 ('a'-'o'=97-111)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  false, false, false, true,  false, // 112-127 ('p'-'z'=112-122, '~'=126)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 128-143 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 144-159 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 160-175 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 176-191 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 192-207 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 208-223 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  // 224-239 (Unicode)
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true   // 240-255 (Unicode)
    };

    // HEXDIG = DIGIT / %x41-46 / %x61-66 (0-9, A-F, a-f)
    private static readonly bool[] IsHexDigitLookup = {
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 0-15
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 16-31
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 32-47
        true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  false, false, false, false, false, false, // 48-63  ('0'-'9'=48-57)
        false, true,  true,  true,  true,  true,  true,  false, false, false, false, false, false, false, false, false, // 64-79  ('A'-'F'=65-70)
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 80-95
        false, true,  true,  true,  true,  true,  true,  false, false, false, false, false, false, false, false, false, // 96-111 ('a'-'f'=97-102)
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 112-127
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 128-143
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 144-159
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 160-175
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 176-191
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 192-207
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 208-223
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 224-239
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false  // 240-255
    };

    /// <summary>
    /// Validates whether the specified byte span represents a valid URI template.
    /// Implements: URI-Template = *( literals / expression )
    /// </summary>
    /// <param name="uriTemplate">The URI template to validate.</param>
    /// <returns><see langword="true"/> if the URI template is valid; otherwise, <see langword="false"/>.</returns>
    public static bool Validate(ReadOnlySpan<byte> uriTemplate)
    {
        if (uriTemplate.IsEmpty)
            return true;

        int position = 0;

        // URI-Template = *( literals / expression )
        while (position < uriTemplate.Length)
        {
            if (uriTemplate[position] == (byte)'{')
            {
                // Parse expression
                if (!ParseExpression(uriTemplate, ref position))
                    return false;
            }
            else
            {
                // Parse literals
                if (!ParseLiterals(uriTemplate, ref position))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses expression = "{" [ operator ] variable-list "}"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseExpression(ReadOnlySpan<byte> input, ref int position)
    {
        if (input[position] != (byte)'{')
            return false;

        position++; // Skip '{'

        int expressionStart = position;

        // Optional operator
        if (position < input.Length)
        {
            byte op = input[position];
            if (IsOperatorLookup[op])
            {
                position++;
            }
        }

        // variable-list = varspec *( "," varspec )
        if (!ParseVariableList(input, ref position))
            return false;

        // Must have at least one character in expression
        if (position == expressionStart)
            return false;

        // Closing brace
        if (position >= input.Length || input[position] != (byte)'}')
            return false;

        position++; // Skip '}'
        return true;
    }

    /// <summary>
    /// Parses literals = %x21 / %x23-24 / %x26 / %x28-3B / %x3D / %x3F-5B
    /// / %x5D / %x5F / %x61-7A / %x7E / ucschar / pct-encoded
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseLiterals(ReadOnlySpan<byte> input, ref int position)
    {
        if (position >= input.Length)
            return false;

        // Parse at least one literal character
        if (!ParseLiteralChar(input, ref position))
            return false;

        // Parse remaining literal characters until we hit '{' or end
        while (position < input.Length && input[position] != (byte)'{')
        {
            if (!ParseLiteralChar(input, ref position))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a single literal character according to RFC 6570 literals ABNF
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseLiteralChar(ReadOnlySpan<byte> input, ref int position)
    {
        byte b = input[position];

        // Check for percent-encoded sequence first
        if (b == (byte)'%')
        {
            return ParsePctEncoded(input, ref position);
        }

        // Use lookup table for fast ASCII literal validation with bounds checking
        if (IsLiteralLookup[b])
        {
            position++;
            return true;
        }

        // Handle Unicode (ucschar)
        if (b > 0x7F)
        {
            return ParseUcsChar(input, ref position);
        }

        return false;
    }

    /// <summary>
    /// Parses variable-list = varspec *( "," varspec )
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseVariableList(ReadOnlySpan<byte> input, ref int position)
    {
        // Parse first varspec
        if (!ParseVarSpec(input, ref position))
            return false;

        // Parse additional varspecs separated by commas
        while (position < input.Length && input[position] == (byte)',')
        {
            position++; // Skip comma
            if (!ParseVarSpec(input, ref position))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses varspec = varname [ modifier ]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseVarSpec(ReadOnlySpan<byte> input, ref int position)
    {
        // Parse varname
        if (!ParseVarName(input, ref position))
            return false;

        // Optional modifier
        if (position < input.Length)
        {
            byte b = input[position];
            if (b == (byte)':')
            {
                // prefix = ":" max-length
                position++;
                return ParseMaxLength(input, ref position);
            }
            else if (b == (byte)'*')
            {
                // explode = "*"
                position++;
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses varname = varchar *( ["."] varchar )
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseVarName(ReadOnlySpan<byte> input, ref int position)
    {
        // Parse first varchar - this is required
        if (!ParseVarChar(input, ref position))
            return false;

        // Parse additional varchar segments with dots: *( ["."] varchar )
        while (position < input.Length && input[position] == (byte)'.')
        {
            int dotPosition = position;
            position++; // Skip the dot

            // Must have varchar after dot
            if (!ParseVarChar(input, ref position))
            {
                // Backtrack - the dot wasn't followed by valid varchar
                position = dotPosition;
                break;
            }

            // Continue to look for more dot-varchar patterns
        }

        return true;
    }

    /// <summary>
    /// Parses varchar = ( ALPHA / DIGIT / "_" / pct-encoded ) *( ALPHA / DIGIT / "_" / pct-encoded )
    /// Follows RFC 6570 ABNF exactly - parses ONE varchar segment only
    /// NOTE: Dots are handled at the varname level, not varchar level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseVarChar(ReadOnlySpan<byte> input, ref int position)
    {
        if (position >= input.Length)
            return false;

        byte b = input[position];

        // First character: ALPHA / DIGIT / "_" / pct-encoded (per RFC 6570)
        if (IsAlpha(b) || IsDigit(b) || b == (byte)'_')
        {
            position++;
        }
        else if (b == (byte)'%')
        {
            if (!ParsePctEncoded(input, ref position))
                return false;
        }
        else
        {
            return false; // Invalid first character
        }

        // Subsequent characters: ALPHA / DIGIT / "_" / pct-encoded
        // NOTE: This should NOT include dots - dots are handled in ParseVarName
        while (position < input.Length)
        {
            b = input[position];

            if (IsAlpha(b) || IsDigit(b) || b == (byte)'_')
            {
                position++;
            }
            else if (b == (byte)'%')
            {
                if (!ParsePctEncoded(input, ref position))
                    return false;
            }
            else
            {
                // End of this varchar segment (could be '.', ',', '}', ':', '*', etc.)
                break;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses max-length = %x31-39 0*3DIGIT; positive integer up to 9999
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseMaxLength(ReadOnlySpan<byte> input, ref int position)
    {
        if (position >= input.Length)
            return false;

        // First digit must be 1-9 (%x31-39)
        byte first = input[position];
        if (first < (byte)'1' || first > (byte)'9')
            return false;

        position++;

        // Up to 3 additional digits (0*3DIGIT)
        for (int digitCount = 1; position < input.Length && IsDigit(input[position]) && digitCount < 4; digitCount++)
        {
            position++;
        }

        return true;
    }

#pragma warning disable RCS1243 // Duplicate word in a comment - ABNF grammar

    /// <summary>
    /// Parses pct-encoded = "%" HEXDIG HEXDIG.
    /// </summary>
#pragma warning restore RCS1243
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParsePctEncoded(ReadOnlySpan<byte> input, ref int position)
    {
        if (position + 2 >= input.Length)
            return false;

        if (input[position] != (byte)'%')
            return false;

        // Add bounds checking for lookup table access
        byte hex1 = input[position + 1];
        byte hex2 = input[position + 2];

        if (!IsHexDigitLookup[hex1] || !IsHexDigitLookup[hex2])
            return false;

        position += 3;
        return true;
    }

    /// <summary>
    /// Parses ucschar according to RFC 6570
    /// ucschar = %xA0-D7FF / %xF900-FDCF / %xFDF0-FFEF
    /// / %x10000-1FFFD / %x20000-2FFFD / %x30000-3FFFD
    /// / %x40000-4FFFD / %x50000-5FFFD / %x60000-6FFFD
    /// / %x70000-7FFFD / %x80000-8FFFD / %x90000-9FFFD
    /// / %xA0000-AFFFD / %xB0000-BFFFD / %xC0000-CFFFD
    /// / %xD0000-DFFFD / %xE1000-EFFFD
    /// </summary>
    private static bool ParseUcsChar(ReadOnlySpan<byte> input, ref int position)
    {
        // Use Rune to decode UTF-8 properly
        OperationStatus status = Rune.DecodeFromUtf8(input.Slice(position), out Rune rune, out int bytesConsumed);

        if (status != OperationStatus.Done)
            return false;

        uint codePoint = (uint)rune.Value;

        // Check if the code point is in the valid UCS character ranges per RFC 6570
        bool isValid = (codePoint >= 0xA0 && codePoint <= 0xD7FF) ||
                       (codePoint >= 0xF900 && codePoint <= 0xFDCF) ||
                       (codePoint >= 0xFDF0 && codePoint <= 0xFFEF) ||
                       (codePoint >= 0x10000 && codePoint <= 0x1FFFD) ||
                       (codePoint >= 0x20000 && codePoint <= 0x2FFFD) ||
                       (codePoint >= 0x30000 && codePoint <= 0x3FFFD) ||
                       (codePoint >= 0x40000 && codePoint <= 0x4FFFD) ||
                       (codePoint >= 0x50000 && codePoint <= 0x5FFFD) ||
                       (codePoint >= 0x60000 && codePoint <= 0x6FFFD) ||
                       (codePoint >= 0x70000 && codePoint <= 0x7FFFD) ||
                       (codePoint >= 0x80000 && codePoint <= 0x8FFFD) ||
                       (codePoint >= 0x90000 && codePoint <= 0x9FFFD) ||
                       (codePoint >= 0xA0000 && codePoint <= 0xAFFFD) ||
                       (codePoint >= 0xB0000 && codePoint <= 0xBFFFD) ||
                       (codePoint >= 0xC0000 && codePoint <= 0xCFFFD) ||
                       (codePoint >= 0xD0000 && codePoint <= 0xDFFFD) ||
                       (codePoint >= 0xE1000 && codePoint <= 0xEFFFD);

        if (isValid)
        {
            position += bytesConsumed;
            return true;
        }

        return false;
    }

    // Character classification helpers with aggressive inlining
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlpha(byte b) => (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';
}
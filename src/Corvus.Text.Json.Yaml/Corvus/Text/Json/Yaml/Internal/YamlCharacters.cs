// <copyright file="YamlCharacters.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

/// <summary>
/// YAML character classification utilities operating on UTF-8 bytes.
/// </summary>
internal static class YamlCharacters
{
    /// <summary>
    /// Determines whether the byte is a YAML whitespace character (space or tab).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhitespace(byte c)
        => c == YamlConstants.Space || c == YamlConstants.Tab;

    /// <summary>
    /// Determines whether the byte is a YAML line break character (LF or CR).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLineBreak(byte c)
        => c == YamlConstants.LineFeed || c == YamlConstants.CarriageReturn;

    /// <summary>
    /// Determines whether the byte is whitespace or a line break.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhitespaceOrLineBreak(byte c)
        => IsWhitespace(c) || IsLineBreak(c);

    /// <summary>
    /// Determines whether the byte is a YAML flow indicator character: { } [ ] , .
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFlowIndicator(byte c)
        => c == YamlConstants.OpenBrace
        || c == YamlConstants.CloseBrace
        || c == YamlConstants.OpenBracket
        || c == YamlConstants.CloseBracket
        || c == YamlConstants.Comma;

    /// <summary>
    /// Determines whether the byte is an ASCII digit (0-9).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDigit(byte c)
        => (uint)(c - YamlConstants.Zero) <= 9;

    /// <summary>
    /// Determines whether the byte is an ASCII hex digit (0-9, a-f, A-F).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(byte c)
        => IsDigit(c) || (uint)(c - (byte)'a') <= 5 || (uint)(c - (byte)'A') <= 5;

    /// <summary>
    /// Determines whether the byte is an ASCII octal digit (0-7).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOctalDigit(byte c)
        => (uint)(c - YamlConstants.Zero) <= 7;

    /// <summary>
    /// Determines whether the byte is an ASCII letter (a-z, A-Z).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLetter(byte c)
        => (uint)(c - (byte)'a') <= 25 || (uint)(c - (byte)'A') <= 25;

    /// <summary>
    /// Determines whether the byte starts a YAML indicator that cannot begin a plain scalar.
    /// In block context, plain scalars cannot start with these characters.
    /// </summary>
    /// <remarks>
    /// The characters are: - ? : , [ ] { } # &amp; * ! | &gt; ' " % @ `
    /// Some of these (- ? :) are only indicators when followed by whitespace.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIndicator(byte c)
    {
        return c switch
        {
            YamlConstants.Dash or
            YamlConstants.QuestionMark or
            YamlConstants.Colon or
            YamlConstants.Comma or
            YamlConstants.OpenBracket or
            YamlConstants.CloseBracket or
            YamlConstants.OpenBrace or
            YamlConstants.CloseBrace or
            YamlConstants.Hash or
            YamlConstants.Ampersand or
            YamlConstants.Asterisk or
            YamlConstants.Exclamation or
            YamlConstants.Pipe or
            YamlConstants.GreaterThan or
            YamlConstants.SingleQuote or
            YamlConstants.DoubleQuote or
            YamlConstants.Percent or
            YamlConstants.At or
            YamlConstants.Backtick => true,
            _ => false,
        };
    }

    /// <summary>
    /// Checks if the given position in the buffer is a valid mapping value indicator
    /// (: followed by whitespace, line break, or end of input).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMappingValueIndicator(ReadOnlySpan<byte> buffer, int pos)
    {
        if (pos >= buffer.Length || buffer[pos] != YamlConstants.Colon)
        {
            return false;
        }

        int next = pos + 1;
        return next >= buffer.Length || IsWhitespaceOrLineBreak(buffer[next]);
    }

    /// <summary>
    /// Checks if the given position in the buffer is a valid block sequence entry indicator
    /// (- followed by whitespace or line break).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBlockSequenceIndicator(ReadOnlySpan<byte> buffer, int pos)
    {
        if (pos >= buffer.Length || buffer[pos] != YamlConstants.Dash)
        {
            return false;
        }

        int next = pos + 1;
        return next >= buffer.Length || IsWhitespaceOrLineBreak(buffer[next]);
    }

    /// <summary>
    /// Checks if the given position in the buffer is a valid mapping value indicator
    /// in flow context (: followed by whitespace, line break, end of input, or flow indicator).
    /// In flow context, {a:1} is valid (no space required after :).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFlowMappingValueIndicator(ReadOnlySpan<byte> buffer, int pos)
    {
        if (pos >= buffer.Length || buffer[pos] != YamlConstants.Colon)
        {
            return false;
        }

        int next = pos + 1;
        return next >= buffer.Length
            || IsWhitespaceOrLineBreak(buffer[next])
            || IsFlowIndicator(buffer[next]);
    }

    /// <summary>
    /// Checks if the position starts a document marker (--- or ...)
    /// that appears at column 0 and is followed by whitespace, line break, or EOF.
    /// </summary>
    public static bool IsDocumentMarker(ReadOnlySpan<byte> buffer, int pos, int column, ReadOnlySpan<byte> marker)
    {
        if (column != 0)
        {
            return false;
        }

        if (pos + 3 > buffer.Length)
        {
            return false;
        }

        if (!buffer.Slice(pos, 3).SequenceEqual(marker))
        {
            return false;
        }

        // Must be followed by whitespace, line break, or EOF
        int after = pos + 3;
        return after >= buffer.Length || IsWhitespaceOrLineBreak(buffer[after]);
    }
}
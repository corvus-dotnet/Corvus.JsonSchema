// <copyright file="YamlConstants.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

/// <summary>
/// Constants used by the YAML scanner and converter.
/// </summary>
internal static class YamlConstants
{
    /// <summary>The UTF-8 byte for space (0x20).</summary>
    public const byte Space = (byte)' ';

    /// <summary>The UTF-8 byte for tab (0x09).</summary>
    public const byte Tab = (byte)'\t';

    /// <summary>The UTF-8 byte for line feed (0x0A).</summary>
    public const byte LineFeed = (byte)'\n';

    /// <summary>The UTF-8 byte for carriage return (0x0D).</summary>
    public const byte CarriageReturn = (byte)'\r';

    /// <summary>The UTF-8 byte for colon (:).</summary>
    public const byte Colon = (byte)':';

    /// <summary>The UTF-8 byte for dash (-).</summary>
    public const byte Dash = (byte)'-';

    /// <summary>The UTF-8 byte for question mark (?).</summary>
    public const byte QuestionMark = (byte)'?';

    /// <summary>The UTF-8 byte for hash (#).</summary>
    public const byte Hash = (byte)'#';

    /// <summary>The UTF-8 byte for ampersand (&amp;).</summary>
    public const byte Ampersand = (byte)'&';

    /// <summary>The UTF-8 byte for asterisk (*).</summary>
    public const byte Asterisk = (byte)'*';

    /// <summary>The UTF-8 byte for exclamation (!).</summary>
    public const byte Exclamation = (byte)'!';

    /// <summary>The UTF-8 byte for pipe (|).</summary>
    public const byte Pipe = (byte)'|';

    /// <summary>The UTF-8 byte for greater-than (&gt;).</summary>
    public const byte GreaterThan = (byte)'>';

    /// <summary>The UTF-8 byte for single quote (').</summary>
    public const byte SingleQuote = (byte)'\'';

    /// <summary>The UTF-8 byte for double quote (").</summary>
    public const byte DoubleQuote = (byte)'"';

    /// <summary>The UTF-8 byte for backslash (\).</summary>
    public const byte Backslash = (byte)'\\';

    /// <summary>The UTF-8 byte for opening brace ({).</summary>
    public const byte OpenBrace = (byte)'{';

    /// <summary>The UTF-8 byte for closing brace (}).</summary>
    public const byte CloseBrace = (byte)'}';

    /// <summary>The UTF-8 byte for opening bracket ([).</summary>
    public const byte OpenBracket = (byte)'[';

    /// <summary>The UTF-8 byte for closing bracket (]).</summary>
    public const byte CloseBracket = (byte)']';

    /// <summary>The UTF-8 byte for comma (,).</summary>
    public const byte Comma = (byte)',';

    /// <summary>The UTF-8 byte for percent (%).</summary>
    public const byte Percent = (byte)'%';

    /// <summary>The UTF-8 byte for tilde (~).</summary>
    public const byte Tilde = (byte)'~';

    /// <summary>The UTF-8 byte for period (.).</summary>
    public const byte Period = (byte)'.';

    /// <summary>The UTF-8 byte for at-sign (@).</summary>
    public const byte At = (byte)'@';

    /// <summary>The UTF-8 byte for backtick (`).</summary>
    public const byte Backtick = (byte)'`';

    /// <summary>The UTF-8 byte for zero (0).</summary>
    public const byte Zero = (byte)'0';

    /// <summary>The UTF-8 byte for nine (9).</summary>
    public const byte Nine = (byte)'9';

    /// <summary>The UTF-8 byte for plus (+).</summary>
    public const byte Plus = (byte)'+';

    /// <summary>UTF-8 BOM byte 0.</summary>
    public const byte Bom0 = 0xEF;

    /// <summary>UTF-8 BOM byte 1.</summary>
    public const byte Bom1 = 0xBB;

    /// <summary>UTF-8 BOM byte 2.</summary>
    public const byte Bom2 = 0xBF;

    /// <summary>
    /// Maximum recursion depth for nested block/flow structures.
    /// Aligns with the default depth limit in <c>ParsedJsonDocument</c>.
    /// </summary>
    public const int MaxNestingDepth = 64;

    /// <summary>
    /// The document start marker: "---".
    /// </summary>
    public static ReadOnlySpan<byte> DocumentStartMarker => "---"u8;

    /// <summary>
    /// The document end marker: "...".
    /// </summary>
    public static ReadOnlySpan<byte> DocumentEndMarker => "..."u8;
}
// <copyright file="EcmaRegexTranslator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Translates ECMAScript 262 regular expression patterns (with /u Unicode mode)
/// to equivalent .NET regular expression patterns.
/// </summary>
/// <remarks>
/// <para>
/// Key semantic differences handled:
/// <list type="bullet">
/// <item><c>\d</c>, <c>\D</c>: ECMAScript = <c>[0-9]</c>; .NET = Unicode digits.</item>
/// <item><c>\w</c>, <c>\W</c>: ECMAScript = <c>[a-zA-Z0-9_]</c>; .NET = Unicode word chars.</item>
/// <item><c>\s</c>, <c>\S</c>: ECMAScript includes <c>\uFEFF</c> but not <c>\x85</c>.</item>
/// <item><c>\b</c>, <c>\B</c>: Word boundary uses ASCII <c>\w</c> definition.</item>
/// <item><c>.</c> (dot): ECMAScript excludes <c>\n</c>, <c>\r</c>, <c>\u2028</c>, <c>\u2029</c>.</item>
/// <item><c>\u{XXXXX}</c>: Converted to <c>\uXXXX</c> or surrogate pair escapes.</item>
/// <item><c>\p{...}</c>: Unicode property names mapped to .NET equivalents.</item>
/// </list>
/// </para>
/// </remarks>
internal static class EcmaRegexTranslator
{
    private const int StackAllocThreshold = 256;

    // Standalone expansions (outside character classes)
    private const string DotExpansion = @"[^\n\r\u2028\u2029]";
    private const string DigitClass = "[0-9]";
    private const string NotDigitClass = "[^0-9]";
    private const string WordClass = "[a-zA-Z0-9_]";
    private const string NotWordClass = "[^a-zA-Z0-9_]";

    private const string WhitespaceClass =
        @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]";

    private const string NotWhitespaceClass =
        @"[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]";

    // Inline expansions (inside character classes, without brackets)
    private const string DigitInline = "0-9";
    private const string WordInline = "a-zA-Z0-9_";

    private const string WhitespaceInline =
        @"\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000";

    // Word boundary expansions
    private const string WordBoundary =
        @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))";

    private const string NonWordBoundary =
        @"(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))";

    // Matches exactly one Unicode code point (surrogate pair or non-surrogate BMP char)
    private const string AnyCodePoint =
        "(?:[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]|[^\\uD800-\\uDFFF])";

    // Latin script — explicit BMP ranges + surrogate-pair alternation for supplementary.
    // Derived from Unicode 16.0 Script=Latin property.
    private const string LatinBmp =
        "\\u0041-\\u005A\\u0061-\\u007A\\u00AA\\u00BA"
        + "\\u00C0-\\u00D6\\u00D8-\\u00F6\\u00F8-\\u02B8"
        + "\\u02E0-\\u02E4\\u1D00-\\u1D25\\u1D2C-\\u1D5C"
        + "\\u1D62-\\u1D65\\u1D6B-\\u1D77\\u1D79-\\u1DBE"
        + "\\u1E00-\\u1EFF\\u2071\\u207F\\u2090-\\u209C"
        + "\\u212A-\\u212B\\u2132\\u214E\\u2160-\\u2188"
        + "\\u2C60-\\u2C7F\\uA722-\\uA787\\uA78B-\\uA7CD"
        + "\\uA7D0-\\uA7D1\\uA7D3\\uA7D5-\\uA7DC"
        + "\\uA7F2-\\uA7FF\\uAB30-\\uAB5A\\uAB5C-\\uAB64"
        + "\\uAB66-\\uAB69\\uFB00-\\uFB06\\uFF21-\\uFF3A"
        + "\\uFF41-\\uFF5A";

    private const string LatinScript =
        "(?:["
        + LatinBmp
        + "]"
        + "|\\uD801[\\uDF80-\\uDF85]"
        + "|\\uD801[\\uDF87-\\uDFB0]"
        + "|\\uD801[\\uDFB2-\\uDFBA]"
        + "|\\uD837[\\uDF00-\\uDF1E]"
        + "|\\uD837[\\uDF25-\\uDF2A]"
        + ")";

    // Hex_Digit — all 44 code points (BMP only)
    private const string HexDigitClass =
        "0-9A-Fa-f\\uFF10-\\uFF19\\uFF21-\\uFF26\\uFF41-\\uFF46";

    // Lowercase — \p{Ll} plus Other_Lowercase code points (28 BMP + 5 supplementary ranges)
    private const string LowercaseBmp =
        "\\p{Ll}\\u00AA\\u00BA\\u02B0-\\u02B8\\u02C0-\\u02C1"
        + "\\u02E0-\\u02E4\\u0345\\u037A\\u10FC"
        + "\\u1D2C-\\u1D6A\\u1D78\\u1D9B-\\u1DBF"
        + "\\u2071\\u207F\\u2090-\\u209C\\u2170-\\u217F"
        + "\\u24D0-\\u24E9\\u2C7C-\\u2C7D\\uA69C-\\uA69D"
        + "\\uA770\\uA7F2-\\uA7F4\\uA7F8-\\uA7F9"
        + "\\uAB5C-\\uAB5F\\uAB69";

    private const string LowercaseProperty =
        "(?:["
        + LowercaseBmp
        + "]"
        + "|\\uD801[\\uDF80\\uDF83-\\uDF85\\uDF87-\\uDFB0\\uDFB2-\\uDFBA]"
        + "|\\uD838[\\uDC30-\\uDC6D]"
        + ")";

    // Uppercase — \p{Lu} plus Other_Uppercase code points (2 BMP + 3 supplementary ranges)
    private const string UppercaseBmp =
        "\\p{Lu}\\u2160-\\u216F\\u24B6-\\u24CF";

    private const string UppercaseProperty =
        "(?:["
        + UppercaseBmp
        + "]"
        + "|\\uD83C[\\uDD30-\\uDD49\\uDD50-\\uDD69\\uDD70-\\uDD89]"
        + ")";

    // ID_Start — [\p{L}\p{Nl}] plus 4 extra BMP ranges (6 code points).
    // Technically excludes U+2E2F (Pattern_Syntax) — negligible over-match.
    private const string IdStartClass =
        "\\p{L}\\p{Nl}\\u1885-\\u1886\\u2118\\u212E\\u309B-\\u309C";

    // Emoji_Presentation — 33 BMP ranges + 47 supplementary ranges
    private const string EmojiPresentationBmp =
        "\\u231A-\\u231B\\u23E9-\\u23EC\\u23F0\\u23F3"
        + "\\u25FD-\\u25FE\\u2614-\\u2615\\u2648-\\u2653"
        + "\\u267F\\u2693\\u26A1\\u26AA-\\u26AB"
        + "\\u26BD-\\u26BE\\u26C4-\\u26C5\\u26CE\\u26D4\\u26EA"
        + "\\u26F2-\\u26F3\\u26F5\\u26FA\\u26FD\\u2705"
        + "\\u270A-\\u270B\\u2728\\u274C\\u274E"
        + "\\u2753-\\u2755\\u2757\\u2795-\\u2797"
        + "\\u27B0\\u27BF\\u2B1B-\\u2B1C\\u2B50\\u2B55";

    private const string EmojiPresentationProperty =
        "(?:["
        + EmojiPresentationBmp
        + "]"
        + "|\\uD83C[\\uDC04\\uDCCF\\uDD8E\\uDD91-\\uDD9A"
        + "\\uDDE6-\\uDDFF\\uDE01\\uDE1A\\uDE2F\\uDE32-\\uDE36"
        + "\\uDE38-\\uDE3A\\uDE50-\\uDE51\\uDF00-\\uDF20"
        + "\\uDF2D-\\uDF35\\uDF37-\\uDF7C\\uDF7E-\\uDF93"
        + "\\uDFA0-\\uDFCA\\uDFCF-\\uDFD3\\uDFE0-\\uDFF0"
        + "\\uDFF4\\uDFF8-\\uDFFF]"
        + "|\\uD83D[\\uDC00-\\uDC3E\\uDC40\\uDC42-\\uDCFC"
        + "\\uDCFF-\\uDD3D\\uDD4B-\\uDD4E\\uDD50-\\uDD67"
        + "\\uDD7A\\uDD95-\\uDD96\\uDDA4\\uDDFB-\\uDE4F"
        + "\\uDE80-\\uDEC5\\uDECC\\uDED0-\\uDED2\\uDED5-\\uDED7"
        + "\\uDEDC-\\uDEDF\\uDEEB-\\uDEEC\\uDEF4-\\uDEFC"
        + "\\uDFE0-\\uDFEB\\uDFF0]"
        + "|\\uD83E[\\uDD0C-\\uDD3A\\uDD3C-\\uDD45\\uDD47-\\uDDFF"
        + "\\uDE70-\\uDE7C\\uDE80-\\uDE89\\uDE8F-\\uDEC6"
        + "\\uDECE-\\uDEDC\\uDEDF-\\uDEE9\\uDEF0-\\uDEF8]"
        + ")";

    // Extended_Pictographic — 50 BMP ranges + 28 supplementary ranges
    private const string ExtendedPictographicBmp =
        "\\u00A9\\u00AE\\u203C\\u2049\\u2122\\u2139"
        + "\\u2194-\\u2199\\u21A9-\\u21AA\\u231A-\\u231B"
        + "\\u2328\\u2388\\u23CF\\u23E9-\\u23F3\\u23F8-\\u23FA"
        + "\\u24C2\\u25AA-\\u25AB\\u25B6\\u25C0\\u25FB-\\u25FE"
        + "\\u2600-\\u2605\\u2607-\\u2612\\u2614-\\u2685"
        + "\\u2690-\\u2705\\u2708-\\u2712\\u2714\\u2716\\u271D"
        + "\\u2721\\u2728\\u2733-\\u2734\\u2744\\u2747\\u274C"
        + "\\u274E\\u2753-\\u2755\\u2757\\u2763-\\u2767"
        + "\\u2795-\\u2797\\u27A1\\u27B0\\u27BF"
        + "\\u2934-\\u2935\\u2B05-\\u2B07\\u2B1B-\\u2B1C"
        + "\\u2B50\\u2B55\\u3030\\u303D\\u3297\\u3299";

    private const string ExtendedPictographicProperty =
        "(?:["
        + ExtendedPictographicBmp
        + "]"
        + "|\\uD83C[\\uDC00-\\uDCFF\\uDD0D-\\uDD0F\\uDD2F"
        + "\\uDD6C-\\uDD71\\uDD7E-\\uDD7F\\uDD8E\\uDD91-\\uDD9A"
        + "\\uDDAD-\\uDDE5\\uDE01-\\uDE0F\\uDE1A\\uDE2F"
        + "\\uDE32-\\uDE3A\\uDE3C-\\uDE3F\\uDE49-\\uDFFA]"
        + "|\\uD83D[\\uDC00-\\uDD3D\\uDD46-\\uDE4F"
        + "\\uDE80-\\uDEFF\\uDF74-\\uDF7F\\uDFD5-\\uDFFF]"
        + "|\\uD83E[\\uDC0C-\\uDC0F\\uDC48-\\uDC4F"
        + "\\uDC5A-\\uDC5F\\uDC88-\\uDC8F\\uDCAE-\\uDCFF"
        + "\\uDD0C-\\uDD3A\\uDD3C-\\uDD45\\uDD47-\\uDEFF]"
        + "|\\uD83F[\\uDC00-\\uDFFD]"
        + ")";

    /// <summary>
    /// Attempts to translate an ECMAScript 262 regex pattern (with /u Unicode mode)
    /// to an equivalent .NET regex pattern.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern to translate.</param>
    /// <param name="destination">The buffer to write the translated .NET pattern to.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="destination"/>.</param>
    /// <returns>
    /// <see cref="OperationStatus.Done"/> if the translation succeeded;
    /// <see cref="OperationStatus.DestinationTooSmall"/> if the destination buffer is too small;
    /// <see cref="OperationStatus.InvalidData"/> if the ECMAScript pattern is invalid.
    /// </returns>
    public static OperationStatus TryTranslate(
        ReadOnlySpan<char> ecmaPattern,
        Span<char> destination,
        out int charsWritten)
    {
        Translator translator = new(ecmaPattern, destination);
        OperationStatus status = translator.Run();
        charsWritten = translator.CharsWritten;
        return status;
    }

    /// <summary>
    /// Translates an ECMAScript 262 regex pattern (with /u Unicode mode)
    /// to an equivalent .NET regex pattern string.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern to translate.</param>
    /// <returns>The translated .NET regex pattern string.</returns>
    /// <exception cref="ArgumentException">The ECMAScript pattern is invalid.</exception>
    public static string Translate(ReadOnlySpan<char> ecmaPattern)
    {
        int maxLen = GetMaxTranslatedLength(ecmaPattern);
        char[]? rented = null;
        Span<char> buffer = maxLen <= StackAllocThreshold
            ? stackalloc char[StackAllocThreshold]
            : (rented = ArrayPool<char>.Shared.Rent(maxLen));

        try
        {
            OperationStatus status = TryTranslate(ecmaPattern, buffer, out int charsWritten);
            return status switch
            {
                OperationStatus.Done => buffer[..charsWritten].ToString(),
                OperationStatus.DestinationTooSmall => throw new InvalidOperationException(
                    "Internal error: buffer size estimation was too small."),
                _ => throw new ArgumentException(
                    "The ECMAScript pattern is invalid.", nameof(ecmaPattern)),
            };
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Translates an ECMAScript regex pattern to a .NET pattern, falling back
    /// to the original pattern if translation fails.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern.</param>
    /// <returns>The translated .NET regex pattern, or the original if translation fails.</returns>
    internal static string TranslateOrFallback(string ecmaPattern)
    {
        try
        {
            return Translate(ecmaPattern.AsSpan());
        }
        catch (ArgumentException)
        {
            return ecmaPattern;
        }
        catch (InvalidOperationException)
        {
            return ecmaPattern;
        }
    }

    /// <summary>
    /// Gets the maximum possible length of the translated pattern, suitable for buffer allocation.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern.</param>
    /// <returns>The maximum number of characters the translated pattern could require.</returns>
    public static int GetMaxTranslatedLength(ReadOnlySpan<char> ecmaPattern)
    {
        int maxLen = 0;
        int i = 0;
        while (i < ecmaPattern.Length)
        {
            if (ecmaPattern[i] == '\\' && i + 1 < ecmaPattern.Length)
            {
                char next = ecmaPattern[i + 1];
                if (next == 'u' && i + 2 < ecmaPattern.Length && ecmaPattern[i + 2] == '{')
                {
                    // \u{XXXXX} — could be part of a supplementary range in a class
                    // Worst case: (?:\uHH[\uLL-\uDFFF]|[\uHH-\uHH][\uDC00-\uDFFF]|\uHH[\uDC00-\uLL])
                    maxLen += 100;
                    int close = i + 3;
                    while (close < ecmaPattern.Length && ecmaPattern[close] != '}')
                    {
                        close++;
                    }

                    i = close < ecmaPattern.Length ? close + 1 : close;
                }
                else
                {
                    maxLen += next switch
                    {
                        'b' or 'B' => WordBoundary.Length,
                        's' or 'S' => NotWhitespaceClass.Length,
                        'd' or 'D' => NotDigitClass.Length,
                        'w' or 'W' => NotWordClass.Length,
                        'k' => 14, // \k<name> → (?(name)\k<name>) — name chars counted separately
                        'p' or 'P' => 1024, // binary properties can expand to large character classes
                        _ => 12, // backrefs become (?(N)\N), other escapes generous
                    };
                    i += 2;
                }
            }
            else if (ecmaPattern[i] == '.')
            {
                maxLen += DotExpansion.Length;
                i++;
            }
            else if (char.IsHighSurrogate(ecmaPattern[i]) && i + 1 < ecmaPattern.Length && char.IsLowSurrogate(ecmaPattern[i + 1]))
            {
                // Literal surrogate pair → (?:\uHHHH\uLLLL) = 20 chars
                maxLen += 20;
                i += 2;
            }
            else
            {
                maxLen++;
                i++;
            }
        }

        // Margin for character class restructuring (alternation, supplementary pairs, negated lookahead)
        return maxLen + 128;
    }

    private static bool TryParseHex(ReadOnlySpan<char> hex, out int value)
    {
        value = 0;
        for (int i = 0; i < hex.Length; i++)
        {
            int digit = HexDigitValue(hex[i]);
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) | digit;
        }

        return true;
    }

    private static int HexDigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    private static char ToHexChar(int value) =>
        (char)(value < 10 ? '0' + value : 'A' + value - 10);

    private static bool TryMapCategoryLongName(
        ReadOnlySpan<char> name,
        out string? shortName)
    {
        // General Category long names → short names
        // Ordered roughly by frequency of use
        shortName = null;

        if (name.SequenceEqual("Letter")) { shortName = "L"; }
        else if (name.SequenceEqual("Lowercase_Letter")) { shortName = "Ll"; }
        else if (name.SequenceEqual("Uppercase_Letter")) { shortName = "Lu"; }
        else if (name.SequenceEqual("Titlecase_Letter")) { shortName = "Lt"; }
        else if (name.SequenceEqual("Cased_Letter")) { shortName = "LC"; }
        else if (name.SequenceEqual("Modifier_Letter")) { shortName = "Lm"; }
        else if (name.SequenceEqual("Other_Letter")) { shortName = "Lo"; }
        else if (name.SequenceEqual("Mark")) { shortName = "M"; }
        else if (name.SequenceEqual("Combining_Mark")) { shortName = "M"; }
        else if (name.SequenceEqual("Nonspacing_Mark")) { shortName = "Mn"; }
        else if (name.SequenceEqual("Spacing_Mark")) { shortName = "Mc"; }
        else if (name.SequenceEqual("Enclosing_Mark")) { shortName = "Me"; }
        else if (name.SequenceEqual("Number")) { shortName = "N"; }
        else if (name.SequenceEqual("Decimal_Number")) { shortName = "Nd"; }
        else if (name.SequenceEqual("digit")) { shortName = "Nd"; }
        else if (name.SequenceEqual("Letter_Number")) { shortName = "Nl"; }
        else if (name.SequenceEqual("Other_Number")) { shortName = "No"; }
        else if (name.SequenceEqual("Punctuation")) { shortName = "P"; }
        else if (name.SequenceEqual("punct")) { shortName = "P"; }
        else if (name.SequenceEqual("Connector_Punctuation")) { shortName = "Pc"; }
        else if (name.SequenceEqual("Dash_Punctuation")) { shortName = "Pd"; }
        else if (name.SequenceEqual("Open_Punctuation")) { shortName = "Ps"; }
        else if (name.SequenceEqual("Close_Punctuation")) { shortName = "Pe"; }
        else if (name.SequenceEqual("Initial_Punctuation")) { shortName = "Pi"; }
        else if (name.SequenceEqual("Final_Punctuation")) { shortName = "Pf"; }
        else if (name.SequenceEqual("Other_Punctuation")) { shortName = "Po"; }
        else if (name.SequenceEqual("Symbol")) { shortName = "S"; }
        else if (name.SequenceEqual("Math_Symbol")) { shortName = "Sm"; }
        else if (name.SequenceEqual("Currency_Symbol")) { shortName = "Sc"; }
        else if (name.SequenceEqual("Modifier_Symbol")) { shortName = "Sk"; }
        else if (name.SequenceEqual("Other_Symbol")) { shortName = "So"; }
        else if (name.SequenceEqual("Separator")) { shortName = "Z"; }
        else if (name.SequenceEqual("Space_Separator")) { shortName = "Zs"; }
        else if (name.SequenceEqual("Line_Separator")) { shortName = "Zl"; }
        else if (name.SequenceEqual("Paragraph_Separator")) { shortName = "Zp"; }
        else if (name.SequenceEqual("Other")) { shortName = "C"; }
        else if (name.SequenceEqual("Control")) { shortName = "Cc"; }
        else if (name.SequenceEqual("cntrl")) { shortName = "Cc"; }
        else if (name.SequenceEqual("Format")) { shortName = "Cf"; }
        else if (name.SequenceEqual("Surrogate")) { shortName = "Cs"; }
        else if (name.SequenceEqual("Private_Use")) { shortName = "Co"; }
        else if (name.SequenceEqual("Unassigned")) { shortName = "Cn"; }

        return shortName is not null;
    }

    private static bool IsValidShortCategory(ReadOnlySpan<char> name)
    {
        // Valid .NET short category names (1-2 chars)
        if (name.Length == 1)
        {
            return name[0] is 'L' or 'M' or 'N' or 'P' or 'S' or 'Z' or 'C';
        }

        if (name.Length == 2)
        {
            char c0 = name[0];
            char c1 = name[1];
            return (c0, c1) switch
            {
                ('L', 'u' or 'l' or 't' or 'm' or 'o') => true,
                ('L', 'C') => true, // Cased_Letter
                ('M', 'n' or 'c' or 'e') => true,
                ('N', 'd' or 'l' or 'o') => true,
                ('P', 'c' or 'd' or 's' or 'e' or 'i' or 'f' or 'o') => true,
                ('S', 'm' or 'c' or 'k' or 'o') => true,
                ('Z', 's' or 'l' or 'p') => true,
                ('C', 'c' or 'f' or 's' or 'o' or 'n') => true,
                _ => false,
            };
        }

        return false;
    }

    private static bool TryMapBinaryProperty(
        ReadOnlySpan<char> name,
        out string? classContent,
        out string? alternationPattern)
    {
        // ECMAScript binary Unicode properties → .NET equivalents.
        // classContent: content usable inside [...] (BMP-only properties).
        // alternationPattern: complete pattern for properties with supplementary code points.
        // Only one of the two will be set.
        classContent = null;
        alternationPattern = null;

        if (name.SequenceEqual("ASCII"))
        {
            classContent = "\\u0000-\\u007F";
        }
        else if (name.SequenceEqual("Alphabetic"))
        {
            // Close approximation: Unicode Letter + Mark categories
            classContent = "\\p{L}\\p{M}";
        }
        else if (name.SequenceEqual("White_Space"))
        {
            classContent = WhitespaceInline;
        }
        else if (name.SequenceEqual("Emoji"))
        {
            // Emoji includes BMP symbols and supplementary code points.
            // Supplementary ranges must use surrogate-pair alternation.
            alternationPattern =
                "(?:"
                + "[\\u0023\\u002A\\u0030-\\u0039\\u00A9\\u00AE"
                + "\\u203C\\u2049\\u2122\\u2139\\u2194-\\u2199"
                + "\\u21A9\\u21AA\\u231A\\u231B\\u2328\\u23CF"
                + "\\u23E9-\\u23F3\\u23F8-\\u23FA\\u24C2"
                + "\\u25AA\\u25AB\\u25B6\\u25C0\\u25FB-\\u25FE"
                + "\\u2600-\\u27BF\\u2934\\u2935\\u2B05-\\u2B07"
                + "\\u2B1B\\u2B1C\\u2B50\\u2B55\\u3030\\u303D\\u3297\\u3299]"
                + "|\\uD83C[\\uDC00-\\uDFFF]"
                + "|\\uD83D[\\uDC00-\\uDFFF]"
                + "|\\uD83E[\\uDD00-\\uDFFF]"
                + ")";
        }
        else if (name.SequenceEqual("Any"))
        {
            classContent = "\\s\\S";
        }
        else if (name.SequenceEqual("Assigned"))
        {
            // Everything except Unassigned
            classContent = "\\P{Cn}";
        }
        else if (name.SequenceEqual("Hex_Digit"))
        {
            classContent = HexDigitClass;
        }
        else if (name.SequenceEqual("Lowercase"))
        {
            alternationPattern = LowercaseProperty;
        }
        else if (name.SequenceEqual("Uppercase"))
        {
            alternationPattern = UppercaseProperty;
        }
        else if (name.SequenceEqual("ID_Start"))
        {
            classContent = IdStartClass;
        }
        else if (name.SequenceEqual("Emoji_Presentation"))
        {
            alternationPattern = EmojiPresentationProperty;
        }
        else if (name.SequenceEqual("Extended_Pictographic"))
        {
            alternationPattern = ExtendedPictographicProperty;
        }

        return classContent is not null || alternationPattern is not null;
    }

    private static bool TryMapScriptName(
        ReadOnlySpan<char> name,
        out string? dotNetBlockName,
        out string? fullPattern)
    {
        // Map ES script names → .NET block names or full regex patterns.
        // Most scripts map cleanly to a .NET \p{IsXxx} block name.
        // Latin requires an explicit character class because it spans many blocks.
        dotNetBlockName = null;
        fullPattern = null;

        if (name.SequenceEqual("Latin") || name.SequenceEqual("Latn"))
        {
            fullPattern = LatinScript;
        }
        else if (name.SequenceEqual("Greek") || name.SequenceEqual("Grek"))
        {
            dotNetBlockName = "IsGreek";
        }
        else if (name.SequenceEqual("Cyrillic") || name.SequenceEqual("Cyrl"))
        {
            dotNetBlockName = "IsCyrillic";
        }
        else if (name.SequenceEqual("Armenian") || name.SequenceEqual("Armn"))
        {
            dotNetBlockName = "IsArmenian";
        }
        else if (name.SequenceEqual("Hebrew") || name.SequenceEqual("Hebr"))
        {
            dotNetBlockName = "IsHebrew";
        }
        else if (name.SequenceEqual("Arabic") || name.SequenceEqual("Arab"))
        {
            dotNetBlockName = "IsArabic";
        }
        else if (name.SequenceEqual("Thai") || name.SequenceEqual("Thai"))
        {
            dotNetBlockName = "IsThai";
        }
        else if (name.SequenceEqual("Georgian") || name.SequenceEqual("Geor"))
        {
            dotNetBlockName = "IsGeorgian";
        }
        else if (name.SequenceEqual("Hangul") || name.SequenceEqual("Hang"))
        {
            dotNetBlockName = "IsHangulJamo";
        }
        else if (name.SequenceEqual("Hiragana") || name.SequenceEqual("Hira"))
        {
            dotNetBlockName = "IsHiragana";
        }
        else if (name.SequenceEqual("Katakana") || name.SequenceEqual("Kana"))
        {
            dotNetBlockName = "IsKatakana";
        }
        else if (name.SequenceEqual("Tibetan") || name.SequenceEqual("Tibt"))
        {
            dotNetBlockName = "IsTibetan";
        }
        else if (name.SequenceEqual("Bengali") || name.SequenceEqual("Beng"))
        {
            dotNetBlockName = "IsBengali";
        }
        else if (name.SequenceEqual("Devanagari") || name.SequenceEqual("Deva"))
        {
            dotNetBlockName = "IsDevanagari";
        }
        else if (name.SequenceEqual("Gujarati") || name.SequenceEqual("Gujr"))
        {
            dotNetBlockName = "IsGujarati";
        }
        else if (name.SequenceEqual("Tamil") || name.SequenceEqual("Taml"))
        {
            dotNetBlockName = "IsTamil";
        }
        else if (name.SequenceEqual("Telugu") || name.SequenceEqual("Telu"))
        {
            dotNetBlockName = "IsTelugu";
        }
        else if (name.SequenceEqual("Kannada") || name.SequenceEqual("Knda"))
        {
            dotNetBlockName = "IsKannada";
        }
        else if (name.SequenceEqual("Malayalam") || name.SequenceEqual("Mlym"))
        {
            dotNetBlockName = "IsMalayalam";
        }
        else if (name.SequenceEqual("Sinhala") || name.SequenceEqual("Sinh"))
        {
            dotNetBlockName = "IsSinhala";
        }
        else if (name.SequenceEqual("Myanmar") || name.SequenceEqual("Mymr"))
        {
            dotNetBlockName = "IsMyanmar";
        }
        else if (name.SequenceEqual("Ethiopic") || name.SequenceEqual("Ethi"))
        {
            dotNetBlockName = "IsEthiopic";
        }
        else if (name.SequenceEqual("Khmer") || name.SequenceEqual("Khmr"))
        {
            dotNetBlockName = "IsKhmer";
        }
        else if (name.SequenceEqual("Mongolian") || name.SequenceEqual("Mong"))
        {
            dotNetBlockName = "IsMongolian";
        }
        else if (name.SequenceEqual("Lao") || name.SequenceEqual("Laoo"))
        {
            dotNetBlockName = "IsLao";
        }

        return dotNetBlockName is not null || fullPattern is not null;
    }

    /// <summary>
    /// Scanning result for a character class.
    /// </summary>
    private struct CharClassInfo
    {
        /// <summary>Position of the closing <c>]</c> in the input.</summary>
        public int EndPos;

        /// <summary>Whether the class is negated (<c>[^...]</c>).</summary>
        public bool IsNegated;

        /// <summary>Whether <c>\D</c> appears in the class.</summary>
        public bool HasNegD;

        /// <summary>Whether <c>\W</c> appears in the class.</summary>
        public bool HasNegW;

        /// <summary>Whether <c>\S</c> appears in the class.</summary>
        public bool HasNegS;

        /// <summary>Whether the class has any non-negated-shorthand content.</summary>
        public bool HasPositiveParts;

        /// <summary>Whether the class contains any supplementary (astral) code points via <c>\u{XXXXX}</c>.</summary>
        public bool HasSupplementary;

        /// <summary>Whether the class contains a property escape that expands to an alternation pattern (e.g. Emoji, Script=Latin).</summary>
        public bool HasAlternationProperty;

        public readonly bool HasNegatedShorthands => HasNegD || HasNegW || HasNegS;
    }

    /// <summary>
    /// Scans character class content (starting after <c>[</c> and optional <c>^</c>)
    /// to find the closing <c>]</c> and detect negated shorthands.
    /// Does not modify any translator state.
    /// </summary>
    private static CharClassInfo ScanCharClassContent(ReadOnlySpan<char> input, int startPos)
    {
        CharClassInfo info = default;
        int pos = startPos;

        while (pos < input.Length && input[pos] != ']')
        {
            if (input[pos] == '\\' && pos + 1 < input.Length)
            {
                char next = input[pos + 1];
                switch (next)
                {
                    case 'D':
                        info.HasNegD = true;
                        pos += 2;
                        break;
                    case 'W':
                        info.HasNegW = true;
                        pos += 2;
                        break;
                    case 'S':
                        info.HasNegS = true;
                        pos += 2;
                        break;
                    case 'u':
                        info.HasPositiveParts = true;
                        pos += 2;
                        if (pos < input.Length && input[pos] == '{')
                        {
                            int hexStart = pos + 1;
                            pos++;
                            while (pos < input.Length && input[pos] != '}')
                            {
                                pos++;
                            }

                            if (pos < input.Length)
                            {
                                if (TryParseHex(input[hexStart..pos], out int cp) && cp > 0xFFFF)
                                {
                                    info.HasSupplementary = true;
                                }

                                pos++;
                            }
                        }
                        else
                        {
                            pos = Math.Min(pos + 4, input.Length);
                        }

                        break;
                    case 'p':
                    case 'P':
                        info.HasPositiveParts = true;
                        pos += 2;
                        if (pos < input.Length && input[pos] == '{')
                        {
                            int propStart = pos + 1;
                            pos++;
                            while (pos < input.Length && input[pos] != '}')
                            {
                                pos++;
                            }

                            if (pos < input.Length)
                            {
                                // Check if this property resolves to an alternation pattern
                                ReadOnlySpan<char> propExpr = input[propStart..pos];
                                if (IsAlternationProperty(propExpr))
                                {
                                    info.HasAlternationProperty = true;
                                }

                                pos++;
                            }
                        }

                        break;
                    case 'x':
                        info.HasPositiveParts = true;
                        pos = Math.Min(pos + 4, input.Length);
                        break;
                    case 'c':
                        info.HasPositiveParts = true;
                        pos = Math.Min(pos + 3, input.Length);
                        break;
                    default:
                        info.HasPositiveParts = true;
                        pos += 2;
                        break;
                }
            }
            else
            {
                info.HasPositiveParts = true;
                if (char.IsHighSurrogate(input[pos]) && pos + 1 < input.Length && input[pos + 1] != ']' && char.IsLowSurrogate(input[pos + 1]))
                {
                    int cp = char.ConvertToUtf32(input[pos], input[pos + 1]);
                    if (cp > 0xFFFF)
                    {
                        info.HasSupplementary = true;
                    }

                    pos += 2;
                }
                else
                {
                    pos++;
                }
            }
        }

        info.EndPos = pos;
        return info;
    }

    /// <summary>
    /// Determines whether a property expression (the content between { and }) resolves to
    /// an alternation pattern that cannot be inlined into a character class.
    /// </summary>
    private static bool IsAlternationProperty(ReadOnlySpan<char> propExpr)
    {
        int eqIndex = propExpr.IndexOf('=');
        if (eqIndex >= 0)
        {
            ReadOnlySpan<char> name = propExpr[..eqIndex];
            ReadOnlySpan<char> value = propExpr[(eqIndex + 1)..];

            if (name.SequenceEqual("Script") || name.SequenceEqual("sc")
                || name.SequenceEqual("Script_Extensions") || name.SequenceEqual("scx"))
            {
                return TryMapScriptName(value, out _, out string? fullPattern) && fullPattern is not null;
            }

            return false;
        }

        // Lone value: check binary properties
        if (TryMapCategoryLongName(propExpr, out _) || IsValidShortCategory(propExpr))
        {
            return false;
        }

        return TryMapBinaryProperty(propExpr, out _, out string? altPattern) && altPattern is not null;
    }

    private ref struct Translator
    {
        private readonly ReadOnlySpan<char> _input;
        private readonly Span<char> _output;
        private int _in;
        private int _out;

        public Translator(ReadOnlySpan<char> input, Span<char> output)
        {
            _input = input;
            _output = output;
            _in = 0;
            _out = 0;
        }

        public readonly int CharsWritten => _out;

        public OperationStatus Run()
        {
            while (_in < _input.Length)
            {
                char c = _input[_in];
                OperationStatus status;

                switch (c)
                {
                    case '.':
                        status = Emit(DotExpansion);
                        if (status != OperationStatus.Done)
                        {
                            return status;
                        }

                        _in++;
                        break;

                    case '[':
                        status = TranslateCharacterClass();
                        if (status != OperationStatus.Done)
                        {
                            return status;
                        }

                        break;

                    case '\\':
                        status = TranslateEscape();
                        if (status != OperationStatus.Done)
                        {
                            return status;
                        }

                        break;

                    default:
                        if (char.IsHighSurrogate(c) && _in + 1 < _input.Length && char.IsLowSurrogate(_input[_in + 1]))
                        {
                            // Literal non-BMP character (e.g. 🐲) stored as UTF-16 surrogate pair.
                            // Wrap in non-capturing group so quantifiers apply to the whole code point.
                            int cp = char.ConvertToUtf32(c, _input[_in + 1]);
                            status = EmitStandaloneCodePointAtom(cp);
                            if (status != OperationStatus.Done)
                            {
                                return status;
                            }

                            _in += 2;
                        }
                        else
                        {
                            status = Emit(c);
                            if (status != OperationStatus.Done)
                            {
                                return status;
                            }

                            _in++;
                        }

                        break;
                }
            }

            return OperationStatus.Done;
        }

        private OperationStatus TranslateEscape()
        {
            if (_in + 1 >= _input.Length)
            {
                return OperationStatus.InvalidData;
            }

            char next = _input[_in + 1];

            switch (next)
            {
                case 'd':
                    _in += 2;
                    return Emit(DigitClass);

                case 'D':
                    _in += 2;
                    return Emit(NotDigitClass);

                case 'w':
                    _in += 2;
                    return Emit(WordClass);

                case 'W':
                    _in += 2;
                    return Emit(NotWordClass);

                case 's':
                    _in += 2;
                    return Emit(WhitespaceClass);

                case 'S':
                    _in += 2;
                    return Emit(NotWhitespaceClass);

                case 'b':
                    _in += 2;
                    return Emit(WordBoundary);

                case 'B':
                    _in += 2;
                    return Emit(NonWordBoundary);

                case 'u':
                    return TranslateUnicodeEscape();

                case 'p':
                case 'P':
                    return TranslatePropertyEscape(insideCharClass: false);

                case '0':
                {
                    // ECMAScript /u mode: \0 is the NUL character, but must NOT
                    // be followed by a decimal digit (octal escapes are forbidden).
                    // Without this check, \01 would pass through and .NET would
                    // interpret it as octal \x01 rather than rejecting it.
                    if (_in + 2 < _input.Length && _input[_in + 2] is >= '0' and <= '9')
                    {
                        return OperationStatus.InvalidData;
                    }

                    OperationStatus s = Emit('\\');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    _in += 2;
                    return Emit('0');
                }

                // Pass-through escapes
                case 't' or 'n' or 'r' or 'f' or 'v':
                case '^' or '$' or '.' or '*' or '+' or '?' or '/':
                case '(' or ')' or '[' or ']' or '{' or '}' or '|' or '\\':
                {
                    OperationStatus s = Emit('\\');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    _in += 2;
                    return Emit(next);
                }

                case 'c':
                {
                    // \cX control escape — X must be a letter [a-zA-Z]
                    if (_in + 2 >= _input.Length)
                    {
                        return OperationStatus.InvalidData;
                    }

                    char controlLetter = _input[_in + 2];
                    if (controlLetter is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
                    {
                        return OperationStatus.InvalidData;
                    }

                    OperationStatus s = Emit(_input.Slice(_in, 3));
                    _in += 3;
                    return s;
                }

                case 'x':
                {
                    // \xHH hex escape
                    if (_in + 3 >= _input.Length)
                    {
                        return OperationStatus.InvalidData;
                    }

                    OperationStatus s = Emit(_input.Slice(_in, 4));
                    _in += 4;
                    return s;
                }

                case 'k':
                {
                    // \k<name> named backreference
                    // Wrap in conditional to match ECMAScript non-participating semantics:
                    // (?(name)\k<name>) → if group captured, match it; else match empty
                    _in += 2;
                    if (_in >= _input.Length || _input[_in] != '<')
                    {
                        return OperationStatus.InvalidData;
                    }

                    // Extract the name
                    int nameStart = _in + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < _input.Length && _input[nameEnd] != '>')
                    {
                        nameEnd++;
                    }

                    if (nameEnd >= _input.Length)
                    {
                        return OperationStatus.InvalidData;
                    }

                    ReadOnlySpan<char> name = _input[nameStart..nameEnd];

                    OperationStatus s = Emit("(?(");
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    s = Emit(name);
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    s = Emit(")\\k<");
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    s = Emit(name);
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    s = Emit(">)");
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    _in = nameEnd + 1;
                    return OperationStatus.Done;
                }

                default:
                {
                    if (next is >= '1' and <= '9')
                    {
                        // Backreference: wrap in conditional to match ECMAScript
                        // non-participating/forward reference semantics:
                        // \N → (?(N)\N) — if group captured, match it; else match empty
                        _in++;
                        int digitStart = _in;
                        while (_in < _input.Length && _input[_in] is >= '0' and <= '9')
                        {
                            _in++;
                        }

                        ReadOnlySpan<char> digits = _input[digitStart.._in];

                        OperationStatus s = Emit("(?(");
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        s = Emit(digits);
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        s = Emit(")\\");
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        s = Emit(digits);
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        s = Emit(')');
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        return OperationStatus.Done;
                    }

                    // In /u mode, no other identity escapes are valid.
                    // The only valid identity escapes (SyntaxCharacter and /)
                    // are handled by the explicit cases above.
                    return OperationStatus.InvalidData;
                }
            }
        }

        private OperationStatus TranslateUnicodeEscape(bool insideCharClass = false)
        {
            _in += 2; // skip \u

            if (_in >= _input.Length)
            {
                return OperationStatus.InvalidData;
            }

            if (_input[_in] == '{')
            {
                // \u{XXXX...} form
                _in++;
                int start = _in;
                while (_in < _input.Length && _input[_in] != '}')
                {
                    _in++;
                }

                if (_in >= _input.Length)
                {
                    return OperationStatus.InvalidData;
                }

                ReadOnlySpan<char> hexDigits = _input[start.._in];
                _in++; // skip '}'

                if (!TryParseHex(hexDigits, out int codePoint))
                {
                    return OperationStatus.InvalidData;
                }

                // Outside character classes, supplementary code points need a non-capturing
                // group wrapper so quantifiers apply to the whole code point.
                return insideCharClass ? EmitCodePoint(codePoint) : EmitStandaloneCodePointAtom(codePoint);
            }
            else
            {
                // \uXXXX form - same syntax in .NET, pass through
                if (_in + 4 > _input.Length)
                {
                    return OperationStatus.InvalidData;
                }

                OperationStatus s = Emit("\\u");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(_input.Slice(_in, 4));
                _in += 4;
                return s;
            }
        }

        private OperationStatus EmitCodePoint(int codePoint)
        {
            if (codePoint <= 0xFFFF)
            {
                return EmitUnicodeEscape((char)codePoint);
            }

            if (codePoint <= 0x10FFFF)
            {
                // Supplementary character: emit as surrogate pair
                int adjusted = codePoint - 0x10000;
                char high = (char)(0xD800 + (adjusted >> 10));
                char low = (char)(0xDC00 + (adjusted & 0x3FF));
                OperationStatus s = EmitUnicodeEscape(high);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return EmitUnicodeEscape(low);
            }

            return OperationStatus.InvalidData;
        }

        /// <summary>
        /// Emits a Unicode code point as a standalone regex atom. For supplementary code points,
        /// wraps the surrogate pair in a non-capturing group <c>(?:\uHHHH\uLLLL)</c> so that
        /// any following quantifier applies to the entire code point.
        /// </summary>
        private OperationStatus EmitStandaloneCodePointAtom(int codePoint)
        {
            if (codePoint <= 0xFFFF)
            {
                return EmitUnicodeEscape((char)codePoint);
            }

            if (codePoint <= 0x10FFFF)
            {
                OperationStatus s = Emit("(?:");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                int adjusted = codePoint - 0x10000;
                char high = (char)(0xD800 + (adjusted >> 10));
                char low = (char)(0xDC00 + (adjusted & 0x3FF));
                s = EmitUnicodeEscape(high);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = EmitUnicodeEscape(low);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return Emit(')');
            }

            return OperationStatus.InvalidData;
        }

        private OperationStatus EmitUnicodeEscape(char c)
        {
            if (_out + 6 > _output.Length)
            {
                return OperationStatus.DestinationTooSmall;
            }

            _output[_out++] = '\\';
            _output[_out++] = 'u';
            _output[_out++] = ToHexChar((c >> 12) & 0xF);
            _output[_out++] = ToHexChar((c >> 8) & 0xF);
            _output[_out++] = ToHexChar((c >> 4) & 0xF);
            _output[_out++] = ToHexChar(c & 0xF);
            return OperationStatus.Done;
        }

        private OperationStatus TranslatePropertyEscape(bool insideCharClass)
        {
            bool negated = _input[_in + 1] == 'P';
            _in += 2; // skip \p or \P

            if (_in >= _input.Length || _input[_in] != '{')
            {
                return OperationStatus.InvalidData;
            }

            _in++; // skip '{'

            int start = _in;
            while (_in < _input.Length && _input[_in] != '}')
            {
                _in++;
            }

            if (_in >= _input.Length)
            {
                return OperationStatus.InvalidData;
            }

            ReadOnlySpan<char> propertyExpr = _input[start.._in];
            _in++; // skip '}'

            return EmitTranslatedProperty(propertyExpr, negated, insideCharClass);
        }

        private OperationStatus EmitTranslatedProperty(ReadOnlySpan<char> propertyExpr, bool negated, bool insideCharClass)
        {
            int eqIndex = propertyExpr.IndexOf('=');
            if (eqIndex >= 0)
            {
                ReadOnlySpan<char> name = propertyExpr[..eqIndex];
                ReadOnlySpan<char> value = propertyExpr[(eqIndex + 1)..];

                if (name.SequenceEqual("General_Category") || name.SequenceEqual("gc"))
                {
                    return EmitCategoryProperty(value, negated);
                }

                if (name.SequenceEqual("Script") || name.SequenceEqual("sc")
                    || name.SequenceEqual("Script_Extensions") || name.SequenceEqual("scx"))
                {
                    return EmitScriptProperty(value, negated);
                }

                // Unknown property name in /u mode is an error
                return OperationStatus.InvalidData;
            }

            // Lone value: general category value or binary property
            return EmitCategoryOrBinaryProperty(propertyExpr, negated, insideCharClass);
        }

        private OperationStatus EmitCategoryProperty(ReadOnlySpan<char> value, bool negated)
        {
            OperationStatus s = Emit(negated ? "\\P{" : "\\p{");
            if (s != OperationStatus.Done)
            {
                return s;
            }

            if (TryMapCategoryLongName(value, out string? shortName))
            {
                s = Emit(shortName);
            }
            else if (IsValidShortCategory(value))
            {
                s = Emit(value);
            }
            else
            {
                return OperationStatus.InvalidData;
            }

            if (s != OperationStatus.Done)
            {
                return s;
            }

            return Emit('}');
        }

        private OperationStatus EmitScriptProperty(ReadOnlySpan<char> value, bool negated)
        {
            if (TryMapScriptName(value, out string? dotNetBlockName, out string? fullPattern))
            {
                if (fullPattern is not null)
                {
                    // Script with explicit pattern (e.g. Latin)
                    if (negated)
                    {
                        OperationStatus sn = Emit("(?!");
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        sn = Emit(fullPattern);
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        sn = Emit(')');
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        return Emit(AnyCodePoint);
                    }

                    return Emit(fullPattern);
                }

                OperationStatus s = Emit(negated ? "\\P{" : "\\p{");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(dotNetBlockName);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return Emit('}');
            }

            // Unknown script name
            return OperationStatus.InvalidData;
        }

        private OperationStatus EmitCategoryOrBinaryProperty(ReadOnlySpan<char> value, bool negated, bool insideCharClass)
        {
            // Try general category long name first
            if (TryMapCategoryLongName(value, out string? shortName))
            {
                OperationStatus s = Emit(negated ? "\\P{" : "\\p{");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(shortName);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return Emit('}');
            }

            // Try valid short category name
            if (IsValidShortCategory(value))
            {
                OperationStatus s = Emit(negated ? "\\P{" : "\\p{");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(value);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return Emit('}');
            }

            // Try binary property → character class or alternation expansion
            if (TryMapBinaryProperty(value, out string? classContent, out string? alternationPattern))
            {
                if (alternationPattern is not null)
                {
                    // Properties with supplementary code points use a complete alternation pattern.
                    // These cannot be inlined into a character class.
                    if (insideCharClass)
                    {
                        return OperationStatus.InvalidData;
                    }

                    if (negated)
                    {
                        // Negate via negative lookahead: (?!pattern)(.)
                        OperationStatus sn = Emit("(?!");
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        sn = Emit(alternationPattern);
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        sn = Emit(')');
                        if (sn != OperationStatus.Done)
                        {
                            return sn;
                        }

                        return Emit(AnyCodePoint);
                    }

                    return Emit(alternationPattern);
                }

                if (insideCharClass)
                {
                    // Inside a character class: emit inline content only.
                    // The caller already opened the class brackets.
                    return Emit(classContent);
                }

                return Emit(negated ? "[^" : "[") is var s1 && s1 != OperationStatus.Done
                    ? s1
                    : Emit(classContent) is var s2 && s2 != OperationStatus.Done
                    ? s2
                    : Emit(']');
            }

            // Unrecognised property name in /u mode
            return OperationStatus.InvalidData;
        }

        private OperationStatus TranslateCharacterClass()
        {
            _in++; // skip '['

            bool isNegated = false;
            if (_in < _input.Length && _input[_in] == '^')
            {
                isNegated = true;
                _in++;
            }

            // Handle empty class [] and match-any class [^]
            if (_in < _input.Length && _input[_in] == ']')
            {
                _in++; // skip ']'
                return isNegated ? Emit("[\\s\\S]") : Emit("[^\\s\\S]");
            }

            // Scan from current position to determine strategy
            CharClassInfo info = ScanCharClassContent(_input, _in);
            info.IsNegated = isNegated;

            if (info.EndPos >= _input.Length)
            {
                return OperationStatus.InvalidData;
            }

            if (!info.HasNegatedShorthands)
            {
                if (info.HasAlternationProperty)
                {
                    return EmitAlternationPropertyCharClass(isNegated, in info);
                }

                if (info.HasSupplementary)
                {
                    return EmitSupplementaryCharClass(isNegated, in info);
                }

                return EmitSimpleCharClass(isNegated, info.EndPos);
            }

            if (!isNegated)
            {
                return EmitAlternationCharClass(in info);
            }

            return EmitSubtractionCharClass(in info);
        }

        private OperationStatus EmitSimpleCharClass(bool isNegated, int endPos)
        {
            OperationStatus s = Emit(isNegated ? "[^" : "[");
            if (s != OperationStatus.Done)
            {
                return s;
            }

            s = EmitCharClassContent(endPos, skipNegShorthands: false);
            if (s != OperationStatus.Done)
            {
                return s;
            }

            s = Emit(']');
            _in = endPos + 1;
            return s;
        }

        /// <summary>
        /// Emits a character class that contains one or more property escapes resolving to
        /// alternation patterns (e.g. Emoji, Script=Latin). These cannot be inlined into
        /// a single [...] class, so we emit (?:[inlineContent]|pattern1|pattern2|...).
        /// For negated classes: (?!pattern1|[inlineContent]|pattern2|...)(anyCodePoint).
        /// </summary>
        private OperationStatus EmitAlternationPropertyCharClass(bool isNegated, in CharClassInfo info)
        {
            int endPos = info.EndPos;
            int savedIn = _in;

            // For negated: (?!...) + AnyCodePoint
            // For non-negated: (?:...)
            OperationStatus s = Emit(isNegated ? "(?!" : "(?:");
            if (s != OperationStatus.Done)
            {
                return s;
            }

            // We need to walk the content, accumulating inline parts and emitting alternation
            // patterns as alternatives. We'll buffer inline content in a [..] class.
            bool hasInlineContent = false;
            bool needsSeparator = false;

            // Determine if there's any non-alternation-property content
            int scanPos = savedIn;
            while (scanPos < endPos)
            {
                if (_input[scanPos] == '\\' && scanPos + 1 < endPos
                    && (_input[scanPos + 1] == 'p' || _input[scanPos + 1] == 'P')
                    && scanPos + 2 < endPos && _input[scanPos + 2] == '{')
                {
                    int braceStart = scanPos + 3;
                    int braceEnd = braceStart;
                    while (braceEnd < endPos && _input[braceEnd] != '}')
                    {
                        braceEnd++;
                    }

                    ReadOnlySpan<char> propExpr = _input[braceStart..braceEnd];
                    if (IsAlternationProperty(propExpr))
                    {
                        scanPos = braceEnd + 1;
                        continue;
                    }
                }

                hasInlineContent = true;
                break;
            }

            // First, emit inline content (non-alternation parts) as a character class
            if (hasInlineContent)
            {
                s = Emit('[');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                _in = savedIn;
                s = EmitCharClassContentSkippingAlternationProperties(endPos);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(']');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needsSeparator = true;
            }

            // Now emit alternation-pattern properties as alternatives
            _in = savedIn;
            while (_in < endPos)
            {
                if (_input[_in] == '\\' && _in + 1 < endPos
                    && (_input[_in + 1] == 'p' || _input[_in + 1] == 'P')
                    && _in + 2 < endPos && _input[_in + 2] == '{')
                {
                    int braceStart = _in + 3;
                    int braceEnd = braceStart;
                    while (braceEnd < endPos && _input[braceEnd] != '}')
                    {
                        braceEnd++;
                    }

                    ReadOnlySpan<char> propExpr = _input[braceStart..braceEnd];
                    if (IsAlternationProperty(propExpr))
                    {
                        if (needsSeparator)
                        {
                            s = Emit('|');
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }
                        }

                        // Emit the property as a standalone pattern (not inside class)
                        s = TranslatePropertyEscape(insideCharClass: false);
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }

                        needsSeparator = true;
                        continue;
                    }
                }

                // Skip non-alternation content (already emitted in the inline class above)
                SkipOneClassAtom(endPos);
            }

            s = Emit(')');
            if (s != OperationStatus.Done)
            {
                return s;
            }

            if (isNegated)
            {
                s = Emit(AnyCodePoint);
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            _in = endPos + 1; // skip ']'
            return OperationStatus.Done;
        }

        /// <summary>
        /// Emits character class content, skipping any property escapes that resolve to alternation patterns.
        /// </summary>
        private OperationStatus EmitCharClassContentSkippingAlternationProperties(int endPos)
        {
            while (_in < endPos)
            {
                if (_input[_in] == '\\' && _in + 1 < endPos
                    && (_input[_in + 1] == 'p' || _input[_in + 1] == 'P')
                    && _in + 2 < endPos && _input[_in + 2] == '{')
                {
                    int braceStart = _in + 3;
                    int braceEnd = braceStart;
                    while (braceEnd < endPos && _input[braceEnd] != '}')
                    {
                        braceEnd++;
                    }

                    ReadOnlySpan<char> propExpr = _input[braceStart..braceEnd];
                    if (IsAlternationProperty(propExpr))
                    {
                        // Skip this property — it will be emitted as an alternation
                        _in = braceEnd + 1;
                        continue;
                    }
                }

                // Emit one atom of class content
                OperationStatus s = EmitOneCharClassAtom(endPos);
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            return OperationStatus.Done;
        }

        /// <summary>
        /// Emits exactly one atom (character, escape, range) from the character class content.
        /// </summary>
        private OperationStatus EmitOneCharClassAtom(int endPos)
        {
            if (_input[_in] == '\\' && _in + 1 < endPos)
            {
                char esc = _input[_in + 1];

                // Handle multi-char escapes that need translation
                switch (esc)
                {
                    case 'd':
                        _in += 2;
                        return Emit(DigitInline);
                    case 'w':
                        _in += 2;
                        return Emit(WordInline);
                    case 's':
                        _in += 2;
                        return Emit(WhitespaceInline);
                    case 'p' or 'P':
                        return TranslatePropertyEscape(insideCharClass: true);
                    case 'u':
                    {
                        OperationStatus s = TranslateUnicodeEscape(insideCharClass: true);
                        return s;
                    }

                    case 'x':
                    {
                        // \xHH — pass through
                        int len = Math.Min(4, endPos - _in);
                        OperationStatus s = Emit(_input.Slice(_in, len));
                        _in += len;
                        return s;
                    }

                    case 'c':
                    {
                        int len = Math.Min(3, endPos - _in);
                        OperationStatus s = Emit(_input.Slice(_in, len));
                        _in += len;
                        return s;
                    }

                    default:
                    {
                        // Validate identity escapes in /u mode inside class
                        if (esc is '^' or '$' or '.' or '*' or '+' or '?' or '(' or ')'
                            or '[' or ']' or '{' or '}' or '|' or '\\' or '/'
                            or '-'
                            or 't' or 'n' or 'r' or 'f' or 'v'
                            or '0')
                        {
                            OperationStatus s = Emit(_input.Slice(_in, 2));
                            _in += 2;
                            return s;
                        }

                        return OperationStatus.InvalidData;
                    }
                }
            }
            else if (char.IsHighSurrogate(_input[_in]) && _in + 1 < endPos && char.IsLowSurrogate(_input[_in + 1]))
            {
                // Literal surrogate pair inside character class — emit as explicit surrogate pair escapes
                int cp = char.ConvertToUtf32(_input[_in], _input[_in + 1]);
                _in += 2;
                return EmitCodePoint(cp);
            }
            else
            {
                // Literal character
                OperationStatus s = Emit(_input[_in]);
                _in++;
                return s;
            }
        }

        /// <summary>
        /// Advances _in past one class atom without emitting anything.
        /// </summary>
        private void SkipOneClassAtom(int endPos)
        {
            if (_input[_in] == '\\' && _in + 1 < endPos)
            {
                char esc = _input[_in + 1];
                _in += esc switch
                {
                    'p' or 'P' => SkipPropertyEscape(_in, endPos),
                    'u' => SkipUnicodeEscape(_in, endPos),
                    'x' => Math.Min(4, endPos - _in),
                    'c' => Math.Min(3, endPos - _in),
                    _ => 2,
                };
            }
            else if (char.IsHighSurrogate(_input[_in]) && _in + 1 < endPos && char.IsLowSurrogate(_input[_in + 1]))
            {
                _in += 2;
            }
            else
            {
                _in++;
            }
        }

        private OperationStatus EmitAlternationCharClass(in CharClassInfo info)
        {
            int contentStart = _in;
            bool needsGroup = info.HasPositiveParts
                || ((info.HasNegD ? 1 : 0) + (info.HasNegW ? 1 : 0) + (info.HasNegS ? 1 : 0)) > 1;

            OperationStatus s;

            if (needsGroup)
            {
                s = Emit("(?:");
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            bool needsSep = false;

            if (info.HasPositiveParts)
            {
                s = Emit('[');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                _in = contentStart;
                s = EmitCharClassContent(info.EndPos, skipNegShorthands: true);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(']');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needsSep = true;
            }

            if (info.HasNegD)
            {
                if (needsSep)
                {
                    s = Emit('|');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }

                s = Emit("[^0-9]");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needsSep = true;
            }

            if (info.HasNegW)
            {
                if (needsSep)
                {
                    s = Emit('|');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }

                s = Emit("[^a-zA-Z0-9_]");
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needsSep = true;
            }

            if (info.HasNegS)
            {
                if (needsSep)
                {
                    s = Emit('|');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }

                s = Emit(NotWhitespaceClass);
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            if (needsGroup)
            {
                s = Emit(')');
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            _in = info.EndPos + 1;
            return OperationStatus.Done;
        }

        private OperationStatus EmitSubtractionCharClass(in CharClassInfo info)
        {
            int contentStart = _in;

            // Compute base set: intersection of complements of negated shorthands
            // NOT(\D) = [0-9], NOT(\W) = [a-zA-Z0-9_], NOT(\S) = [whitespace]
            // Intersections involving \S with either \D or \W yield empty set
            if ((info.HasNegD && info.HasNegS) || (info.HasNegW && info.HasNegS))
            {
                _in = info.EndPos + 1;
                return Emit("[^\\s\\S]");
            }

            string baseSet;
            if (info.HasNegD || (info.HasNegD && info.HasNegW))
            {
                baseSet = "0-9";
            }
            else if (info.HasNegW)
            {
                baseSet = "a-zA-Z0-9_";
            }
            else
            {
                // HasNegS only
                baseSet = WhitespaceInline;
            }

            OperationStatus s;

            if (!info.HasPositiveParts)
            {
                s = Emit('[');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(baseSet);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(']');
                _in = info.EndPos + 1;
                return s;
            }

            // [baseSet-[positive_parts_expanded]]
            s = Emit('[');
            if (s != OperationStatus.Done)
            {
                return s;
            }

            s = Emit(baseSet);
            if (s != OperationStatus.Done)
            {
                return s;
            }

            s = Emit("-[");
            if (s != OperationStatus.Done)
            {
                return s;
            }

            _in = contentStart;
            s = EmitCharClassContent(info.EndPos, skipNegShorthands: true);
            if (s != OperationStatus.Done)
            {
                return s;
            }

            s = Emit("]]");
            _in = info.EndPos + 1;
            return s;
        }

        private OperationStatus EmitSupplementaryCharClass(bool isNegated, in CharClassInfo info)
        {
            int contentStart = _in;
            int endPos = info.EndPos;

            // Phase 1: Collect supplementary items
            // Each item is a (startCp, endCp) pair; start == end for individuals
            Span<int> suppCps = stackalloc int[32];  // code point pairs
            Span<int> suppPos = stackalloc int[32];  // input position pairs
            int suppCount = 0;
            bool hasBmp = false;

            int pos = contentStart;
            while (pos < endPos)
            {
                if (_input[pos] == '\\' && pos + 1 < endPos
                    && _input[pos + 1] == 'u' && pos + 2 < endPos
                    && _input[pos + 2] == '{')
                {
                    int escStart = pos;
                    int hexStart = pos + 3;
                    int hexEnd = hexStart;
                    while (hexEnd < endPos && _input[hexEnd] != '}')
                    {
                        hexEnd++;
                    }

                    if (TryParseHex(_input[hexStart..hexEnd], out int cp) && cp > 0xFFFF)
                    {
                        int startCp = cp;
                        int endCp = cp;
                        int escEnd = hexEnd + 1;

                        // Check for supplementary-to-supplementary range
                        if (escEnd < endPos && _input[escEnd] == '-'
                            && escEnd + 1 < endPos && _input[escEnd + 1] == '\\'
                            && escEnd + 2 < endPos && _input[escEnd + 2] == 'u'
                            && escEnd + 3 < endPos && _input[escEnd + 3] == '{')
                        {
                            int rStart = escEnd + 4;
                            int rEnd = rStart;
                            while (rEnd < endPos && _input[rEnd] != '}')
                            {
                                rEnd++;
                            }

                            if (TryParseHex(_input[rStart..rEnd], out int endCpVal)
                                && endCpVal > 0xFFFF)
                            {
                                endCp = endCpVal;
                                escEnd = rEnd + 1;
                            }
                        }

                        int idx = suppCount * 2;
                        if (idx + 1 < suppCps.Length)
                        {
                            suppCps[idx] = startCp;
                            suppCps[idx + 1] = endCp;
                            suppPos[idx] = escStart;
                            suppPos[idx + 1] = escEnd;
                            suppCount++;
                        }

                        pos = escEnd;
                        continue;
                    }

                    // BMP \u{...} escape
                    hasBmp = true;
                    pos = hexEnd + 1;
                    continue;
                }

                // Check for literal surrogate pair (non-BMP character in UTF-16)
                if (!(_input[pos] == '\\') && char.IsHighSurrogate(_input[pos])
                    && pos + 1 < endPos && char.IsLowSurrogate(_input[pos + 1]))
                {
                    int litStart = pos;
                    int startCp = char.ConvertToUtf32(_input[pos], _input[pos + 1]);
                    int endCp = startCp;
                    int litEnd = pos + 2;

                    // Check for literal-pair - literal-pair range
                    if (litEnd < endPos && _input[litEnd] == '-'
                        && litEnd + 1 < endPos && char.IsHighSurrogate(_input[litEnd + 1])
                        && litEnd + 2 < endPos && char.IsLowSurrogate(_input[litEnd + 2]))
                    {
                        int rangeCp = char.ConvertToUtf32(_input[litEnd + 1], _input[litEnd + 2]);
                        if (rangeCp > 0xFFFF)
                        {
                            endCp = rangeCp;
                            litEnd += 3;
                        }
                    }

                    int idx = suppCount * 2;
                    if (idx + 1 < suppCps.Length)
                    {
                        suppCps[idx] = startCp;
                        suppCps[idx + 1] = endCp;
                        suppPos[idx] = litStart;
                        suppPos[idx + 1] = litEnd;
                        suppCount++;
                    }

                    pos = litEnd;
                    continue;
                }

                // All other content is BMP
                hasBmp = true;
                if (_input[pos] == '\\' && pos + 1 < endPos)
                {
                    char esc = _input[pos + 1];
                    pos += esc switch
                    {
                        'p' or 'P' => SkipPropertyEscape(pos, endPos),
                        'u' => SkipUnicodeEscape(pos, endPos),
                        'x' => Math.Min(4, endPos - pos),
                        'c' => Math.Min(3, endPos - pos),
                        _ => 2,
                    };
                }
                else
                {
                    pos++;
                }
            }

            // Phase 2: Emit
            // For negated: (?!<positive_pattern>)<any_codepoint>
            // For non-negated: <positive_pattern>
            bool needGroup = (hasBmp && suppCount > 0) || suppCount > 1;
            OperationStatus s;

            if (isNegated)
            {
                s = Emit("(?!");
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            if (needGroup)
            {
                s = Emit("(?:");
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            bool needSep = false;

            if (hasBmp)
            {
                s = Emit('[');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                // Emit BMP content in segments between supplementary items
                int segStart = contentStart;
                for (int i = 0; i < suppCount; i++)
                {
                    int suppStart = suppPos[i * 2];
                    int suppEnd = suppPos[(i * 2) + 1];

                    if (segStart < suppStart)
                    {
                        _in = segStart;
                        s = EmitCharClassContent(suppStart, skipNegShorthands: false);
                        if (s != OperationStatus.Done)
                        {
                            return s;
                        }
                    }

                    segStart = suppEnd;
                }

                if (segStart < endPos)
                {
                    _in = segStart;
                    s = EmitCharClassContent(endPos, skipNegShorthands: false);
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }

                s = Emit(']');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needSep = true;
            }

            for (int i = 0; i < suppCount; i++)
            {
                if (needSep)
                {
                    s = Emit('|');
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }

                s = EmitSurrogateRange(suppCps[i * 2], suppCps[(i * 2) + 1]);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                needSep = true;
            }

            if (needGroup)
            {
                s = Emit(')');
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            if (isNegated)
            {
                s = Emit(')');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit(AnyCodePoint);
                if (s != OperationStatus.Done)
                {
                    return s;
                }
            }

            _in = endPos + 1;
            return OperationStatus.Done;
        }

        private OperationStatus EmitSurrogateRange(int startCp, int endCp)
        {
            int sAdj = startCp - 0x10000;
            char sHi = (char)(0xD800 + (sAdj >> 10));
            char sLo = (char)(0xDC00 + (sAdj & 0x3FF));

            int eAdj = endCp - 0x10000;
            char eHi = (char)(0xD800 + (eAdj >> 10));
            char eLo = (char)(0xDC00 + (eAdj & 0x3FF));

            if (startCp == endCp)
            {
                // Single supplementary character
                OperationStatus s = EmitUnicodeEscape(sHi);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return EmitUnicodeEscape(sLo);
            }

            if (sHi == eHi)
            {
                // Same high surrogate: \uHH[\uLL1-\uLL2]
                OperationStatus s = EmitUnicodeEscape(sHi);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit('[');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = EmitUnicodeEscape(sLo);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = Emit('-');
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                s = EmitUnicodeEscape(eLo);
                if (s != OperationStatus.Done)
                {
                    return s;
                }

                return Emit(']');
            }

            // Different high surrogates: multi-part alternation
            OperationStatus st;
            st = Emit("(?:");
            if (st != OperationStatus.Done)
            {
                return st;
            }

            // Part 1: sHi[sLo-\uDFFF]
            st = EmitUnicodeEscape(sHi);
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit('[');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = EmitUnicodeEscape(sLo);
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit('-');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = EmitUnicodeEscape('\uDFFF');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit(']');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            // Part 2: middle high surrogates with [\uDC00-\uDFFF]
            if (sHi + 1 <= eHi - 1)
            {
                st = Emit('|');
                if (st != OperationStatus.Done)
                {
                    return st;
                }

                char midStart = (char)(sHi + 1);
                char midEnd = (char)(eHi - 1);

                if (midStart == midEnd)
                {
                    st = EmitUnicodeEscape(midStart);
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }
                }
                else
                {
                    st = Emit('[');
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }

                    st = EmitUnicodeEscape(midStart);
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }

                    st = Emit('-');
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }

                    st = EmitUnicodeEscape(midEnd);
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }

                    st = Emit(']');
                    if (st != OperationStatus.Done)
                    {
                        return st;
                    }
                }

                st = Emit('[');
                if (st != OperationStatus.Done)
                {
                    return st;
                }

                st = EmitUnicodeEscape('\uDC00');
                if (st != OperationStatus.Done)
                {
                    return st;
                }

                st = Emit('-');
                if (st != OperationStatus.Done)
                {
                    return st;
                }

                st = EmitUnicodeEscape('\uDFFF');
                if (st != OperationStatus.Done)
                {
                    return st;
                }

                st = Emit(']');
                if (st != OperationStatus.Done)
                {
                    return st;
                }
            }

            // Part 3: eHi[\uDC00-eLo]
            st = Emit('|');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = EmitUnicodeEscape(eHi);
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit('[');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = EmitUnicodeEscape('\uDC00');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit('-');
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = EmitUnicodeEscape(eLo);
            if (st != OperationStatus.Done)
            {
                return st;
            }

            st = Emit("])");
            return st;
        }

        private int SkipPropertyEscape(int pos, int endPos)
        {
            // Skip \p{...} or \P{...}: returns total length to advance past
            if (pos + 2 < endPos && _input[pos + 2] == '{')
            {
                int p = pos + 3;
                while (p < endPos && _input[p] != '}')
                {
                    p++;
                }

                return p < endPos ? (p + 1 - pos) : (endPos - pos);
            }

            return 2;
        }

        private int SkipUnicodeEscape(int pos, int endPos)
        {
            // Skip \u{XXXX...} or \uXXXX: returns total length
            if (pos + 2 < endPos && _input[pos + 2] == '{')
            {
                int p = pos + 3;
                while (p < endPos && _input[p] != '}')
                {
                    p++;
                }

                return p < endPos ? (p + 1 - pos) : (endPos - pos);
            }

            return Math.Min(6, endPos - pos);
        }

        private OperationStatus EmitCharClassContent(int endPos, bool skipNegShorthands)
        {
            while (_in < endPos)
            {
                if (_input[_in] == '\\' && _in + 1 < endPos)
                {
                    char next = _input[_in + 1];
                    OperationStatus s;

                    switch (next)
                    {
                        case 'D' or 'W' or 'S':
                            if (skipNegShorthands)
                            {
                                _in += 2;
                                break;
                            }

                            // Should not occur in simple path; emit as-is for resilience
                            s = Emit('\\');
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            s = Emit(next);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;

                        case 'd':
                            s = Emit(DigitInline);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;

                        case 'w':
                            s = Emit(WordInline);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;

                        case 's':
                            s = Emit(WhitespaceInline);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;

                        case 'p' or 'P':
                            s = TranslatePropertyEscape(insideCharClass: true);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            break;

                        case 'u':
                            s = TranslateUnicodeEscape(insideCharClass: true);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            break;

                        case 'b':
                            // \b inside character class = backspace (same in .NET)
                            s = Emit("\\b");
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;

                        case 'x':
                        {
                            if (_in + 4 > _input.Length)
                            {
                                return OperationStatus.InvalidData;
                            }

                            s = Emit(_input.Slice(_in, 4));
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 4;
                            break;
                        }

                        case 'c':
                        {
                            if (_in + 3 > _input.Length)
                            {
                                return OperationStatus.InvalidData;
                            }

                            char controlLetter = _input[_in + 2];
                            if (controlLetter is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
                            {
                                return OperationStatus.InvalidData;
                            }

                            s = Emit(_input.Slice(_in, 3));
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 3;
                            break;
                        }

                        case '0':
                        {
                            // ECMAScript /u mode: \0 inside a character class must NOT
                            // be followed by a decimal digit (octal escapes are forbidden).
                            if (_in + 2 < endPos && _input[_in + 2] is >= '0' and <= '9')
                            {
                                return OperationStatus.InvalidData;
                            }

                            s = Emit("\\0");
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;
                        }

                        default:
                        {
                            // In /u mode inside character class, valid escapes reaching
                            // the default are: control escapes (t,n,r,f,v), identity
                            // escapes of SyntaxCharacter, /, and -.
                            if (next is not ('t' or 'n' or 'r' or 'f' or 'v'
                                or '^' or '$' or '.' or '*' or '+' or '?' or '/' or '-'
                                or '(' or ')' or '[' or ']' or '{' or '}' or '|' or '\\'))
                            {
                                return OperationStatus.InvalidData;
                            }

                            s = Emit('\\');
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            s = Emit(next);
                            if (s != OperationStatus.Done)
                            {
                                return s;
                            }

                            _in += 2;
                            break;
                        }
                    }
                }
                else if (char.IsHighSurrogate(_input[_in]) && _in + 1 < endPos && char.IsLowSurrogate(_input[_in + 1]))
                {
                    int cp = char.ConvertToUtf32(_input[_in], _input[_in + 1]);
                    _in += 2;
                    OperationStatus s = EmitCodePoint(cp);
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }
                }
                else
                {
                    OperationStatus s = Emit(_input[_in]);
                    if (s != OperationStatus.Done)
                    {
                        return s;
                    }

                    _in++;
                }
            }

            return OperationStatus.Done;
        }

        private OperationStatus Emit(char c)
        {
            if (_out >= _output.Length)
            {
                return OperationStatus.DestinationTooSmall;
            }

            _output[_out++] = c;
            return OperationStatus.Done;
        }

        private OperationStatus Emit(ReadOnlySpan<char> text)
        {
            if (_out + text.Length > _output.Length)
            {
                return OperationStatus.DestinationTooSmall;
            }

            text.CopyTo(_output[_out..]);
            _out += text.Length;
            return OperationStatus.Done;
        }
    }
}
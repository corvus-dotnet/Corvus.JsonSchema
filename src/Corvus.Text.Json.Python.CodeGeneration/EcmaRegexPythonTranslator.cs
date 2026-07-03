// <copyright file="EcmaRegexPythonTranslator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Python.CodeGeneration;

/// <summary>
/// Translates an ECMAScript-262 (/u Unicode mode) regular-expression pattern to an equivalent pattern for
/// Python's <c>regex</c> module -- the Python peer of the .NET <c>EcmaRegexTranslator</c>, applied at code
/// generation time so the emitted <c>re_test(...)</c> call receives an already-Python pattern.
/// </summary>
/// <remarks>
/// This is far smaller than the .NET translator because Python's <c>regex</c> module is a full Unicode engine:
/// <c>\p{...}</c>, script properties, and astral code points are native, so the surrogate-pair synthesis and
/// the script / emoji BMP tables the .NET translator needs are unnecessary. Only the genuine ECMA-vs-Python
/// differences are handled: the ASCII <c>\d</c>/<c>\w</c> and ECMA <c>\s</c> / <c>\b</c> shorthands (which
/// Python treats as Unicode), the ECMA <c>.</c> (which excludes the line terminators), <c>\cX</c> control
/// escapes, <c>\u{...}</c>, and combining a <c>\uHHHH\uLLLL</c> surrogate-pair escape into the single astral
/// code point Python matches. <c>\p{...}</c>, literals, back-references, and groups pass through unchanged.
/// </remarks>
internal static class EcmaRegexPythonTranslator
{
    private const string DigitOut = "[0-9]";
    private const string NotDigitOut = "[^0-9]";
    private const string WordOut = "[a-zA-Z0-9_]";
    private const string NotWordOut = "[^a-zA-Z0-9_]";

    // The ECMA-262 WhiteSpace + LineTerminator set (NOT Python's Unicode \s).
    private const string WsInline = @"\x09-\x0d\x20\xa0\u1680\u2000-\u200a\u2028-\u2029\u202f\u205f\u3000\ufeff";
    private const string WsOut = "[" + WsInline + "]";
    private const string NotWsOut = "[^" + WsInline + "]";

    // Inside-character-class expansions. The negated forms are the exact code-point complements (generated,
    // not hand-written) so `[\D]` etc. keep ECMA semantics inside a class where `[^...]` cannot be nested.
    private const string DigitIn = "0-9";
    private const string WordIn = "a-zA-Z0-9_";
    private const string NotDigitIn = @"\x00-\x2f\x3a-\U0010ffff";
    private const string NotWordIn = @"\x00-\x2f\x3a-\x40\x5b-\x5e\x60\x7b-\U0010ffff";
    private const string NotWsIn = @"\x00-\x08\x0e-\x1f\x21-\x9f\xa1-\u167f\u1681-\u1fff\u200b-\u2027\u202a-\u202e\u2030-\u205e\u2060-\u2fff\u3001-\ufefe\uff00-\U0010ffff";

    // ECMA `.` excludes the four line terminators (not just \n).
    private const string DotOut = @"[^\n\r\u2028\u2029]";

    // Word boundaries with the ASCII \w definition (Python's \b uses Unicode \w).
    private const string WordBoundary = @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))";
    private const string NonWordBoundary = @"(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))";

    /// <summary>Translates an ECMA-262 pattern to a Python <c>regex</c>-module pattern.</summary>
    /// <param name="ecma">The ECMA-262 pattern.</param>
    /// <returns>The translated Python pattern.</returns>
    public static string Translate(string ecma)
    {
        var sb = new StringBuilder(ecma.Length + 16);
        int i = 0;
        while (i < ecma.Length)
        {
            char c = ecma[i];
            if (c == '\\' && i + 1 < ecma.Length)
            {
                i = TranslateEscape(ecma, i, sb, inClass: false);
            }
            else if (c == '.')
            {
                sb.Append(DotOut);
                i++;
            }
            else if (c == '[')
            {
                i = TranslateClass(ecma, i, sb);
            }
            else
            {
                i = AppendLiteral(ecma, i, sb);
            }
        }

        return sb.ToString();
    }

    private static int TranslateClass(string s, int i, StringBuilder sb)
    {
        sb.Append('[');
        i++;
        if (i < s.Length && s[i] == '^')
        {
            sb.Append('^');
            i++;
        }

        while (i < s.Length && s[i] != ']')
        {
            i = s[i] == '\\' && i + 1 < s.Length
                ? TranslateEscape(s, i, sb, inClass: true)
                : AppendLiteral(s, i, sb);
        }

        if (i < s.Length)
        {
            sb.Append(']');
            i++;
        }

        return i;
    }

    // Emit a literal atom, combining a UTF-16 surrogate-pair CHARACTER (a literal astral char in the source,
    // e.g. an emoji) into the single astral code point escape Python matches — the peer of the \uHHHH\uLLLL
    // escape-pair combining in TranslateUnicode.
    private static int AppendLiteral(string s, int i, StringBuilder sb)
    {
        char c = s[i];
        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
        {
            AppendCodePoint(sb, char.ConvertToUtf32(c, s[i + 1]));
            return i + 2;
        }

        sb.Append(c);
        return i + 1;
    }

    private static int TranslateEscape(string s, int i, StringBuilder sb, bool inClass)
    {
        char next = s[i + 1];
        switch (next)
        {
            case 'd': sb.Append(inClass ? DigitIn : DigitOut); return i + 2;
            case 'D': sb.Append(inClass ? NotDigitIn : NotDigitOut); return i + 2;
            case 'w': sb.Append(inClass ? WordIn : WordOut); return i + 2;
            case 'W': sb.Append(inClass ? NotWordIn : NotWordOut); return i + 2;
            case 's': sb.Append(inClass ? WsInline : WsOut); return i + 2;
            case 'S': sb.Append(inClass ? NotWsIn : NotWsOut); return i + 2;
            case 'b':
                sb.Append(inClass ? @"\x08" : WordBoundary); // \b inside a class is backspace in ECMA
                return i + 2;
            case 'B':
                sb.Append(inClass ? @"\B" : NonWordBoundary);
                return i + 2;
            case 'c':
                if (i + 2 < s.Length && IsAsciiLetter(s[i + 2]))
                {
                    sb.Append(@"\x").Append((s[i + 2] & 0x1F).ToString("x2", CultureInfo.InvariantCulture));
                    return i + 3;
                }

                sb.Append(@"\c");
                return i + 2;
            case 'u':
                return TranslateUnicode(s, i, sb);
            default:
                sb.Append('\\').Append(next);
                return i + 2;
        }
    }

    private static int TranslateUnicode(string s, int i, StringBuilder sb)
    {
        int j = i + 2; // after \u

        if (j < s.Length && s[j] == '{')
        {
            int close = s.IndexOf('}', j);
            if (close > j && int.TryParse(s.AsSpan((j + 1)..close), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
            {
                AppendCodePoint(sb, cp);
                return close + 1;
            }

            sb.Append(@"\u");
            return i + 2;
        }

        // \uHHHH form: combine a surrogate pair into the single astral code point Python matches; else pass through.
        if (TryParseHex4(s, j, out int hi))
        {
            if (hi is >= 0xD800 and <= 0xDBFF
                && j + 6 < s.Length && s[j + 4] == '\\' && s[j + 5] == 'u'
                && TryParseHex4(s, j + 6, out int lo) && lo is >= 0xDC00 and <= 0xDFFF)
            {
                AppendCodePoint(sb, 0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00));
                return j + 10;
            }

            sb.Append(@"\u").Append(s, j, 4);
            return j + 4;
        }

        sb.Append(@"\u");
        return i + 2;
    }

    private static void AppendCodePoint(StringBuilder sb, int cp)
    {
        sb.Append(cp <= 0xFFFF ? @"\u" : @"\U")
          .Append(cp.ToString(cp <= 0xFFFF ? "x4" : "x8", CultureInfo.InvariantCulture));
    }

    private static bool TryParseHex4(string s, int start, out int value)
    {
        value = 0;
        if (start + 4 > s.Length)
        {
            return false;
        }

        return int.TryParse(s.AsSpan(start, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
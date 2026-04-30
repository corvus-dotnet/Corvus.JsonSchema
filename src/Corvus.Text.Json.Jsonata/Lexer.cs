// <copyright file="Lexer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Corvus.Text;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Tokenizer for JSONata expressions. Produces a stream of <see cref="Token"/> values
/// from a UTF-8 byte source. Follows the reference jsonata-js tokenizer faithfully.
/// </summary>
internal ref struct Lexer
{
    private readonly byte[] source;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> struct.
    /// </summary>
    /// <param name="source">The JSONata expression as UTF-8 bytes.</param>
    public Lexer(byte[] source)
    {
        this.source = source;
        this.position = 0;
    }

    /// <summary>
    /// Gets the current position in the source.
    /// </summary>
    public readonly int Position => this.position;

    /// <summary>
    /// Scans and returns the next token from the source.
    /// </summary>
    /// <param name="prefixMode">
    /// When <c>true</c>, the <c>/</c> character is treated as a division operator rather than
    /// the start of a regex literal. The parser sets this when the previous token could be the
    /// end of an expression (a value or closing bracket).
    /// </param>
    /// <returns>The next token, or a token with <see cref="TokenType.End"/> if the end of input is reached.</returns>
    public Token Next(bool prefixMode = false)
    {
        if (this.position >= this.source.Length)
        {
            return new Token(TokenType.End, "(end)", this.position);
        }

        this.SkipWhitespace();

        if (this.position >= this.source.Length)
        {
            return new Token(TokenType.End, "(end)", this.position);
        }

        // Skip comments (/* ... */)
        if (this.Peek() == (byte)'/' && this.PeekAt(1) == (byte)'*')
        {
            int commentStart = this.position;
            this.position += 2;
            while (this.position < this.source.Length)
            {
                if (this.source[this.position] == (byte)'*' && this.PeekAt(1) == (byte)'/')
                {
                    this.position += 2;
                    return this.Next(prefixMode);
                }

                this.position++;
            }

            throw new JsonataException("S0106", SR.S0106_CommentNotTerminated, commentStart);
        }

        byte c = this.Peek();

        // Regex literal
        if (!prefixMode && c == (byte)'/')
        {
            this.position++;
            return this.ScanRegex();
        }

        // Double-character operators (must check before single-char)
        if (this.position + 1 < this.source.Length)
        {
            byte next = this.source[this.position + 1];
            string? doubleOp = (c, next) switch
            {
                ((byte)'.', (byte)'.') => "..",
                ((byte)':', (byte)'=') => ":=",
                ((byte)'!', (byte)'=') => "!=",
                ((byte)'>', (byte)'=') => ">=",
                ((byte)'<', (byte)'=') => "<=",
                ((byte)'*', (byte)'*') => "**",
                ((byte)'~', (byte)'>') => "~>",
                ((byte)'?', (byte)':') => "?:",
                ((byte)'?', (byte)'?') => "??",
                _ => null,
            };

            if (doubleOp is not null)
            {
                int pos = this.position;
                this.position += 2;
                return new Token(TokenType.Operator, doubleOp, pos);
            }
        }

        // Single-character operators and punctuation
        if (IsOperatorChar(c))
        {
            int pos = this.position;
            this.position++;
            return new Token(TokenType.Operator, ((char)c).ToString(), pos);
        }

        // Characters that are only valid as part of multi-char operators (e.g. ! in !=, ~ in ~>)
        if (c == (byte)'!' || c == (byte)'~')
        {
            throw new JsonataException("S0204", SR.Format(SR.S0204_UnknownOperator, (char)c), this.position, ((char)c).ToString());
        }

        // String literals
        if (c == (byte)'"' || c == (byte)'\'')
        {
            return this.ScanString(c);
        }

        // Numeric literals
        if (c >= (byte)'0' && c <= (byte)'9')
        {
            Token? num = this.TryScanNumber();
            if (num.HasValue)
            {
                return num.Value;
            }
        }

        // Backtick-quoted names
        if (c == (byte)'`')
        {
            return this.ScanBacktickName();
        }

        // Names and variables
        return this.ScanNameOrVariable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly byte Peek() => this.source[this.position];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly byte PeekAt(int offset)
    {
        int idx = this.position + offset;
        return idx < this.source.Length ? this.source[idx] : (byte)0;
    }

    private void SkipWhitespace()
    {
        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\n' || c == (byte)'\r' || c == 0x0B /* \v */)
            {
                this.position++;
            }
            else
            {
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOperatorChar(byte c)
    {
        return c switch
        {
            (byte)'.' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'(' or (byte)')' or (byte)',' or
            (byte)'@' or (byte)'#' or (byte)';' or (byte)':' or (byte)'?' or (byte)'+' or (byte)'-' or (byte)'*' or
            (byte)'/' or (byte)'%' or (byte)'|' or (byte)'=' or (byte)'<' or (byte)'>' or (byte)'^' or (byte)'&' => true,
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameStopChar(byte c)
    {
        return c == (byte)' ' || c == (byte)'\t' || c == (byte)'\n' || c == (byte)'\r' || c == 0x0B /* \v */
            || IsOperatorChar(c) || c == (byte)'!' || c == (byte)'~';
    }

    private Token ScanString(byte quote)
    {
        int start = this.position;
        this.position++; // skip opening quote

        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];

            if (c == (byte)'\\')
            {
                this.position++;
                if (this.position >= this.source.Length)
                {
                    sb.Dispose();
                    throw new JsonataException("S0101", SR.S0101_StringLiteralNotTerminated, start);
                }

                c = this.source[this.position];
                switch (c)
                {
                    case (byte)'"': sb.Append((byte)'"'); break;
                    case (byte)'\\': sb.Append((byte)'\\'); break;
                    case (byte)'/': sb.Append((byte)'/'); break;
                    case (byte)'b': sb.Append((byte)'\b'); break;
                    case (byte)'f': sb.Append((byte)'\f'); break;
                    case (byte)'n': sb.Append((byte)'\n'); break;
                    case (byte)'r': sb.Append((byte)'\r'); break;
                    case (byte)'t': sb.Append((byte)'\t'); break;
                    case (byte)'u':
                        if (this.position + 4 >= this.source.Length)
                        {
                            sb.Dispose();
                            throw new JsonataException("S0104", SR.S0104_InvalidUnicodeEscape, this.position);
                        }

                        // Parse the 4 hex digits from UTF-8 bytes (all ASCII hex chars)
                        ReadOnlySpan<byte> hexBytes = this.source.AsSpan(this.position + 1, 4);
                        if (TryParseHexCodepoint(hexBytes, out int codepoint))
                        {
                            this.position += 4;

                            // Handle surrogate pairs: high surrogate must be followed by \uXXXX low surrogate
                            if (codepoint >= 0xD800 && codepoint <= 0xDBFF)
                            {
                                if (this.position + 6 < this.source.Length
                                    && this.source[this.position + 1] == (byte)'\\'
                                    && this.source[this.position + 2] == (byte)'u'
                                    && TryParseHexCodepoint(this.source.AsSpan(this.position + 3, 4), out int low)
                                    && low >= 0xDC00 && low <= 0xDFFF)
                                {
                                    codepoint = 0x10000 + ((codepoint - 0xD800) << 10) + (low - 0xDC00);
                                    this.position += 6;
                                }
                            }

                            AppendCodepoint(ref sb, codepoint);
                        }
                        else
                        {
                            sb.Dispose();
                            throw new JsonataException("S0104", SR.S0104_InvalidUnicodeEscape, this.position);
                        }

                        break;
                    default:
                        sb.Dispose();
                        throw new JsonataException("S0103", SR.Format(SR.S0103_IllegalEscapeSequence, (char)c), this.position, ((char)c).ToString());
                }
            }
            else if (c == quote)
            {
                this.position++;

                // String token values are materialized since StringNode.Value is always used as string
#if NETSTANDARD2_0
                string value = Encoding.UTF8.GetString(sb.AsSpan().ToArray(), 0, sb.Length);
#else
                string value = Encoding.UTF8.GetString(sb.AsSpan());
#endif
                sb.Dispose();
                return new Token(TokenType.String, value, start);
            }
            else
            {
                sb.Append(c);
            }

            this.position++;
        }

        sb.Dispose();
        throw new JsonataException("S0101", SR.S0101_StringLiteralNotTerminated, start);
    }

    private Token? TryScanNumber()
    {
        int start = this.position;

        // Match the JSON number regex: -?(0|([1-9][0-9]*))(\.[0-9]+)?([Ee][-+]?[0-9]+)?
        int i = start;
        if (i < this.source.Length && this.source[i] == (byte)'-')
        {
            i++;
        }

        if (i >= this.source.Length || this.source[i] < (byte)'0' || this.source[i] > (byte)'9')
        {
            return null;
        }

        if (this.source[i] == (byte)'0')
        {
            i++;
        }
        else
        {
            while (i < this.source.Length && this.source[i] >= (byte)'0' && this.source[i] <= (byte)'9')
            {
                i++;
            }
        }

        if (i < this.source.Length && this.source[i] == (byte)'.'
            && (i + 1 >= this.source.Length || this.source[i + 1] != (byte)'.'))
        {
            i++;
            int fracStart = i;
            while (i < this.source.Length && this.source[i] >= (byte)'0' && this.source[i] <= (byte)'9')
            {
                i++;
            }

            if (i == fracStart)
            {
                // No digits after '.', not a valid number
                return null;
            }
        }

        if (i < this.source.Length && (this.source[i] == (byte)'e' || this.source[i] == (byte)'E'))
        {
            i++;
            if (i < this.source.Length && (this.source[i] == (byte)'+' || this.source[i] == (byte)'-'))
            {
                i++;
            }

            int expStart = i;
            while (i < this.source.Length && this.source[i] >= (byte)'0' && this.source[i] <= (byte)'9')
            {
                i++;
            }

            if (i == expStart)
            {
                return null;
            }
        }

        ReadOnlySpan<byte> numSpan = this.source.AsSpan(start, i - start);

        if (Utf8Parser.TryParse(numSpan, out double value, out _)
            && !double.IsNaN(value)
            && !double.IsInfinity(value))
        {
            this.position = i;
            return new Token(TokenType.Number, start, start, i - start) { NumericValue = value };
        }

#if NETSTANDARD2_0
        string numStr = Encoding.UTF8.GetString(numSpan.ToArray(), 0, numSpan.Length);
#else
        string numStr = Encoding.UTF8.GetString(numSpan);
#endif
        throw new JsonataException("S0102", SR.Format(SR.S0102_InvalidNumber, numStr), start, numStr);
    }

    private Token ScanBacktickName()
    {
        int start = this.position;
        this.position++; // skip opening backtick

        int end = this.source.AsSpan(this.position).IndexOf((byte)'`');
        if (end == -1)
        {
            this.position = this.source.Length;
            throw new JsonataException("S0105", SR.S0105_BacktickQuotedNameNotTerminated, start);
        }

        end += this.position; // convert relative to absolute

        // Semantic value is between the backticks (excludes them)
        int valueOffset = this.position;
        int valueLength = end - this.position;
        this.position = end + 1;
        return new Token(TokenType.Name, start, valueOffset, valueLength);
    }

    private Token ScanNameOrVariable()
    {
        int start = this.position;
        int i = this.position;

        while (i < this.source.Length && !IsNameStopChar(this.source[i]))
        {
            i++;
        }

        int length = i - start;
        this.position = i;

        if (this.source[start] == (byte)'$')
        {
            // Variable reference — value excludes the '$'
            return new Token(TokenType.Variable, start, start + 1, length - 1);
        }

        ReadOnlySpan<byte> span = this.source.AsSpan(start, length);

        if (span.SequenceEqual("or"u8) || span.SequenceEqual("in"u8) || span.SequenceEqual("and"u8))
        {
#if NETSTANDARD2_0
            return new Token(TokenType.Operator, Encoding.UTF8.GetString(this.source, start, length), start);
#else
            return new Token(TokenType.Operator, Encoding.UTF8.GetString(span), start);
#endif
        }

        if (span.SequenceEqual("true"u8))
        {
            return new Token(TokenType.Value, "true", start);
        }

        if (span.SequenceEqual("false"u8))
        {
            return new Token(TokenType.Value, "false", start);
        }

        if (span.SequenceEqual("null"u8))
        {
            return new Token(TokenType.Value, "null", start);
        }

        // Non-keyword name: store offset/length, no allocation
        return new Token(TokenType.Name, start, start, length);
    }

    private Token ScanRegex()
    {
        // The leading '/' has already been consumed
        int start = this.position;
        int depth = 0;

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];

            // Check for closing '/' at depth 0
            if (c == (byte)'/' && depth == 0)
            {
                // Count preceding backslashes
                int backslashes = 0;
                while (this.position - (backslashes + 1) >= start
                    && this.source[this.position - (backslashes + 1)] == (byte)'\\')
                {
                    backslashes++;
                }

                if (backslashes % 2 == 0)
                {
                    // End of regex — pattern and flags are ASCII, safe to convert directly
#if NETSTANDARD2_0
                    string pattern = Encoding.UTF8.GetString(this.source, start, this.position - start);
#else
                    string pattern = Encoding.UTF8.GetString(this.source.AsSpan(start, this.position - start));
#endif
                    if (pattern.Length == 0)
                    {
                        throw new JsonataException("S0301", SR.S0301_EmptyRegularExpression, this.position);
                    }

                    this.position++;

                    // Scan flags
                    int flagStart = this.position;
                    while (this.position < this.source.Length
                        && (this.source[this.position] == (byte)'i' || this.source[this.position] == (byte)'m'))
                    {
                        this.position++;
                    }

#if NETSTANDARD2_0
                    string flags = Encoding.UTF8.GetString(this.source, flagStart, this.position - flagStart);
#else
                    string flags = Encoding.UTF8.GetString(this.source.AsSpan(flagStart, this.position - flagStart));
#endif

                    return new Token(TokenType.Regex, pattern, start - 1)
                    {
                        RegexPattern = pattern,
                        RegexFlags = flags,
                    };
                }
            }

            if ((c == (byte)'(' || c == (byte)'[' || c == (byte)'{') && (this.position == start || this.source[this.position - 1] != (byte)'\\'))
            {
                depth++;
            }

            if ((c == (byte)')' || c == (byte)']' || c == (byte)'}') && (this.position == start || this.source[this.position - 1] != (byte)'\\'))
            {
                depth--;
            }

            this.position++;
        }

        throw new JsonataException("S0302", SR.S0302_RegularExpressionNotTerminated, this.position);
    }

    private static bool TryParseHexCodepoint(ReadOnlySpan<byte> hexBytes, out int codepoint)
    {
        codepoint = 0;
        if (hexBytes.Length != 4)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            byte b = hexBytes[i];
            int digit;
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                digit = b - (byte)'0';
            }
            else if (b >= (byte)'a' && b <= (byte)'f')
            {
                digit = 10 + b - (byte)'a';
            }
            else if (b >= (byte)'A' && b <= (byte)'F')
            {
                digit = 10 + b - (byte)'A';
            }
            else
            {
                return false;
            }

            codepoint = (codepoint << 4) | digit;
        }

        return true;
    }

    private static void AppendCodepoint(ref Utf8ValueStringBuilder sb, int codepoint)
    {
        if (codepoint <= 0x7F)
        {
            sb.Append((byte)codepoint);
        }
        else if (codepoint <= 0x7FF)
        {
            sb.Append((byte)(0xC0 | (codepoint >> 6)));
            sb.Append((byte)(0x80 | (codepoint & 0x3F)));
        }
        else if (codepoint <= 0xFFFF)
        {
            sb.Append((byte)(0xE0 | (codepoint >> 12)));
            sb.Append((byte)(0x80 | ((codepoint >> 6) & 0x3F)));
            sb.Append((byte)(0x80 | (codepoint & 0x3F)));
        }
        else
        {
            sb.Append((byte)(0xF0 | (codepoint >> 18)));
            sb.Append((byte)(0x80 | ((codepoint >> 12) & 0x3F)));
            sb.Append((byte)(0x80 | ((codepoint >> 6) & 0x3F)));
            sb.Append((byte)(0x80 | (codepoint & 0x3F)));
        }
    }
}
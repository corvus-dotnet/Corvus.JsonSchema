// <copyright file="Lexer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// A UTF-8 lexer for JMESPath expressions. Operates directly on byte spans
/// with zero string allocation for identifiers and literals.
/// </summary>
internal ref struct Lexer
{
    private readonly ReadOnlySpan<byte> source;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> struct.
    /// </summary>
    /// <param name="source">The UTF-8 encoded JMESPath expression.</param>
    public Lexer(ReadOnlySpan<byte> source)
    {
        this.source = source;
        this.position = 0;
    }

    /// <summary>
    /// Reads the next token from the source.
    /// </summary>
    /// <returns>The next token.</returns>
    /// <exception cref="JMESPathException">Thrown on invalid syntax.</exception>
    public Token NextToken()
    {
        this.SkipWhitespace();

        if (this.position >= this.source.Length)
        {
            return new Token(TokenType.Eof, this.position);
        }

        byte c = this.source[this.position];

        return c switch
        {
            (byte)'.' => new Token(TokenType.Dot, this.position++),
            (byte)'*' => new Token(TokenType.Star, this.position++),
            (byte)']' => new Token(TokenType.RightBracket, this.position++),
            (byte)')' => new Token(TokenType.RightParen, this.position++),
            (byte)'}' => new Token(TokenType.RightBrace, this.position++),
            (byte)',' => new Token(TokenType.Comma, this.position++),
            (byte)':' => new Token(TokenType.Colon, this.position++),
            (byte)'@' => new Token(TokenType.At, this.position++),
            (byte)'(' => new Token(TokenType.LeftParen, this.position++),
            (byte)'{' => new Token(TokenType.LeftBrace, this.position++),
            (byte)'&' => this.ScanAmpersand(),
            (byte)'|' => this.ScanPipe(),
            (byte)'!' => this.ScanNot(),
            (byte)'<' => this.ScanLessThan(),
            (byte)'>' => this.ScanGreaterThan(),
            (byte)'=' => this.ScanEqual(),
            (byte)'[' => this.ScanLeftBracket(),
            (byte)'\'' => this.ScanRawString(),
            (byte)'"' => this.ScanQuotedIdentifier(),
            (byte)'`' => this.ScanLiteral(),
            (byte)'-' => this.ScanNumber(),
            >= (byte)'0' and <= (byte)'9' => this.ScanNumber(),
            >= (byte)'a' and <= (byte)'z' => this.ScanIdentifier(),
            >= (byte)'A' and <= (byte)'Z' => this.ScanIdentifier(),
            (byte)'_' => this.ScanIdentifier(),
            _ => throw new JMESPathException(
                $"Unexpected character '{(char)c}' at position {this.position}."),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipWhitespace()
    {
        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
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
    private byte Peek()
    {
        return this.position < this.source.Length ? this.source[this.position] : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte PeekAt(int offset)
    {
        int idx = this.position + offset;
        return idx < this.source.Length ? this.source[idx] : (byte)0;
    }

    private Token ScanIdentifier()
    {
        int start = this.position;
        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if ((c >= (byte)'a' && c <= (byte)'z')
                || (c >= (byte)'A' && c <= (byte)'Z')
                || (c >= (byte)'0' && c <= (byte)'9')
                || c == (byte)'_')
            {
                this.position++;
            }
            else
            {
                break;
            }
        }

        return new Token(TokenType.Identifier, start, start, this.position - start);
    }

    private Token ScanNumber()
    {
        int start = this.position;
        bool negative = false;

        if (this.source[this.position] == (byte)'-')
        {
            negative = true;
            this.position++;
            if (this.position >= this.source.Length
                || this.source[this.position] < (byte)'0'
                || this.source[this.position] > (byte)'9')
            {
                throw new JMESPathException(
                    $"Expected digit after '-' at position {start}.");
            }
        }

        int value = 0;
        while (this.position < this.source.Length
            && this.source[this.position] >= (byte)'0'
            && this.source[this.position] <= (byte)'9')
        {
            value = (value * 10) + (this.source[this.position] - '0');
            this.position++;
        }

        return new Token(TokenType.Number, start, negative ? -value : value);
    }

    private Token ScanRawString()
    {
        // Opening single quote already at this.position
        int start = this.position;
        this.position++; // skip opening '

        int contentStart = this.position;
        bool hasEscapes = false;

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)'\'')
            {
                if (!hasEscapes)
                {
                    // Fast path: no escapes, reference source directly
                    Token token = new(TokenType.RawString, start, contentStart, this.position - contentStart);
                    this.position++; // skip closing '
                    return token;
                }

                // Had escapes: use materialized value
                break;
            }

            if (c == (byte)'\\')
            {
                this.position++; // skip backslash
                if (this.position >= this.source.Length)
                {
                    throw new JMESPathException(
                        $"Unexpected end of expression in raw string starting at position {start}.");
                }

                // In raw strings, only \' is a meaningful escape.
                // All other backslash sequences (including \\) are preserved literally.
                byte next = this.source[this.position];
                if (next == (byte)'\'')
                {
                    hasEscapes = true;
                }
            }

            this.position++;
        }

        if (this.position >= this.source.Length)
        {
            throw new JMESPathException(
                $"Unterminated raw string starting at position {start}.");
        }

        // Materialized path: rebuild without escapes
        return this.MaterializeRawString(start, contentStart);
    }

    private Token MaterializeRawString(int start, int contentStart)
    {
        // Reset position and re-scan, building materialized value
        this.position = contentStart;
        byte[]? rented = null;
        int len = 0;
        Span<byte> buffer = stackalloc byte[256];

        while (this.position < this.source.Length && this.source[this.position] != (byte)'\'')
        {
            byte c = this.source[this.position];
            if (c == (byte)'\\')
            {
                this.position++;
                byte next = this.source[this.position];
                if (next == (byte)'\'')
                {
                    // Escaped single quote: skip backslash, emit quote
                    c = next;
                }
                else
                {
                    // Everything else (including \\): emit literal backslash then the character
                    GrowBuffer(ref buffer, ref rented, ref len);
                    buffer[len++] = (byte)'\\';
                    c = next;
                }
            }

            if (len >= buffer.Length)
            {
                byte[] newRented = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                buffer.Slice(0, len).CopyTo(newRented);
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }

                rented = newRented;
                buffer = rented;
            }

            buffer[len++] = c;
            this.position++;
        }

        byte[] result = buffer.Slice(0, len).ToArray();
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        this.position++; // skip closing '
        return new Token(TokenType.RawString, start, result);
    }

    private Token ScanQuotedIdentifier()
    {
        // Opening double quote at this.position
        int start = this.position;
        this.position++; // skip opening "

        int contentStart = this.position;
        bool hasEscapes = false;

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)'"')
            {
                if (!hasEscapes)
                {
                    Token token = new(TokenType.QuotedIdentifier, start, contentStart, this.position - contentStart);
                    this.position++; // skip closing "
                    return token;
                }

                break;
            }

            if (c == (byte)'\\')
            {
                hasEscapes = true;
                this.position++; // skip past escape sequence
                if (this.position >= this.source.Length)
                {
                    throw new JMESPathException(
                        $"Unexpected end of expression in quoted identifier starting at position {start}.");
                }
            }

            this.position++;
        }

        if (this.position >= this.source.Length)
        {
            throw new JMESPathException(
                $"Unterminated quoted identifier starting at position {start}.");
        }

        return this.MaterializeQuotedIdentifier(start, contentStart);
    }

    private Token MaterializeQuotedIdentifier(int start, int contentStart)
    {
        // JSON string escapes: \", \\, \/, \b, \f, \n, \r, \t, \uXXXX
        this.position = contentStart;
        byte[]? rented = null;
        int len = 0;
        Span<byte> buffer = stackalloc byte[256];

        while (this.position < this.source.Length && this.source[this.position] != (byte)'"')
        {
            byte c = this.source[this.position];
            if (c == (byte)'\\')
            {
                this.position++;
                byte esc = this.source[this.position];
                switch (esc)
                {
                    case (byte)'"': c = (byte)'"'; break;
                    case (byte)'\\': c = (byte)'\\'; break;
                    case (byte)'/': c = (byte)'/'; break;
                    case (byte)'b': c = (byte)'\b'; break;
                    case (byte)'f': c = (byte)'\f'; break;
                    case (byte)'n': c = (byte)'\n'; break;
                    case (byte)'r': c = (byte)'\r'; break;
                    case (byte)'t': c = (byte)'\t'; break;
                    case (byte)'u':
                        AppendUnicodeEscape(this.source, ref this.position, ref buffer, ref rented, ref len);
                        continue;
                    default:
                        throw new JMESPathException(
                            $"Invalid escape '\\{(char)esc}' in quoted identifier at position {this.position - 1}.");
                }
            }

            GrowBuffer(ref buffer, ref rented, ref len);
            buffer[len++] = c;
            this.position++;
        }

        byte[] result = buffer.Slice(0, len).ToArray();
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        this.position++; // skip closing "
        return new Token(TokenType.QuotedIdentifier, start, result);
    }

    private static void AppendUnicodeEscape(ReadOnlySpan<byte> source, ref int position, ref Span<byte> buffer, ref byte[]? rented, ref int len)
    {
        position++; // skip 'u'
        if (position + 4 > source.Length)
        {
            throw new JMESPathException(
                $"Invalid unicode escape at position {position - 2}.");
        }

        int codePoint = ParseHex4(source.Slice(position, 4));
        position += 4;

        // Handle surrogate pairs
        if (codePoint >= 0xD800 && codePoint <= 0xDBFF)
        {
            if (position + 6 <= source.Length
                && source[position] == (byte)'\\'
                && source[position + 1] == (byte)'u')
            {
                position += 2;
                int low = ParseHex4(source.Slice(position, 4));
                position += 4;
                if (low >= 0xDC00 && low <= 0xDFFF)
                {
                    codePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (low - 0xDC00);
                }
                else
                {
                    throw new JMESPathException(
                        $"Invalid surrogate pair at position {position - 6}.");
                }
            }
            else
            {
                throw new JMESPathException(
                    $"Unpaired surrogate at position {position - 6}.");
            }
        }

        // Encode code point as UTF-8
        if (codePoint < 0x80)
        {
            GrowBuffer(ref buffer, ref rented, ref len);
            buffer[len++] = (byte)codePoint;
        }
        else if (codePoint < 0x800)
        {
            GrowBuffer(ref buffer, ref rented, ref len, 2);
            buffer[len++] = (byte)(0xC0 | (codePoint >> 6));
            buffer[len++] = (byte)(0x80 | (codePoint & 0x3F));
        }
        else if (codePoint < 0x10000)
        {
            GrowBuffer(ref buffer, ref rented, ref len, 3);
            buffer[len++] = (byte)(0xE0 | (codePoint >> 12));
            buffer[len++] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            buffer[len++] = (byte)(0x80 | (codePoint & 0x3F));
        }
        else
        {
            GrowBuffer(ref buffer, ref rented, ref len, 4);
            buffer[len++] = (byte)(0xF0 | (codePoint >> 18));
            buffer[len++] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
            buffer[len++] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            buffer[len++] = (byte)(0x80 | (codePoint & 0x3F));
        }
    }

    private static int ParseHex4(ReadOnlySpan<byte> hex)
    {
        int value = 0;
        for (int i = 0; i < 4; i++)
        {
            byte c = hex[i];
            int digit = c switch
            {
                >= (byte)'0' and <= (byte)'9' => c - '0',
                >= (byte)'a' and <= (byte)'f' => c - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => c - 'A' + 10,
                _ => throw new JMESPathException($"Invalid hex digit '{(char)c}'."),
            };

            value = (value << 4) | digit;
        }

        return value;
    }

    private Token ScanLiteral()
    {
        // Backtick-delimited JSON literal
        int start = this.position;
        this.position++; // skip opening `

        int contentStart = this.position;

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)'`')
            {
                Token token = new(TokenType.Literal, start, contentStart, this.position - contentStart);
                this.position++; // skip closing `
                return token;
            }

            if (c == (byte)'\\')
            {
                this.position++; // skip past escaped character
                if (this.position >= this.source.Length)
                {
                    break;
                }
            }

            this.position++;
        }

        throw new JMESPathException(
            $"Unterminated literal starting at position {start}.");
    }

    private Token ScanAmpersand()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'&')
        {
            this.position++;
            return new Token(TokenType.And, start);
        }

        return new Token(TokenType.Ampersand, start);
    }

    private Token ScanPipe()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'|')
        {
            this.position++;
            return new Token(TokenType.Or, start);
        }

        return new Token(TokenType.Pipe, start);
    }

    private Token ScanNot()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.NotEqual, start);
        }

        return new Token(TokenType.Not, start);
    }

    private Token ScanLessThan()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.LessThanOrEqual, start);
        }

        return new Token(TokenType.LessThan, start);
    }

    private Token ScanGreaterThan()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.GreaterThanOrEqual, start);
        }

        return new Token(TokenType.GreaterThan, start);
    }

    private Token ScanEqual()
    {
        int start = this.position;
        this.position++;

        if (this.Peek() == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.Equal, start);
        }

        throw new JMESPathException(
            $"Unexpected '=' at position {start}. Did you mean '=='?");
    }

    private Token ScanLeftBracket()
    {
        int start = this.position;
        this.position++;

        byte next = this.Peek();

        if (next == (byte)'?')
        {
            this.position++;
            return new Token(TokenType.Filter, start);
        }

        if (next == (byte)']')
        {
            this.position++;
            return new Token(TokenType.Flatten, start);
        }

        return new Token(TokenType.LeftBracket, start);
    }

    private static void GrowBuffer(ref Span<byte> buffer, ref byte[]? rented, ref int len, int needed = 1)
    {
        if (len + needed <= buffer.Length)
        {
            return;
        }

        byte[] newRented = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
        buffer.Slice(0, len).CopyTo(newRented);
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        rented = newRented;
        buffer = rented;
    }
}

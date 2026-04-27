// <copyright file="Lexer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// A UTF-8 lexer for JSONPath (RFC 9535) expressions. Operates directly on byte spans
/// with zero string allocation for identifiers and string literals.
/// </summary>
internal ref struct Lexer
{
    private readonly ReadOnlySpan<byte> source;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> struct.
    /// </summary>
    /// <param name="source">The UTF-8 encoded JSONPath expression.</param>
    public Lexer(ReadOnlySpan<byte> source)
    {
        this.source = source;
        this.position = 0;
    }

    /// <summary>
    /// Gets the current position in the source.
    /// </summary>
    public int Position => this.position;

    /// <summary>
    /// Reads the next token from the source.
    /// </summary>
    /// <returns>The next token.</returns>
    /// <exception cref="JsonPathException">Thrown on invalid syntax.</exception>
    public Token NextToken()
    {
        this.SkipWhitespace();

        if (this.position >= this.source.Length)
        {
            return new Token(TokenType.Eof, this.position, 0);
        }

        byte c = this.source[this.position];

        return c switch
        {
            (byte)'$' => new Token(TokenType.Root, this.position++, 1),
            (byte)'@' => new Token(TokenType.Current, this.position++, 1),
            (byte)'[' => new Token(TokenType.LeftBracket, this.position++, 1),
            (byte)']' => new Token(TokenType.RightBracket, this.position++, 1),
            (byte)'(' => new Token(TokenType.LeftParen, this.position++, 1),
            (byte)')' => new Token(TokenType.RightParen, this.position++, 1),
            (byte)'*' => new Token(TokenType.Wildcard, this.position++, 1),
            (byte)',' => new Token(TokenType.Comma, this.position++, 1),
            (byte)':' => new Token(TokenType.Colon, this.position++, 1),
            (byte)'?' => new Token(TokenType.Question, this.position++, 1),
            (byte)'.' => this.ScanDot(),
            (byte)'\'' => this.ScanSingleQuotedString(),
            (byte)'"' => this.ScanDoubleQuotedString(),
            (byte)'!' => this.ScanNot(),
            (byte)'<' => this.ScanLessThan(),
            (byte)'>' => this.ScanGreaterThan(),
            (byte)'=' => this.ScanEqual(),
            (byte)'&' => this.ScanAnd(),
            (byte)'|' => this.ScanOr(),
            (byte)'-' => this.ScanNumber(),
            >= (byte)'0' and <= (byte)'9' => this.ScanNumber(),
            _ => this.ScanNameOrKeyword(),
        };
    }

    /// <summary>
    /// Peeks at the next token without consuming it.
    /// </summary>
    /// <returns>The next token.</returns>
    public Token Peek()
    {
        int saved = this.position;
        Token t = this.NextToken();
        this.position = saved;
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipWhitespace()
    {
        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
            {
                this.position++;
            }
            else
            {
                break;
            }
        }
    }

    private Token ScanDot()
    {
        int start = this.position;
        this.position++; // consume first '.'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'.')
        {
            this.position++;
            return new Token(TokenType.DotDot, start, 2);
        }

        return new Token(TokenType.Dot, start, 1);
    }

    private Token ScanSingleQuotedString()
    {
        int start = this.position;
        this.position++; // consume opening quote

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)'\\')
            {
                // Skip the escaped character
                this.position += 2;
                if (this.position > this.source.Length)
                {
                    throw new JsonPathException("Unterminated single-quoted string.", start);
                }

                continue;
            }

            if (c == (byte)'\'')
            {
                this.position++; // consume closing quote
                return new Token(TokenType.SingleQuotedString, start, this.position - start);
            }

            // RFC 9535: raw control characters (U+0000-U+001F) are not allowed
            if (c <= 0x1F)
            {
                throw new JsonPathException(
                    $"Unescaped control character U+{c:X4} in single-quoted string at position {this.position}.", this.position);
            }

            this.position++;
        }

        throw new JsonPathException("Unterminated single-quoted string.", start);
    }

    private Token ScanDoubleQuotedString()
    {
        int start = this.position;
        this.position++; // consume opening quote

        while (this.position < this.source.Length)
        {
            byte c = this.source[this.position];
            if (c == (byte)'\\')
            {
                this.position += 2;
                if (this.position > this.source.Length)
                {
                    throw new JsonPathException("Unterminated double-quoted string.", start);
                }

                continue;
            }

            if (c == (byte)'"')
            {
                this.position++; // consume closing quote
                return new Token(TokenType.DoubleQuotedString, start, this.position - start);
            }

            // RFC 9535: raw control characters (U+0000-U+001F) are not allowed
            if (c <= 0x1F)
            {
                throw new JsonPathException(
                    $"Unescaped control character U+{c:X4} in double-quoted string at position {this.position}.", this.position);
            }

            this.position++;
        }

        throw new JsonPathException("Unterminated double-quoted string.", start);
    }

    private Token ScanNot()
    {
        int start = this.position;
        this.position++; // consume '!'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.NotEqual, start, 2);
        }

        return new Token(TokenType.Not, start, 1);
    }

    private Token ScanLessThan()
    {
        int start = this.position;
        this.position++; // consume '<'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.LessThanOrEqual, start, 2);
        }

        return new Token(TokenType.LessThan, start, 1);
    }

    private Token ScanGreaterThan()
    {
        int start = this.position;
        this.position++; // consume '>'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.GreaterThanOrEqual, start, 2);
        }

        return new Token(TokenType.GreaterThan, start, 1);
    }

    private Token ScanEqual()
    {
        int start = this.position;
        this.position++; // consume '='

        if (this.position < this.source.Length && this.source[this.position] == (byte)'=')
        {
            this.position++;
            return new Token(TokenType.Equal, start, 2);
        }

        throw new JsonPathException(
            $"Expected '==' but found lone '=' at position {start}.", start);
    }

    private Token ScanAnd()
    {
        int start = this.position;
        this.position++; // consume first '&'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'&')
        {
            this.position++;
            return new Token(TokenType.And, start, 2);
        }

        throw new JsonPathException(
            $"Expected '&&' but found lone '&' at position {start}.", start);
    }

    private Token ScanOr()
    {
        int start = this.position;
        this.position++; // consume first '|'

        if (this.position < this.source.Length && this.source[this.position] == (byte)'|')
        {
            this.position++;
            return new Token(TokenType.Or, start, 2);
        }

        throw new JsonPathException(
            $"Expected '||' but found lone '|' at position {start}.", start);
    }

    private Token ScanNumber()
    {
        int start = this.position;

        if (this.source[this.position] == (byte)'-')
        {
            this.position++;
        }

        if (this.position >= this.source.Length ||
            this.source[this.position] < (byte)'0' ||
            this.source[this.position] > (byte)'9')
        {
            throw new JsonPathException(
                $"Expected digit after '-' at position {start}.", start);
        }

        // RFC 9535: int = "0" / (["-"] DIGIT1 *DIGIT)
        // No leading zeros allowed for integers
        byte firstDigit = this.source[this.position];
        this.position++;

        if (firstDigit == (byte)'0' &&
            this.position < this.source.Length &&
            this.source[this.position] >= (byte)'0' &&
            this.source[this.position] <= (byte)'9')
        {
            // Leading zero in integer — check if this is a fractional number
            // (e.g., 0.5 is allowed in filter literals)
            if (this.source[this.position] != (byte)'.' &&
                this.source[this.position] != (byte)'e' &&
                this.source[this.position] != (byte)'E')
            {
                throw new JsonPathException(
                    $"Leading zero in integer at position {start}.", start);
            }
        }

        while (this.position < this.source.Length &&
               this.source[this.position] >= (byte)'0' &&
               this.source[this.position] <= (byte)'9')
        {
            this.position++;
        }

        // Check for fractional part or exponent → full Number token
        if (this.position < this.source.Length &&
            (this.source[this.position] == (byte)'.' || this.source[this.position] == (byte)'e' || this.source[this.position] == (byte)'E'))
        {
            return this.ScanNumberFraction(start);
        }

        return new Token(TokenType.Integer, start, this.position - start);
    }

    private Token ScanNumberFraction(int start)
    {
        if (this.position < this.source.Length && this.source[this.position] == (byte)'.')
        {
            this.position++;
            while (this.position < this.source.Length &&
                   this.source[this.position] >= (byte)'0' &&
                   this.source[this.position] <= (byte)'9')
            {
                this.position++;
            }
        }

        if (this.position < this.source.Length &&
            (this.source[this.position] == (byte)'e' || this.source[this.position] == (byte)'E'))
        {
            this.position++;
            if (this.position < this.source.Length &&
                (this.source[this.position] == (byte)'+' || this.source[this.position] == (byte)'-'))
            {
                this.position++;
            }

            while (this.position < this.source.Length &&
                   this.source[this.position] >= (byte)'0' &&
                   this.source[this.position] <= (byte)'9')
            {
                this.position++;
            }
        }

        return new Token(TokenType.Number, start, this.position - start);
    }

    private Token ScanNameOrKeyword()
    {
        int start = this.position;
        byte c = this.source[this.position];

        // RFC 9535: member-name-shorthand = name-first *name-char
        // name-first = ALPHA / "_" / %x80-D7FF / ...  (Unicode)
        if (!IsNameFirst(c))
        {
            throw new JsonPathException(
                $"Unexpected character '{(char)c}' at position {this.position}.", this.position);
        }

        this.position++;
        while (this.position < this.source.Length && IsNameChar(this.source[this.position]))
        {
            this.position++;
        }

        int length = this.position - start;
        ReadOnlySpan<byte> text = this.source.Slice(start, length);

        // Check if this is a function call: name immediately followed by '('
        // RFC 9535 grammar requires NO whitespace between function name and '('
        if (this.position < this.source.Length && this.source[this.position] == (byte)'(')
        {
            return new Token(TokenType.Function, start, length);
        }

        // Check for keyword literals
        if (text.SequenceEqual("true"u8))
        {
            return new Token(TokenType.True, start, length);
        }

        if (text.SequenceEqual("false"u8))
        {
            return new Token(TokenType.False, start, length);
        }

        if (text.SequenceEqual("null"u8))
        {
            return new Token(TokenType.Null, start, length);
        }

        return new Token(TokenType.Name, start, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameFirst(byte c)
    {
        // ASCII alpha or underscore, or start of multi-byte UTF-8 sequence
        return (c >= (byte)'a' && c <= (byte)'z') ||
               (c >= (byte)'A' && c <= (byte)'Z') ||
               c == (byte)'_' ||
               c >= 0x80; // Start of multi-byte UTF-8
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameChar(byte c)
    {
        // name-char = name-first / DIGIT
        return IsNameFirst(c) ||
               (c >= (byte)'0' && c <= (byte)'9');
    }
}

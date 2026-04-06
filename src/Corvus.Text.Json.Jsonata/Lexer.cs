// <copyright file="Lexer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Tokenizer for JSONata expressions. Produces a stream of <see cref="Token"/> values
/// from a source string. Follows the reference jsonata-js tokenizer faithfully.
/// </summary>
internal struct Lexer
{
    private readonly string source;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> struct.
    /// </summary>
    /// <param name="source">The JSONata expression to tokenize.</param>
    public Lexer(string source)
    {
        this.source = source;
        this.position = 0;
    }

    /// <summary>
    /// Gets the current position in the source string.
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
        if (this.Peek() == '/' && this.PeekAt(1) == '*')
        {
            int commentStart = this.position;
            this.position += 2;
            while (this.position < this.source.Length)
            {
                if (this.source[this.position] == '*' && this.PeekAt(1) == '/')
                {
                    this.position += 2;
                    return this.Next(prefixMode);
                }

                this.position++;
            }

            throw new JsonataException("S0106", "Comment not terminated", commentStart);
        }

        char c = this.Peek();

        // Regex literal
        if (!prefixMode && c == '/')
        {
            this.position++;
            return this.ScanRegex();
        }

        // Double-character operators (must check before single-char)
        if (this.position + 1 < this.source.Length)
        {
            char next = this.source[this.position + 1];
            string? doubleOp = (c, next) switch
            {
                ('.', '.') => "..",
                (':', '=') => ":=",
                ('!', '=') => "!=",
                ('>', '=') => ">=",
                ('<', '=') => "<=",
                ('*', '*') => "**",
                ('~', '>') => "~>",
                ('?', ':') => "?:",
                ('?', '?') => "??",
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
            return new Token(TokenType.Operator, c.ToString(), pos);
        }

        // String literals
        if (c == '"' || c == '\'')
        {
            return this.ScanString(c);
        }

        // Numeric literals
        if (c >= '0' && c <= '9')
        {
            Token? num = this.TryScanNumber();
            if (num.HasValue)
            {
                return num.Value;
            }
        }

        // Backtick-quoted names
        if (c == '`')
        {
            return this.ScanBacktickName();
        }

        // Names and variables
        return this.ScanNameOrVariable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly char Peek() => this.source[this.position];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly char PeekAt(int offset)
    {
        int idx = this.position + offset;
        return idx < this.source.Length ? this.source[idx] : '\0';
    }

    private void SkipWhitespace()
    {
        while (this.position < this.source.Length)
        {
            char c = this.source[this.position];
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\v')
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
    private static bool IsOperatorChar(char c)
    {
        return c switch
        {
            '.' or '[' or ']' or '{' or '}' or '(' or ')' or ',' or
            '@' or '#' or ';' or ':' or '?' or '+' or '-' or '*' or
            '/' or '%' or '|' or '=' or '<' or '>' or '^' or '&' => true,
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameStopChar(char c)
    {
        return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\v'
            || IsOperatorChar(c) || c == '!' || c == '~';
    }

    private Token ScanString(char quote)
    {
        int start = this.position;
        this.position++; // skip opening quote

        var sb = new StringBuilder();

        while (this.position < this.source.Length)
        {
            char c = this.source[this.position];

            if (c == '\\')
            {
                this.position++;
                if (this.position >= this.source.Length)
                {
                    throw new JsonataException("S0101", "String literal not terminated", start);
                }

                c = this.source[this.position];
                switch (c)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (this.position + 4 >= this.source.Length)
                        {
                            throw new JsonataException("S0104", "Invalid unicode escape", this.position);
                        }

                        string hex = this.source.Substring(this.position + 1, 4);
                        if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codepoint))
                        {
                            sb.Append((char)codepoint);
                            this.position += 4;
                        }
                        else
                        {
                            throw new JsonataException("S0104", "Invalid unicode escape", this.position);
                        }

                        break;
                    default:
                        throw new JsonataException("S0103", $"Illegal escape sequence: \\{c}", this.position, c.ToString());
                }
            }
            else if (c == quote)
            {
                this.position++;
                return new Token(TokenType.String, sb.ToString(), start);
            }
            else
            {
                sb.Append(c);
            }

            this.position++;
        }

        throw new JsonataException("S0101", "String literal not terminated", start);
    }

    private Token? TryScanNumber()
    {
        int start = this.position;

        // Match the JSON number regex: -?(0|([1-9][0-9]*))(\.[0-9]+)?([Ee][-+]?[0-9]+)?
        int i = start;
        if (i < this.source.Length && this.source[i] == '-')
        {
            i++;
        }

        if (i >= this.source.Length || this.source[i] < '0' || this.source[i] > '9')
        {
            return null;
        }

        if (this.source[i] == '0')
        {
            i++;
        }
        else
        {
            while (i < this.source.Length && this.source[i] >= '0' && this.source[i] <= '9')
            {
                i++;
            }
        }

        if (i < this.source.Length && this.source[i] == '.'
            && (i + 1 >= this.source.Length || this.source[i + 1] != '.'))
        {
            i++;
            int fracStart = i;
            while (i < this.source.Length && this.source[i] >= '0' && this.source[i] <= '9')
            {
                i++;
            }

            if (i == fracStart)
            {
                // No digits after '.', not a valid number
                return null;
            }
        }

        if (i < this.source.Length && (this.source[i] == 'e' || this.source[i] == 'E'))
        {
            i++;
            if (i < this.source.Length && (this.source[i] == '+' || this.source[i] == '-'))
            {
                i++;
            }

            int expStart = i;
            while (i < this.source.Length && this.source[i] >= '0' && this.source[i] <= '9')
            {
                i++;
            }

            if (i == expStart)
            {
                return null;
            }
        }

        string numStr = this.source.Substring(start, i - start);
        if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value))
        {
            this.position = i;
            return new Token(TokenType.Number, numStr, start) { NumericValue = value };
        }

        throw new JsonataException("S0102", $"Invalid number: {numStr}", start, numStr);
    }

    private Token ScanBacktickName()
    {
        int start = this.position;
        this.position++; // skip opening backtick

        int end = this.source.IndexOf('`', this.position);
        if (end == -1)
        {
            this.position = this.source.Length;
            throw new JsonataException("S0105", "Backtick-quoted name not terminated", start);
        }

        string name = this.source.Substring(this.position, end - this.position);
        this.position = end + 1;
        return new Token(TokenType.Name, name, start);
    }

    private Token ScanNameOrVariable()
    {
        int start = this.position;
        int i = this.position;

        while (i < this.source.Length && !IsNameStopChar(this.source[i]))
        {
            i++;
        }

        if (this.source[start] == '$')
        {
            // Variable reference — value excludes the '$'
            string varName = this.source.Substring(start + 1, i - start - 1);
            this.position = i;
            return new Token(TokenType.Variable, varName, start);
        }

        string name = this.source.Substring(start, i - start);
        this.position = i;

        return name switch
        {
            "or" or "in" or "and" => new Token(TokenType.Operator, name, start),
            "true" => new Token(TokenType.Value, "true", start),
            "false" => new Token(TokenType.Value, "false", start),
            "null" => new Token(TokenType.Value, "null", start),
            _ => this.position == this.source.Length && name.Length == 0
                ? new Token(TokenType.End, "(end)", start)
                : new Token(TokenType.Name, name, start),
        };
    }

    private Token ScanRegex()
    {
        // The leading '/' has already been consumed
        int start = this.position;
        int depth = 0;

        while (this.position < this.source.Length)
        {
            char c = this.source[this.position];

            // Check for closing '/' at depth 0
            if (c == '/' && depth == 0)
            {
                // Count preceding backslashes
                int backslashes = 0;
                while (this.position - (backslashes + 1) >= start
                    && this.source[this.position - (backslashes + 1)] == '\\')
                {
                    backslashes++;
                }

                if (backslashes % 2 == 0)
                {
                    // End of regex
                    string pattern = this.source.Substring(start, this.position - start);
                    if (pattern.Length == 0)
                    {
                        throw new JsonataException("S0301", "Empty regular expression", this.position);
                    }

                    this.position++;

                    // Scan flags
                    int flagStart = this.position;
                    while (this.position < this.source.Length
                        && (this.source[this.position] == 'i' || this.source[this.position] == 'm'))
                    {
                        this.position++;
                    }

                    string flags = this.source.Substring(flagStart, this.position - flagStart);

                    return new Token(TokenType.Regex, pattern, start - 1)
                    {
                        RegexPattern = pattern,
                        RegexFlags = flags,
                    };
                }
            }

            if ((c == '(' || c == '[' || c == '{') && (this.position == start || this.source[this.position - 1] != '\\'))
            {
                depth++;
            }

            if ((c == ')' || c == ']' || c == '}') && (this.position == start || this.source[this.position - 1] != '\\'))
            {
                depth--;
            }

            this.position++;
        }

        throw new JsonataException("S0302", "Regular expression not terminated", this.position);
    }
}
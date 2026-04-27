// <copyright file="Parser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Parses a UTF-8 JSONPath (RFC 9535) expression into an AST.
/// Uses recursive descent for path/segment syntax and precedence climbing
/// for filter expressions.
/// </summary>
internal static class Parser
{
    // RFC 9535 int range: i-JSON exact integers
    private const long MaxInt = (1L << 53) - 1;
    private const long MinInt = -((1L << 53) - 1);

    /// <summary>
    /// Parses a UTF-8 JSONPath expression string into an AST.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded JSONPath expression.</param>
    /// <returns>The parsed query AST.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public static QueryNode Parse(ReadOnlySpan<byte> utf8Expression)
    {
        // RFC 9535: no leading whitespace allowed
        if (utf8Expression.Length > 0 && IsWhitespace(utf8Expression[0]))
        {
            throw new JsonPathException("Leading whitespace is not allowed in JSONPath expressions.", 0);
        }

        Lexer lexer = new(utf8Expression);

        Token root = lexer.NextToken();
        if (root.Type != TokenType.Root)
        {
            throw new JsonPathException("JSONPath expression must start with '$'.", 0);
        }

        ImmutableArray<SegmentNode> segments = ParseSegments(ref lexer, utf8Expression);

        Token eof = lexer.NextToken();
        if (eof.Type != TokenType.Eof)
        {
            throw new JsonPathException(
                $"Unexpected token '{eof.Type}' at position {eof.Start}; expected end of expression.", eof.Start);
        }

        // RFC 9535: no trailing whitespace allowed
        if (utf8Expression.Length > 0 && IsWhitespace(utf8Expression[utf8Expression.Length - 1]))
        {
            throw new JsonPathException(
                "Trailing whitespace is not allowed in JSONPath expressions.", utf8Expression.Length - 1);
        }

        return new QueryNode(segments);
    }

    private static bool IsWhitespace(byte c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';

    private static ImmutableArray<SegmentNode> ParseSegments(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        ImmutableArray<SegmentNode>.Builder builder = ImmutableArray.CreateBuilder<SegmentNode>();

        while (true)
        {
            Token next = lexer.Peek();
            switch (next.Type)
            {
                case TokenType.Dot:
                    lexer.NextToken(); // consume '.'
                    builder.Add(ParseChildAfterDot(ref lexer, source, next));
                    break;

                case TokenType.LeftBracket:
                    lexer.NextToken(); // consume '['
                    builder.Add(new ChildSegmentNode(ParseBracketedSelectors(ref lexer, source)));
                    break;

                case TokenType.DotDot:
                    lexer.NextToken(); // consume '..'
                    builder.Add(ParseDescendantSegment(ref lexer, source, next));
                    break;

                default:
                    return builder.ToImmutable();
            }
        }
    }

    private static ChildSegmentNode ParseChildAfterDot(ref Lexer lexer, ReadOnlySpan<byte> source, Token dotToken)
    {
        Token next = lexer.Peek();

        // RFC 9535: no whitespace allowed between '.' and member-name-shorthand / '*'
        if (next.Start > dotToken.Start + dotToken.Length)
        {
            throw new JsonPathException(
                $"Whitespace is not allowed between '.' and member name at position {dotToken.Start}.", dotToken.Start);
        }

        switch (next.Type)
        {
            case TokenType.Name:
            case TokenType.True:
            case TokenType.False:
            case TokenType.Null:
                lexer.NextToken();
                byte[] name = DecodeUtf8Name(next.GetSpan(source));
                return new ChildSegmentNode(ImmutableArray.Create<SelectorNode>(new NameSelectorNode(name)));

            case TokenType.Wildcard:
                lexer.NextToken();
                return new ChildSegmentNode(ImmutableArray.Create<SelectorNode>(new WildcardSelectorNode()));

            default:
                throw new JsonPathException(
                    $"Expected member name or '*' after '.', found '{next.Type}' at position {next.Start}.", next.Start);
        }
    }

    private static SegmentNode ParseDescendantSegment(ref Lexer lexer, ReadOnlySpan<byte> source, Token dotDotToken)
    {
        Token next = lexer.Peek();

        // RFC 9535: no whitespace between '..' and its target
        if (next.Start > dotDotToken.Start + dotDotToken.Length)
        {
            throw new JsonPathException(
                $"Whitespace is not allowed after '..' at position {dotDotToken.Start}.", dotDotToken.Start);
        }

        switch (next.Type)
        {
            case TokenType.Name:
            case TokenType.True:
            case TokenType.False:
            case TokenType.Null:
                lexer.NextToken();
                byte[] name = DecodeUtf8Name(next.GetSpan(source));
                return new DescendantSegmentNode(ImmutableArray.Create<SelectorNode>(new NameSelectorNode(name)));

            case TokenType.Wildcard:
                lexer.NextToken();
                return new DescendantSegmentNode(ImmutableArray.Create<SelectorNode>(new WildcardSelectorNode()));

            case TokenType.LeftBracket:
                lexer.NextToken();
                return new DescendantSegmentNode(ParseBracketedSelectors(ref lexer, source));

            default:
                throw new JsonPathException(
                    $"Expected member name, '*', or '[' after '..', found '{next.Type}' at position {next.Start}.", next.Start);
        }
    }

    private static ImmutableArray<SelectorNode> ParseBracketedSelectors(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        ImmutableArray<SelectorNode>.Builder builder = ImmutableArray.CreateBuilder<SelectorNode>();

        builder.Add(ParseSelector(ref lexer, source));

        while (lexer.Peek().Type == TokenType.Comma)
        {
            lexer.NextToken(); // consume ','
            builder.Add(ParseSelector(ref lexer, source));
        }

        Token close = lexer.NextToken();
        if (close.Type != TokenType.RightBracket)
        {
            throw new JsonPathException(
                $"Expected ']' at position {close.Start}, found '{close.Type}'.", close.Start);
        }

        return builder.ToImmutable();
    }

    private static SelectorNode ParseSelector(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        Token next = lexer.Peek();

        switch (next.Type)
        {
            case TokenType.SingleQuotedString:
                lexer.NextToken();
                byte[] singleName = DecodeSingleQuotedString(next.GetSpan(source));
                return new NameSelectorNode(singleName);

            case TokenType.DoubleQuotedString:
                lexer.NextToken();
                byte[] doubleName = DecodeDoubleQuotedString(next.GetSpan(source));
                return new NameSelectorNode(doubleName);

            case TokenType.Wildcard:
                lexer.NextToken();
                return new WildcardSelectorNode();

            case TokenType.Question:
                lexer.NextToken(); // consume '?'
                FilterExpressionNode filter = ParseFilterExpression(ref lexer, source, precedence: 0);
                ValidateFilterExpressionType(filter, FilterContext.TestExpression);
                return new FilterSelectorNode(filter);

            case TokenType.Integer:
            case TokenType.Colon:
                return ParseIndexOrSlice(ref lexer, source);

            default:
                throw new JsonPathException(
                    $"Unexpected token '{next.Type}' at position {next.Start} in bracket selector.", next.Start);
        }
    }

    private static SelectorNode ParseIndexOrSlice(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        long? start = null;
        long? end = null;
        long? step = null;

        if (lexer.Peek().Type == TokenType.Integer)
        {
            Token intToken = lexer.NextToken();
            start = ParseJsonPathInt(intToken.GetSpan(source), intToken.Start);
        }

        // If next is not ':', this is a plain index selector
        if (lexer.Peek().Type != TokenType.Colon)
        {
            if (start is null)
            {
                throw new JsonPathException("Expected integer or ':' in array selector.", lexer.Peek().Start);
            }

            return new IndexSelectorNode(start.Value);
        }

        // Parse slice: [start?:end?:step?]
        lexer.NextToken(); // consume first ':'

        // Parse end
        if (lexer.Peek().Type == TokenType.Integer)
        {
            Token endToken = lexer.NextToken();
            end = ParseJsonPathInt(endToken.GetSpan(source), endToken.Start);
        }

        // Optional second ':'
        if (lexer.Peek().Type == TokenType.Colon)
        {
            lexer.NextToken(); // consume second ':'

            if (lexer.Peek().Type == TokenType.Integer)
            {
                Token stepToken = lexer.NextToken();
                step = ParseJsonPathInt(stepToken.GetSpan(source), stepToken.Start);
            }
        }

        return new SliceSelectorNode(start, end, step);
    }

    private static FilterExpressionNode ParseFilterExpression(ref Lexer lexer, ReadOnlySpan<byte> source, int precedence)
    {
        FilterExpressionNode left = ParseFilterAtom(ref lexer, source);

        while (true)
        {
            Token op = lexer.Peek();
            int opPrec = GetFilterPrecedence(op.Type);
            if (opPrec < 0 || opPrec < precedence)
            {
                break;
            }

            lexer.NextToken(); // consume operator

            if (op.Type == TokenType.And || op.Type == TokenType.Or)
            {
                FilterExpressionNode right = ParseFilterExpression(ref lexer, source, opPrec + 1);
                left = op.Type == TokenType.And
                    ? new LogicalAndNode(left, right)
                    : new LogicalOrNode(left, right);
            }
            else
            {
                // Comparison operators
                FilterExpressionNode right = ParseFilterExpression(ref lexer, source, opPrec + 1);
                ComparisonOp cmpOp = op.Type switch
                {
                    TokenType.Equal => ComparisonOp.Equal,
                    TokenType.NotEqual => ComparisonOp.NotEqual,
                    TokenType.LessThan => ComparisonOp.LessThan,
                    TokenType.LessThanOrEqual => ComparisonOp.LessThanOrEqual,
                    TokenType.GreaterThan => ComparisonOp.GreaterThan,
                    TokenType.GreaterThanOrEqual => ComparisonOp.GreaterThanOrEqual,
                    _ => throw new JsonPathException($"Unexpected operator '{op.Type}'.", op.Start),
                };

                // Validate that both sides are comparable
                ValidateFilterExpressionType(left, FilterContext.Comparable);
                ValidateFilterExpressionType(right, FilterContext.Comparable);
                left = new ComparisonNode(left, cmpOp, right);
            }
        }

        return left;
    }

    private static int GetFilterPrecedence(TokenType type) => type switch
    {
        TokenType.Or => 1,
        TokenType.And => 2,
        TokenType.Equal or TokenType.NotEqual or
        TokenType.LessThan or TokenType.LessThanOrEqual or
        TokenType.GreaterThan or TokenType.GreaterThanOrEqual => 3,
        _ => -1, // Not an operator
    };

    private static FilterExpressionNode ParseFilterAtom(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        Token next = lexer.Peek();

        switch (next.Type)
        {
            case TokenType.Not:
                lexer.NextToken();
                FilterExpressionNode operand = ParseFilterAtom(ref lexer, source);
                return new LogicalNotNode(operand);

            case TokenType.LeftParen:
                lexer.NextToken(); // consume '('
                FilterExpressionNode inner = ParseFilterExpression(ref lexer, source, precedence: 0);
                Token close = lexer.NextToken();
                if (close.Type != TokenType.RightParen)
                {
                    throw new JsonPathException(
                        $"Expected ')' at position {close.Start}, found '{close.Type}'.", close.Start);
                }

                return new ParenExpressionNode(inner);

            case TokenType.Current:
            case TokenType.Root:
                return ParseFilterQuery(ref lexer, source);

            case TokenType.Function:
                return ParseFunctionCall(ref lexer, source);

            case TokenType.SingleQuotedString:
                lexer.NextToken();
                byte[] strBytes = DecodeSingleQuotedString(next.GetSpan(source));
                return CreateStringLiteral(strBytes);

            case TokenType.DoubleQuotedString:
                lexer.NextToken();
                byte[] dblBytes = DecodeDoubleQuotedString(next.GetSpan(source));
                return CreateStringLiteral(dblBytes);

            case TokenType.Integer:
            case TokenType.Number:
                lexer.NextToken();
                return CreateNumericLiteral(next.GetSpan(source));

            case TokenType.True:
                lexer.NextToken();
                return new LiteralNode(JsonElement.ParseValue("true"u8));

            case TokenType.False:
                lexer.NextToken();
                return new LiteralNode(JsonElement.ParseValue("false"u8));

            case TokenType.Null:
                lexer.NextToken();
                return new LiteralNode(JsonElement.ParseValue("null"u8));

            default:
                throw new JsonPathException(
                    $"Unexpected token '{next.Type}' at position {next.Start} in filter expression.", next.Start);
        }
    }

    private static LiteralNode CreateStringLiteral(byte[] utf8Bytes)
    {
        string strValue = Encoding.UTF8.GetString(utf8Bytes);
        try
        {
            JsonElement element = JsonElement.ParseValue(
                Encoding.UTF8.GetBytes($"\"{EscapeJsonString(strValue)}\""));
            return new LiteralNode(element);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Invalid string literal: {ex.Message}");
        }
    }

    private static LiteralNode CreateNumericLiteral(ReadOnlySpan<byte> utf8)
    {
        try
        {
            JsonElement element = JsonElement.ParseValue(utf8);
            return new LiteralNode(element);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Invalid numeric literal: {ex.Message}");
        }
    }

    private static FilterExpressionNode ParseFilterQuery(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        Token start = lexer.NextToken(); // consume '@' or '$'
        bool isRelative = start.Type == TokenType.Current;

        ImmutableArray<SegmentNode> segments = ParseSegments(ref lexer, source);
        return new FilterQueryNode(isRelative, segments);
    }

    private static FunctionCallNode ParseFunctionCall(ref Lexer lexer, ReadOnlySpan<byte> source)
    {
        Token nameToken = lexer.NextToken();
        ReadOnlySpan<byte> nameSpan = nameToken.GetSpan(source);
        string funcName = Encoding.UTF8.GetString(nameSpan.ToArray());

        Token open = lexer.NextToken();
        if (open.Type != TokenType.LeftParen)
        {
            throw new JsonPathException(
                $"Expected '(' after function name '{funcName}' at position {open.Start}.", open.Start);
        }

        ImmutableArray<FilterExpressionNode>.Builder args = ImmutableArray.CreateBuilder<FilterExpressionNode>();

        if (lexer.Peek().Type != TokenType.RightParen)
        {
            args.Add(ParseFilterExpression(ref lexer, source, precedence: 0));

            while (lexer.Peek().Type == TokenType.Comma)
            {
                lexer.NextToken(); // consume ','
                args.Add(ParseFilterExpression(ref lexer, source, precedence: 0));
            }
        }

        Token close = lexer.NextToken();
        if (close.Type != TokenType.RightParen)
        {
            throw new JsonPathException(
                $"Expected ')' after function arguments at position {close.Start}.", close.Start);
        }

        FunctionCallNode node = new(funcName, args.ToImmutable());
        ValidateFunctionTypes(node);
        return node;
    }

    /// <summary>
    /// Parses a JSONPath integer (RFC 9535 grammar: int = "0" / (["-"] DIGIT1 *DIGIT)).
    /// Validates no leading zeros, no -0, and range within i-JSON integers.
    /// </summary>
    private static long ParseJsonPathInt(ReadOnlySpan<byte> utf8, int position)
    {
        if (utf8.Length == 0)
        {
            throw new JsonPathException("Expected integer.", position);
        }

        int i = 0;
        bool negative = false;

        if (utf8[0] == (byte)'-')
        {
            negative = true;
            i = 1;
            if (i >= utf8.Length)
            {
                throw new JsonPathException("Expected digit after '-'.", position);
            }
        }

        // Check for -0 (invalid per RFC 9535 int grammar)
        if (negative && utf8.Length == 2 && utf8[1] == (byte)'0')
        {
            throw new JsonPathException("'-0' is not a valid JSONPath integer.", position);
        }

        // Check for leading zero (only "0" alone is valid)
        if (utf8[i] == (byte)'0' && (utf8.Length - i) > 1)
        {
            throw new JsonPathException("Leading zeros are not allowed in JSONPath integers.", position);
        }

        long result = 0;
        for (; i < utf8.Length; i++)
        {
            byte d = utf8[i];
            if (d < (byte)'0' || d > (byte)'9')
            {
                throw new JsonPathException($"Invalid character in integer at position {position}.", position);
            }

            result = (result * 10) + (d - '0');

            // Overflow check — i-JSON range is symmetric: [-MaxInt, MaxInt]
            if (result > MaxInt)
            {
                throw new JsonPathException(
                    $"Integer value out of i-JSON range at position {position}.", position);
            }
        }

        return negative ? -result : result;
    }

    private static byte[] DecodeUtf8Name(ReadOnlySpan<byte> raw)
    {
        return raw.ToArray();
    }

    private static byte[] DecodeSingleQuotedString(ReadOnlySpan<byte> raw)
    {
        // raw includes the surrounding quotes: 'text'
        return DecodeQuotedString(raw.Slice(1, raw.Length - 2), (byte)'\'');
    }

    private static byte[] DecodeDoubleQuotedString(ReadOnlySpan<byte> raw)
    {
        // raw includes the surrounding quotes: "text"
        return DecodeQuotedString(raw.Slice(1, raw.Length - 2), (byte)'"');
    }

    private static byte[] DecodeQuotedString(ReadOnlySpan<byte> content, byte quoteChar)
    {
        // Fast path: no escapes
        if (content.IndexOf((byte)'\\') < 0)
        {
            return content.ToArray();
        }

        byte[]? rented = null;
        Span<byte> buffer = content.Length <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(content.Length * 4));

        try
        {
            int written = 0;
            int i = 0;
            while (i < content.Length)
            {
                if (content[i] == (byte)'\\' && i + 1 < content.Length)
                {
                    byte escaped = content[i + 1];
                    switch (escaped)
                    {
                        case (byte)'\\':
                            buffer[written++] = (byte)'\\';
                            i += 2;
                            break;
                        case (byte)'/':
                            buffer[written++] = (byte)'/';
                            i += 2;
                            break;
                        case (byte)'b':
                            buffer[written++] = (byte)'\b';
                            i += 2;
                            break;
                        case (byte)'f':
                            buffer[written++] = (byte)'\f';
                            i += 2;
                            break;
                        case (byte)'n':
                            buffer[written++] = (byte)'\n';
                            i += 2;
                            break;
                        case (byte)'r':
                            buffer[written++] = (byte)'\r';
                            i += 2;
                            break;
                        case (byte)'t':
                            buffer[written++] = (byte)'\t';
                            i += 2;
                            break;
                        case (byte)'u':
                            written += DecodeUnicodeEscape(content, ref i, buffer.Slice(written));
                            break;
                        default:
                            if (escaped == quoteChar)
                            {
                                buffer[written++] = quoteChar;
                                i += 2;
                            }
                            else
                            {
                                // RFC 9535: invalid escape sequence
                                throw new JsonPathException(
                                    $"Invalid escape sequence '\\{(char)escaped}' in quoted string.");
                            }

                            break;
                    }
                }
                else
                {
                    buffer[written++] = content[i];
                    i++;
                }
            }

            return buffer.Slice(0, written).ToArray();
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int DecodeUnicodeEscape(ReadOnlySpan<byte> content, ref int i, Span<byte> dest)
    {
        // \uXXXX — decode to UTF-8
        if (i + 5 >= content.Length)
        {
            throw new JsonPathException("Incomplete \\u escape sequence.");
        }

        int codePoint = ParseHex4(content.Slice(i + 2, 4));
        i += 6;

        // Handle surrogate pairs
        if (codePoint >= 0xD800 && codePoint <= 0xDBFF)
        {
            if (i + 5 < content.Length &&
                content[i] == (byte)'\\' && content[i + 1] == (byte)'u')
            {
                int low = ParseHex4(content.Slice(i + 2, 4));
                if (low >= 0xDC00 && low <= 0xDFFF)
                {
                    codePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (low - 0xDC00);
                    i += 6;
                }
                else
                {
                    // High surrogate not followed by valid low surrogate
                    throw new JsonPathException("Invalid surrogate pair in \\u escape sequence.");
                }
            }
            else
            {
                // Lone high surrogate
                throw new JsonPathException("Lone high surrogate in \\u escape sequence.");
            }
        }
        else if (codePoint >= 0xDC00 && codePoint <= 0xDFFF)
        {
            // Lone low surrogate
            throw new JsonPathException("Lone low surrogate in \\u escape sequence.");
        }

        // Encode as UTF-8
        if (codePoint <= 0x7F)
        {
            dest[0] = (byte)codePoint;
            return 1;
        }

        if (codePoint <= 0x7FF)
        {
            dest[0] = (byte)(0xC0 | (codePoint >> 6));
            dest[1] = (byte)(0x80 | (codePoint & 0x3F));
            return 2;
        }

        if (codePoint <= 0xFFFF)
        {
            dest[0] = (byte)(0xE0 | (codePoint >> 12));
            dest[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            dest[2] = (byte)(0x80 | (codePoint & 0x3F));
            return 3;
        }

        dest[0] = (byte)(0xF0 | (codePoint >> 18));
        dest[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
        dest[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
        dest[3] = (byte)(0x80 | (codePoint & 0x3F));
        return 4;
    }

    private static int ParseHex4(ReadOnlySpan<byte> hex)
    {
        int result = 0;
        for (int j = 0; j < 4; j++)
        {
            result <<= 4;
            byte b = hex[j];
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                result |= b - '0';
            }
            else if (b >= (byte)'a' && b <= (byte)'f')
            {
                result |= (b - 'a') + 10;
            }
            else if (b >= (byte)'A' && b <= (byte)'F')
            {
                result |= (b - 'A') + 10;
            }
            else
            {
                throw new JsonPathException($"Invalid hex digit in \\u escape.");
            }
        }

        return result;
    }

    private static string EscapeJsonString(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append($"\\u{(int)c:X4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    private enum FilterContext
    {
        TestExpression,
        Comparable,
    }

    /// <summary>
    /// RFC 9535 Section 2.4 well-typedness rules.
    /// A filter expression in test-expression context must yield LogicalType.
    /// A filter expression in comparable context must yield ValueType.
    /// </summary>
    private static void ValidateFilterExpressionType(FilterExpressionNode node, FilterContext context)
    {
        switch (node)
        {
            case LogicalAndNode and:
                if (context == FilterContext.Comparable)
                {
                    throw new JsonPathException("Logical AND expression cannot be used as a comparable value.");
                }

                ValidateFilterExpressionType(and.Left, FilterContext.TestExpression);
                ValidateFilterExpressionType(and.Right, FilterContext.TestExpression);
                break;

            case LogicalOrNode or:
                if (context == FilterContext.Comparable)
                {
                    throw new JsonPathException("Logical OR expression cannot be used as a comparable value.");
                }

                ValidateFilterExpressionType(or.Left, FilterContext.TestExpression);
                ValidateFilterExpressionType(or.Right, FilterContext.TestExpression);
                break;

            case LogicalNotNode not:
                if (context == FilterContext.Comparable)
                {
                    throw new JsonPathException("Logical NOT expression cannot be used as a comparable value.");
                }

                ValidateFilterExpressionType(not.Operand, FilterContext.TestExpression);
                break;

            case ComparisonNode cmp:
                // Comparison produces LogicalType — OK in test context, invalid in comparable
                if (context == FilterContext.Comparable)
                {
                    throw new JsonPathException("Comparison expression cannot be used as a comparable value.");
                }

                // Each side must be comparable
                ValidateFilterExpressionType(cmp.Left, FilterContext.Comparable);
                ValidateFilterExpressionType(cmp.Right, FilterContext.Comparable);
                break;

            case LiteralNode:
                // Literal is ValueType — valid as comparable, NOT as test expression
                if (context == FilterContext.TestExpression)
                {
                    throw new JsonPathException("Literal value cannot be used as a filter test expression.");
                }

                break;

            case FilterQueryNode query:
                // Filter query is NodesType
                if (context == FilterContext.Comparable)
                {
                    // Only singular queries can be used in comparisons
                    if (!IsSingularQuery(query))
                    {
                        throw new JsonPathException(
                            "Non-singular query cannot be used in a comparison. Use value() for singular queries.");
                    }
                }

                // In test context: existence test — always valid
                break;

            case FunctionCallNode func:
                // Function return type depends on the function
                FilterResultType returnType = GetFunctionReturnType(func.Name);
                if (context == FilterContext.TestExpression && returnType == FilterResultType.ValueType)
                {
                    throw new JsonPathException(
                        $"Function '{func.Name}()' returns a value type and cannot be used as a test expression.");
                }

                if (context == FilterContext.Comparable && returnType == FilterResultType.LogicalType)
                {
                    throw new JsonPathException(
                        $"Function '{func.Name}()' returns a logical type and cannot be used in a comparison.");
                }

                if (context == FilterContext.Comparable && returnType == FilterResultType.NodesType)
                {
                    // nodes() would need to be singular — we don't have nodes() but just in case
                    throw new JsonPathException(
                        $"Function '{func.Name}()' returns a node list and cannot be used directly in a comparison.");
                }

                break;

            case ParenExpressionNode paren:
                ValidateFilterExpressionType(paren.Inner, context);
                break;
        }
    }

    private static bool IsSingularQuery(FilterQueryNode query)
    {
        foreach (SegmentNode segment in query.Segments)
        {
            if (segment is DescendantSegmentNode)
            {
                return false;
            }

            if (segment.Selectors.Length != 1)
            {
                return false;
            }

            SelectorNode sel = segment.Selectors[0];
            if (sel is not (NameSelectorNode or IndexSelectorNode))
            {
                return false;
            }
        }

        return true;
    }

    private enum FilterResultType
    {
        ValueType,
        LogicalType,
        NodesType,
    }

    private static FilterResultType GetFunctionReturnType(string name) => name switch
    {
        "length" => FilterResultType.ValueType,
        "count" => FilterResultType.ValueType,
        "value" => FilterResultType.ValueType,
        "match" => FilterResultType.LogicalType,
        "search" => FilterResultType.LogicalType,
        _ => throw new JsonPathException($"Unknown function: '{name}'."),
    };

    /// <summary>
    /// Validates function argument types per RFC 9535 well-typedness rules.
    /// </summary>
    private static void ValidateFunctionTypes(FunctionCallNode node)
    {
        switch (node.Name)
        {
            case "length":
                // length(ValueType) → ValueType
                if (node.Arguments.Length != 1)
                {
                    throw new JsonPathException("length() requires exactly 1 argument.");
                }

                ValidateFunctionArgType(node.Arguments[0], "length", FunctionArgType.ValueType);
                break;

            case "count":
                // count(NodesType) → ValueType
                if (node.Arguments.Length != 1)
                {
                    throw new JsonPathException("count() requires exactly 1 argument.");
                }

                ValidateFunctionArgType(node.Arguments[0], "count", FunctionArgType.NodesType);
                break;

            case "value":
                // value(NodesType) → ValueType
                if (node.Arguments.Length != 1)
                {
                    throw new JsonPathException("value() requires exactly 1 argument.");
                }

                ValidateFunctionArgType(node.Arguments[0], "value", FunctionArgType.NodesType);
                break;

            case "match":
            case "search":
                // match/search(ValueType, ValueType) → LogicalType
                if (node.Arguments.Length != 2)
                {
                    throw new JsonPathException($"{node.Name}() requires exactly 2 arguments.");
                }

                ValidateFunctionArgType(node.Arguments[0], node.Name, FunctionArgType.ValueType);
                ValidateFunctionArgType(node.Arguments[1], node.Name, FunctionArgType.ValueType);
                break;

            default:
                throw new JsonPathException($"Unknown function: '{node.Name}'.");
        }
    }

    private enum FunctionArgType
    {
        ValueType,
        NodesType,
        LogicalType,
    }

    private static void ValidateFunctionArgType(FilterExpressionNode arg, string funcName, FunctionArgType expected)
    {
        FunctionArgType actual = InferArgType(arg);

        if (actual == expected)
        {
            return;
        }

        // NodesType can coerce to ValueType via implicit singular-query unwrap
        if (expected == FunctionArgType.ValueType && actual == FunctionArgType.NodesType)
        {
            if (arg is FilterQueryNode query && IsSingularQuery(query))
            {
                return;
            }

            // Non-singular query cannot coerce to ValueType
            throw new JsonPathException(
                $"Non-singular query argument to {funcName}() cannot be used where a value is expected.");
        }

        throw new JsonPathException(
            $"Argument to {funcName}() has type {actual} but {expected} was expected.");
    }

    private static FunctionArgType InferArgType(FilterExpressionNode node)
    {
        return node switch
        {
            LiteralNode => FunctionArgType.ValueType,
            FilterQueryNode => FunctionArgType.NodesType,
            FunctionCallNode func => GetFunctionReturnType(func.Name) switch
            {
                FilterResultType.ValueType => FunctionArgType.ValueType,
                FilterResultType.LogicalType => FunctionArgType.LogicalType,
                FilterResultType.NodesType => FunctionArgType.NodesType,
                _ => FunctionArgType.ValueType,
            },
            ComparisonNode => FunctionArgType.LogicalType,
            LogicalAndNode => FunctionArgType.LogicalType,
            LogicalOrNode => FunctionArgType.LogicalType,
            LogicalNotNode => FunctionArgType.LogicalType,
            ParenExpressionNode paren => InferArgType(paren.Inner),
            _ => FunctionArgType.ValueType,
        };
    }
}

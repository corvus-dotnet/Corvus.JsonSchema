// <copyright file="Parser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// A Pratt parser for JMESPath expressions. Converts a token stream from
/// <see cref="Lexer"/> into a <see cref="JMESPathNode"/> AST.
/// Projections are modelled as explicit AST nodes following the rubber-duck recommendation.
/// </summary>
internal ref struct Parser
{
    // Binding power constants (higher = tighter binding).
    private const int BpPipe = 1;
    private const int BpOr = 5;
    private const int BpAnd = 10;
    private const int BpNot = 15;
    private const int BpComparison = 20;
    private const int BpFlatten = 25;

    // The maximum binding power for a token that stops a projection.
    // Tokens with LBP below this threshold (Flatten, Pipe, Or, And, comparators)
    // cannot continue inside a projection RHS — they return identity instead.
    private const int ProjectionStop = 26;
    private const int BpStar = 30;
    private const int BpFilter = 31;
    private const int BpDot = 40;
    private const int BpBracket = 55;
    private const int BpLparen = 60;

    private readonly byte[] source;
    private Lexer lexer;
    private Token current;

    private Parser(byte[] source)
    {
        this.source = source;
        this.lexer = new Lexer(source);
        this.current = this.lexer.NextToken();
    }

    /// <summary>
    /// Parses a UTF-8 JMESPath expression into an AST.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded expression.</param>
    /// <returns>The root AST node.</returns>
    public static JMESPathNode Parse(byte[] utf8Expression)
    {
        Parser parser = new(utf8Expression);
        JMESPathNode ast = parser.Expression(0);
        if (parser.current.Type != TokenType.Eof)
        {
            throw new JMESPathException(
                $"Unexpected token '{parser.current.Type}' at position {parser.current.Position}.", parser.current.Position);
        }

        return ast;
    }

    private JMESPathNode Expression(int rbp)
    {
        JMESPathNode left = this.Nud();

        while (rbp < this.GetLbp())
        {
            left = this.Led(left);
        }

        return left;
    }

    private JMESPathNode Nud()
    {
        Token token = this.current;

        switch (token.Type)
        {
            case TokenType.Identifier:
            {
                this.Advance();
                byte[] name = token.GetValueSpan(this.source).ToArray();
                IdentifierNode node = new(name) { Position = token.Position };

                // Check for function call: identifier followed by '('
                if (this.current.Type == TokenType.LeftParen)
                {
                    return this.ParseFunctionCall(name, token.Position);
                }

                return node;
            }

            case TokenType.QuotedIdentifier:
            {
                this.Advance();
                byte[] name = token.GetValueSpan(this.source).ToArray();
                return new IdentifierNode(name) { Position = token.Position };
            }

            case TokenType.Number:
            {
                // Bare number is invalid in NUD position in JMESPath
                throw new JMESPathException(
                    $"Unexpected number token at position {token.Position}.", token.Position);
            }

            case TokenType.RawString:
            {
                this.Advance();
                byte[] value = token.GetValueSpan(this.source).ToArray();
                return new RawStringNode(value) { Position = token.Position };
            }

            case TokenType.Literal:
            {
                this.Advance();
                byte[] jsonValue = this.GetLiteralBytes(token);
                return new LiteralNode(jsonValue) { Position = token.Position };
            }

            case TokenType.At:
                this.Advance();
                return new CurrentNode { Position = token.Position };

            case TokenType.Not:
                this.Advance();
                return new NotNode(this.Expression(BpNot)) { Position = token.Position };

            case TokenType.Star:
                // Leading wildcard: *.rhs — object value projection on current node
                this.Advance();
                return new ValueProjectionNode(
                    new CurrentNode { Position = token.Position },
                    this.ParseProjectionRhs(BpStar)) { Position = token.Position };

            case TokenType.Flatten:
                // Leading flatten: [].rhs
                this.Advance();
                return new FlattenProjectionNode(
                    new CurrentNode { Position = token.Position },
                    this.ParseProjectionRhs(BpFlatten)) { Position = token.Position };

            case TokenType.Filter:
                return this.ParseFilter(new CurrentNode { Position = token.Position });

            case TokenType.LeftBracket:
                return this.ParseNudBracket(token);

            case TokenType.LeftBrace:
                return this.ParseMultiSelectHash(token.Position);

            case TokenType.Ampersand:
                this.Advance();
                return new ExpressionRefNode(this.Expression(BpPipe)) { Position = token.Position };

            case TokenType.LeftParen:
                this.Advance();
                JMESPathNode inner = this.Expression(0);
                this.Expect(TokenType.RightParen);
                return inner;

            default:
                throw new JMESPathException(
                    $"Unexpected token '{token.Type}' at position {token.Position}.", token.Position);
        }
    }

    private JMESPathNode Led(JMESPathNode left)
    {
        Token token = this.current;

        switch (token.Type)
        {
            case TokenType.Dot:
                this.Advance();
                return this.ParseDotRhs(left, token.Position);

            case TokenType.Pipe:
                this.Advance();
                return new PipeNode(left, this.Expression(BpPipe)) { Position = token.Position };

            case TokenType.Or:
                this.Advance();
                return new OrNode(left, this.Expression(BpOr)) { Position = token.Position };

            case TokenType.And:
                this.Advance();
                return new AndNode(left, this.Expression(BpAnd)) { Position = token.Position };

            case TokenType.LessThan:
            case TokenType.LessThanOrEqual:
            case TokenType.Equal:
            case TokenType.GreaterThanOrEqual:
            case TokenType.GreaterThan:
            case TokenType.NotEqual:
                return this.ParseComparison(left, token);

            case TokenType.LeftBracket:
                return this.ParseLedBracket(left);

            case TokenType.Flatten:
                this.Advance();
                return new FlattenProjectionNode(left, this.ParseProjectionRhs(BpFlatten))
                    { Position = token.Position };

            case TokenType.Filter:
                return this.ParseFilter(left);

            default:
                throw new JMESPathException(
                    $"Unexpected token '{token.Type}' at position {token.Position}.", token.Position);
        }
    }

    private int GetLbp()
    {
        return this.current.Type switch
        {
            TokenType.Pipe => BpPipe,
            TokenType.Or => BpOr,
            TokenType.And => BpAnd,
            TokenType.LessThan
                or TokenType.LessThanOrEqual
                or TokenType.Equal
                or TokenType.GreaterThanOrEqual
                or TokenType.GreaterThan
                or TokenType.NotEqual => BpComparison,
            TokenType.Flatten => BpFlatten,
            TokenType.Star => BpStar,
            TokenType.Filter => BpFilter,
            TokenType.Dot => BpDot,
            TokenType.LeftBracket => BpBracket,
            TokenType.LeftParen => BpLparen,
            _ => 0,
        };
    }

    private JMESPathNode ParseDotRhs(JMESPathNode left, int dotPosition, int bp = BpDot)
    {
        // After a dot we expect: identifier, star (wildcard), multi-select list, or multi-select hash
        Token rhs = this.current;

        switch (rhs.Type)
        {
            case TokenType.Star:
                // Object wildcard projection: left.*.rhs
                this.Advance();
                return new ValueProjectionNode(left, this.ParseProjectionRhs(BpStar))
                    { Position = dotPosition };

            case TokenType.LeftBracket:
                // Multi-select list: left.[expr, expr, ...]
                this.Advance(); // consume [
                return new SubExpressionNode(left, this.ParseMultiSelectList(rhs.Position))
                    { Position = dotPosition };

            case TokenType.LeftBrace:
                return new SubExpressionNode(left, this.ParseMultiSelectHash(rhs.Position))
                    { Position = dotPosition };

            case TokenType.Identifier:
            case TokenType.QuotedIdentifier:
            {
                JMESPathNode rightNode = this.Expression(bp);
                return new SubExpressionNode(left, rightNode) { Position = dotPosition };
            }

            default:
                throw new JMESPathException(
                    $"Expected identifier, '*', '[', or '{{' after '.' at position {rhs.Position}.", rhs.Position);
        }
    }

    private JMESPathNode ParseNudBracket(Token bracketToken)
    {
        // [ in NUD position — could be: [number], [start:stop:step], [*], [*.*,...], or [expr, expr, ...] (multi-select list)
        this.Advance(); // consume [

        if (this.current.Type == TokenType.Number || this.current.Type == TokenType.Colon)
        {
            // Index or slice
            JMESPathNode indexOrSlice = this.ParseIndexOrSlice(bracketToken.Position);
            if (indexOrSlice is SliceNode)
            {
                // NUD slices create projections (same as LED slices)
                return new ListProjectionNode(
                    indexOrSlice,
                    this.ParseProjectionRhs(BpStar)) { Position = bracketToken.Position };
            }

            return indexOrSlice;
        }

        if (this.current.Type == TokenType.Star)
        {
            this.Advance(); // consume *
            if (this.current.Type == TokenType.RightBracket)
            {
                // [*] — list wildcard projection on current node
                this.Advance(); // consume ]
                return new ListProjectionNode(
                    new CurrentNode { Position = bracketToken.Position },
                    this.ParseProjectionRhs(BpStar)) { Position = bracketToken.Position };
            }

            // Multi-select list starting with * expression (e.g., [*.*] or [*, foo])
            // Reconstruct the * as a value projection NUD
            JMESPathNode starExpr = new ValueProjectionNode(
                new CurrentNode { Position = bracketToken.Position },
                this.ParseProjectionRhs(BpStar)) { Position = bracketToken.Position };

            // Continue parsing LEDs for the first expression
            while (0 < this.GetLbp())
            {
                starExpr = this.Led(starExpr);
            }

            List<JMESPathNode> expressions = new();
            expressions.Add(starExpr);

            while (this.current.Type == TokenType.Comma)
            {
                this.Advance();
                expressions.Add(this.Expression(0));
            }

            this.Expect(TokenType.RightBracket);
            return new MultiSelectListNode(expressions.ToArray()) { Position = bracketToken.Position };
        }

        // Multi-select list: [expr, expr, ...]
        return this.ParseMultiSelectList(bracketToken.Position);
    }

    private JMESPathNode ParseLedBracket(JMESPathNode left)
    {
        // [ in LED position (after an expression)
        Token bracketToken = this.current;
        this.Advance(); // consume [

        if (this.current.Type == TokenType.Number || this.current.Type == TokenType.Colon)
        {
            // Index or slice
            JMESPathNode indexOrSlice = this.ParseIndexOrSlice(bracketToken.Position);
            if (indexOrSlice is SliceNode)
            {
                // Slices create projections
                return new ListProjectionNode(
                    new SubExpressionNode(left, indexOrSlice) { Position = bracketToken.Position },
                    this.ParseProjectionRhs(BpStar)) { Position = bracketToken.Position };
            }

            return new SubExpressionNode(left, indexOrSlice) { Position = bracketToken.Position };
        }

        if (this.current.Type == TokenType.Star)
        {
            // [*] — list wildcard projection
            this.Advance(); // consume *
            this.Expect(TokenType.RightBracket);
            return new ListProjectionNode(left, this.ParseProjectionRhs(BpStar))
                { Position = bracketToken.Position };
        }

        // In LED bracket position, only number, colon (slice), and star are valid.
        // Flatten [] and filter [? are separate tokens handled elsewhere.
        throw new JMESPathException(
            $"Unexpected token '{this.current.Type}' in bracket expression at position {bracketToken.Position}.", bracketToken.Position);
    }

    private JMESPathNode ParseIndexOrSlice(int position)
    {
        // We've consumed [ already. Current is Number or Colon.
        int? start = null;
        int? stop = null;
        int? step = null;
        bool isSlice = false;

        // Parse start
        if (this.current.Type == TokenType.Number)
        {
            start = this.current.IntegerValue;
            this.Advance();
        }

        if (this.current.Type == TokenType.Colon)
        {
            isSlice = true;
            this.Advance(); // consume first :

            // Parse stop
            if (this.current.Type == TokenType.Number)
            {
                stop = this.current.IntegerValue;
                this.Advance();
            }

            if (this.current.Type == TokenType.Colon)
            {
                this.Advance(); // consume second :

                // Parse step
                if (this.current.Type == TokenType.Number)
                {
                    step = this.current.IntegerValue;
                    this.Advance();
                }
            }
        }

        this.Expect(TokenType.RightBracket);

        if (isSlice)
        {
            return new SliceNode
            {
                Start = start,
                Stop = stop,
                Step = step,
                Position = position,
            };
        }

        return new IndexNode
        {
            Index = start ?? 0,
            Position = position,
        };
    }

    private JMESPathNode ParseFilter(JMESPathNode left)
    {
        Token filterToken = this.current;
        this.Advance(); // consume [?

        JMESPathNode condition = this.Expression(0);
        this.Expect(TokenType.RightBracket);

        return new FilterProjectionNode(left, condition, this.ParseProjectionRhs(BpFilter))
            { Position = filterToken.Position };
    }

    private JMESPathNode ParseMultiSelectList(int position)
    {
        // Current is first expression ([ already consumed)
        List<JMESPathNode> expressions = new();
        expressions.Add(this.Expression(0));

        while (this.current.Type == TokenType.Comma)
        {
            this.Advance(); // consume ,
            expressions.Add(this.Expression(0));
        }

        this.Expect(TokenType.RightBracket);
        return new MultiSelectListNode(expressions.ToArray()) { Position = position };
    }

    private JMESPathNode ParseMultiSelectHash(int position)
    {
        this.Advance(); // consume {
        List<MultiSelectHashNode.KeyValuePair> pairs = new();

        pairs.Add(this.ParseHashPair());

        while (this.current.Type == TokenType.Comma)
        {
            this.Advance(); // consume ,
            pairs.Add(this.ParseHashPair());
        }

        this.Expect(TokenType.RightBrace);
        return new MultiSelectHashNode(pairs.ToArray()) { Position = position };
    }

    private MultiSelectHashNode.KeyValuePair ParseHashPair()
    {
        // key : expression
        byte[] key;
        if (this.current.Type == TokenType.Identifier)
        {
            key = this.current.GetValueSpan(this.source).ToArray();
            this.Advance();
        }
        else if (this.current.Type == TokenType.QuotedIdentifier)
        {
            key = this.current.GetValueSpan(this.source).ToArray();
            this.Advance();
        }
        else
        {
            throw new JMESPathException(
                $"Expected identifier for hash key at position {this.current.Position}.", this.current.Position);
        }

        this.Expect(TokenType.Colon);
        JMESPathNode value = this.Expression(0);

        return new MultiSelectHashNode.KeyValuePair(key, value);
    }

    private FunctionCallNode ParseFunctionCall(byte[] name, int position)
    {
        this.Advance(); // consume (
        List<JMESPathNode> args = new();

        if (this.current.Type != TokenType.RightParen)
        {
            args.Add(this.Expression(0));
            while (this.current.Type == TokenType.Comma)
            {
                this.Advance(); // consume ,
                args.Add(this.Expression(0));
            }
        }

        this.Expect(TokenType.RightParen);
        return new FunctionCallNode(name, args.ToArray()) { Position = position };
    }

    /// <summary>
    /// Parses the right-hand side of a projection. Uses a projection-stop threshold:
    /// tokens with binding power below <see cref="ProjectionStop"/> (Flatten, Pipe, Or,
    /// And, comparators) return identity, leaving them for the outer expression loop.
    /// This ensures flatten <c>[]</c> creates a new outer projection rather than being
    /// captured inside the current one.
    /// </summary>
    private JMESPathNode ParseProjectionRhs(int bp)
    {
        // If the next token has binding power below the projection stop threshold,
        // it stops the projection — return identity and let the outer loop handle it.
        if (this.GetLbp() < ProjectionStop)
        {
            return new CurrentNode { Position = this.current.Position };
        }

        if (this.current.Type == TokenType.Dot)
        {
            this.Advance();
            return this.ParseDotRhs(new CurrentNode { Position = this.current.Position }, this.current.Position, bp);
        }

        if (this.current.Type == TokenType.LeftBracket
            || this.current.Type == TokenType.Filter)
        {
            return this.Expression(bp);
        }

        return new CurrentNode { Position = this.current.Position };
    }

    private JMESPathNode ParseComparison(JMESPathNode left, Token opToken)
    {
        CompareOp op = opToken.Type switch
        {
            TokenType.LessThan => CompareOp.LessThan,
            TokenType.LessThanOrEqual => CompareOp.LessThanOrEqual,
            TokenType.Equal => CompareOp.Equal,
            TokenType.GreaterThanOrEqual => CompareOp.GreaterThanOrEqual,
            TokenType.GreaterThan => CompareOp.GreaterThan,
            TokenType.NotEqual => CompareOp.NotEqual,
            _ => throw new JMESPathException($"Unexpected comparison operator at position {opToken.Position}.", opToken.Position),
        };

        this.Advance();
        JMESPathNode right = this.Expression(BpComparison);

        return new ComparisonNode(left, op, right) { Position = opToken.Position };
    }

    private byte[] GetLiteralBytes(Token token)
    {
        ReadOnlySpan<byte> span = token.GetValueSpan(this.source);

        // Handle escaped backticks in literal content
        bool hasEscapes = false;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == (byte)'\\' && i + 1 < span.Length && span[i + 1] == (byte)'`')
            {
                hasEscapes = true;
                break;
            }
        }

        if (!hasEscapes)
        {
            return span.ToArray();
        }

        // Unescape backticks
        List<byte> result = new(span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == (byte)'\\' && i + 1 < span.Length && span[i + 1] == (byte)'`')
            {
                result.Add((byte)'`');
                i++; // skip the backtick
            }
            else
            {
                result.Add(span[i]);
            }
        }

        return result.ToArray();
    }

    private void Advance()
    {
        this.current = this.lexer.NextToken();
    }

    private void Expect(TokenType type)
    {
        if (this.current.Type != type)
        {
            throw new JMESPathException(
                $"Expected '{type}' but found '{this.current.Type}' at position {this.current.Position}.", this.current.Position);
        }

        this.Advance();
    }
}

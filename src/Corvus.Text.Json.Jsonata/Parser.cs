// <copyright file="Parser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata.Ast;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Pratt parser (Top Down Operator Precedence) for JSONata expressions.
/// Produces a typed AST from a token stream, then applies a post-processing
/// pass to flatten path expressions, attach predicates, and resolve parent references.
/// </summary>
/// <remarks>
/// This follows the reference jsonata-js parser closely, adapted to C# with
/// strongly-typed AST nodes.
/// </remarks>
internal sealed class Parser
{
    // Parser binding powers for infix operators.
    // Delimiters (: ; , ) ] } |) have BP 0 — they terminate expressions.
    // The tokenizer recognizes them as operators, but the parser does not give them infix precedence.
    private static readonly Dictionary<string, int> OperatorPrecedence = new()
    {
        ["."] = 75,
        ["["] = 80,
        ["]"] = 0,
        ["{"] = 70,
        ["("] = 80,
        [")"] = 0,
        ["@"] = 80,
        ["#"] = 80,
        ["?"] = 20,
        ["+"] = 50,
        ["-"] = 50,
        ["*"] = 60,
        ["/"] = 60,
        ["%"] = 60,
        ["="] = 40,
        ["<"] = 40,
        [">"] = 40,
        ["^"] = 40,
        ["**"] = 60,
        [":="] = 10,
        ["!="] = 40,
        ["<="] = 40,
        [">="] = 40,
        ["~>"] = 40,
        ["?:"] = 40,
        ["??"] = 40,
        ["and"] = 30,
        ["or"] = 25,
        ["in"] = 40,
        ["&"] = 50,
    };

    private readonly string source;
    private Lexer lexer;
    private Token current;

    // Ancestry tracking for parent operator resolution
    private int ancestorLabel;
    private int ancestorIndex;
    private readonly List<ParentNode> ancestry = [];

    private Parser(string source)
    {
        this.source = source;
        this.lexer = new Lexer(source);
    }

    /// <summary>
    /// Parses a JSONata expression string into an AST.
    /// </summary>
    /// <param name="expression">The JSONata expression.</param>
    /// <returns>The root AST node.</returns>
    public static JsonataNode Parse(string expression)
    {
        var parser = new Parser(expression);
        parser.Advance();
        var ast = parser.Expression(0);

        if (parser.current.Type != TokenType.End)
        {
            throw new JsonataException(
                "S0201",
                $"Unexpected token: {parser.current.Value}",
                parser.current.Position,
                parser.current.Value);
        }

        ast = parser.ProcessAst(ast);

        // Top-level parent reference is invalid
        if (ast.Type == NodeType.Parent || ast.SeekingParent is not null)
        {
            throw new JsonataException(
                "S0217",
                "Attempt to derive parent at top level",
                ast.Position,
                ast.Type.ToString());
        }

        return ast;
    }

    private void Advance(string? expectedOperator = null, bool infix = false)
    {
        if (expectedOperator is not null
            && !(this.current.Type == TokenType.Operator && this.current.Value == expectedOperator))
        {
            string code = this.current.Type == TokenType.End ? "S0203" : "S0202";
            throw new JsonataException(
                code,
                $"Expected '{expectedOperator}', got '{this.current.Value}'",
                this.current.Position,
                this.current.Value);
        }

        this.current = this.lexer.Next(prefixMode: infix);
    }

    /// <summary>
    /// Core Pratt expression parser. Parses expressions with binding power greater than <paramref name="rbp"/>.
    /// </summary>
    private JsonataNode Expression(int rbp)
    {
        var t = this.current;
        this.Advance(infix: true);
        var left = this.Nud(t);

        while (rbp < this.GetLbp())
        {
            t = this.current;
            this.Advance();
            left = this.Led(t, left);
        }

        return left;
    }

    /// <summary>
    /// Gets the left binding power of the current token.
    /// </summary>
    private int GetLbp()
    {
        if (this.current.Type == TokenType.Operator && OperatorPrecedence.TryGetValue(this.current.Value, out int bp))
        {
            return bp;
        }

        return 0;
    }

    /// <summary>
    /// Null denotation — handles prefix operators and terminals.
    /// </summary>
    private JsonataNode Nud(Token token)
    {
        return token.Type switch
        {
            TokenType.Name => this.NudName(token),
            TokenType.Variable => this.NudVariable(token),
            TokenType.Number => new NumberNode { Value = token.NumericValue, Position = token.Position },
            TokenType.String => new StringNode { Value = token.Value, Position = token.Position },
            TokenType.Value => new ValueNode { Value = token.Value, Position = token.Position },
            TokenType.Regex => new RegexNode
            {
                Pattern = token.RegexPattern ?? string.Empty,
                Flags = token.RegexFlags ?? string.Empty,
                Position = token.Position,
            },
            TokenType.Operator => this.NudOperator(token),
            TokenType.End => throw new JsonataException(
                "S0207",
                "Unexpected end of expression",
                token.Position,
                token.Value),
            _ => throw new JsonataException(
                "S0211",
                $"Unexpected token: {token.Value}",
                token.Position,
                token.Value),
        };
    }

    private JsonataNode NudName(Token token)
    {
        return new NameNode { Value = token.Value, Position = token.Position };
    }

    private JsonataNode NudVariable(Token token)
    {
        return new VariableNode { Name = token.Value, Position = token.Position };
    }

    private JsonataNode NudOperator(Token token)
    {
        return token.Value switch
        {
            "-" => this.NudUnaryMinus(token),
            "*" => new WildcardNode { Position = token.Position },
            "**" => new DescendantNode { Position = token.Position },
            "%" => new ParentNode
            {
                Position = token.Position,
                Slot = new ParentSlot
                {
                    Label = $"!{this.ancestorLabel++}",
                    Level = 1,
                    Index = this.ancestorIndex++,
                },
            },
            "(" => this.NudBlock(token),
            "[" => this.NudArrayConstructor(token),
            "{" => this.NudObjectConstructor(token),
            "|" => this.NudTransform(token),

            // 'and', 'or', 'in' can appear as names when used in prefix position
            "and" or "or" or "in" => new NameNode { Value = token.Value, Position = token.Position },
            _ => throw new JsonataException(
                "S0211",
                $"Unexpected operator in prefix position: {token.Value}",
                token.Position,
                token.Value),
        };
    }

    private JsonataNode NudUnaryMinus(Token token)
    {
        var expr = this.Expression(70);
        return new UnaryNode { Operator = "-", Expression = expr, Position = token.Position };
    }

    private JsonataNode NudBlock(Token token)
    {
        var expressions = new List<JsonataNode>();
        while (!(this.current.Type == TokenType.Operator && this.current.Value == ")"))
        {
            expressions.Add(this.Expression(0));
            if (!(this.current.Type == TokenType.Operator && this.current.Value == ";"))
            {
                break;
            }

            this.Advance(";");
        }

        this.Advance(")", infix: true);
        return new BlockNode { Expressions = { }, Position = token.Position }.WithExpressions(expressions);
    }

    private JsonataNode NudArrayConstructor(Token token)
    {
        var items = new List<JsonataNode>();
        if (!(this.current.Type == TokenType.Operator && this.current.Value == "]"))
        {
            for (; ;)
            {
                var item = this.Expression(0);

                // Range operator inside array constructor
                if (this.current.Type == TokenType.Operator && this.current.Value == "..")
                {
                    int rangePos = this.current.Position;
                    this.Advance("..");
                    var rhs = this.Expression(0);
                    item = new BinaryNode
                    {
                        Operator = "..",
                        Lhs = item,
                        Rhs = rhs,
                        Position = rangePos,
                    };
                }

                items.Add(item);
                if (!(this.current.Type == TokenType.Operator && this.current.Value == ","))
                {
                    break;
                }

                this.Advance(",");
            }
        }

        this.Advance("]", infix: true);
        return new ArrayConstructorNode { Position = token.Position }.WithExpressions(items);
    }

    private JsonataNode NudObjectConstructor(Token token)
    {
        return this.ParseObjectConstructor(token, null);
    }

    private JsonataNode ParseObjectConstructor(Token token, JsonataNode? left)
    {
        var pairs = new List<(JsonataNode Key, JsonataNode Value)>();
        if (!(this.current.Type == TokenType.Operator && this.current.Value == "}"))
        {
            for (; ;)
            {
                var key = this.Expression(0);
                this.Advance(":");
                var value = this.Expression(0);
                pairs.Add((key, value));
                if (!(this.current.Type == TokenType.Operator && this.current.Value == ","))
                {
                    break;
                }

                this.Advance(",");
            }
        }

        this.Advance("}", infix: true);

        if (left is null)
        {
            // Prefix form — object constructor
            return new ObjectConstructorNode { Position = token.Position }.WithPairs(pairs);
        }
        else
        {
            // Infix form — group-by (handled during processAST as binary with value "{")
            return new BinaryNode
            {
                Operator = "{",
                Lhs = left,
                Rhs = new ObjectConstructorNode { Position = token.Position }.WithPairs(pairs),
                Position = token.Position,
            };
        }
    }

    private JsonataNode NudTransform(Token token)
    {
        var pattern = this.Expression(0);
        this.Advance("|");
        var update = this.Expression(0);
        JsonataNode? delete = null;
        if (this.current.Type == TokenType.Operator && this.current.Value == ",")
        {
            this.Advance(",");
            delete = this.Expression(0);
        }

        this.Advance("|");
        return new TransformNode
        {
            Pattern = pattern,
            Update = update,
            Delete = delete,
            Position = token.Position,
        };
    }

    /// <summary>
    /// Left denotation — handles infix and postfix operators.
    /// </summary>
    private JsonataNode Led(Token token, JsonataNode left)
    {
        if (token.Type != TokenType.Operator)
        {
            throw new JsonataException(
                "S0204",
                $"Unknown operator: {token.Value}",
                token.Position,
                token.Value);
        }

        return token.Value switch
        {
            "." or "+" or "-" or "*" or "/" or "%" or "=" or "!=" or
            "<" or "<=" or ">" or ">=" or "&" or "and" or "or" or "in" =>
                this.LedBinary(token, left),
            "[" => this.LedFilter(token, left),
            "(" => this.LedFunctionCall(token, left),
            "{" => this.LedGroupBy(token, left),
            "?" => this.LedCondition(token, left),
            "?:" => this.LedElvis(token, left),
            "??" => this.LedCoalesce(token, left),
            ":=" => this.LedBind(token, left),
            "~>" => this.LedApply(token, left),
            "^" => this.LedSort(token, left),
            "@" => this.LedFocus(token, left),
            "#" => this.LedIndex(token, left),
            _ => throw new JsonataException(
                "S0204",
                $"Unknown operator: {token.Value}",
                token.Position,
                token.Value),
        };
    }

    private JsonataNode LedBinary(Token token, JsonataNode left)
    {
        int bp = OperatorPrecedence[token.Value];
        var rhs = this.Expression(bp);
        return new BinaryNode
        {
            Operator = token.Value,
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    private JsonataNode LedFilter(Token token, JsonataNode left)
    {
        if (this.current.Type == TokenType.Operator && this.current.Value == "]")
        {
            // Empty predicate — keep-array modifier
            this.Advance("]");
            left.KeepArray = true;
            return left;
        }

        var rhs = this.Expression(OperatorPrecedence["]"]);
        this.Advance("]", infix: true);
        return new BinaryNode
        {
            Operator = "[",
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    private JsonataNode LedFunctionCall(Token token, JsonataNode left)
    {
        var args = new List<JsonataNode>();
        bool isPartial = false;

        if (!(this.current.Type == TokenType.Operator && this.current.Value == ")"))
        {
            for (; ;)
            {
                if (this.current.Type == TokenType.Operator && this.current.Value == "?")
                {
                    // Partial application placeholder
                    isPartial = true;
                    args.Add(new PlaceholderNode { Position = this.current.Position });
                    this.Advance("?");
                }
                else
                {
                    args.Add(this.Expression(0));
                }

                if (!(this.current.Type == TokenType.Operator && this.current.Value == ","))
                {
                    break;
                }

                this.Advance(",");
            }
        }

        this.Advance(")", infix: true);

        // Check for lambda definition: function($x, $y){ body } or λ($x, $y){ body }
        if (left is NameNode nameNode && (nameNode.Value == "function" || nameNode.Value == "\u03BB"))
        {
            return this.ParseLambdaBody(token, args);
        }

        if (isPartial)
        {
            return new PartialNode { Procedure = left, Position = token.Position }.WithArguments(args);
        }

        return new FunctionCallNode { Procedure = left, Position = token.Position }.WithArguments(args);
    }

    private LambdaNode ParseLambdaBody(Token token, List<JsonataNode> paramNodes)
    {
        var lambda = new LambdaNode { Position = token.Position };

        // All args must be variable tokens
        for (int i = 0; i < paramNodes.Count; i++)
        {
            if (paramNodes[i] is not VariableNode vn)
            {
                throw new JsonataException(
                    "S0208",
                    $"Parameter {i + 1} of function definition is not a variable",
                    paramNodes[i].Position,
                    paramNodes[i].ToString());
            }

            lambda.Parameters.Add(vn.Name);
        }

        // Optional function signature: <sig>
        if (this.current.Type == TokenType.Operator && this.current.Value == "<")
        {
            int sigPos = this.current.Position;
            int depth = 1;
            string sig = "<";
            while (depth > 0
                && !(this.current.Type == TokenType.Operator && this.current.Value == "{")
                && this.current.Type != TokenType.End)
            {
                this.Advance();
                if (this.current.Type == TokenType.Operator && this.current.Value == ">")
                {
                    depth--;
                }
                else if (this.current.Type == TokenType.Operator && this.current.Value == "<")
                {
                    depth++;
                }

                sig += this.current.Value;
            }

            this.Advance(">");
            lambda.Signature = sig;
        }

        // Parse the function body: { expr }
        this.Advance("{");
        lambda.Body = this.Expression(0);
        this.Advance("}");

        return lambda;
    }

    private JsonataNode LedGroupBy(Token token, JsonataNode left)
    {
        return this.ParseObjectConstructor(token, left);
    }

    private JsonataNode LedCondition(Token token, JsonataNode left)
    {
        var then = this.Expression(0);
        JsonataNode? elseExpr = null;
        if (this.current.Type == TokenType.Operator && this.current.Value == ":")
        {
            this.Advance(":");
            elseExpr = this.Expression(0);
        }

        return new ConditionNode
        {
            Condition = left,
            Then = then,
            Else = elseExpr,
            Position = token.Position,
        };
    }

    private JsonataNode LedElvis(Token token, JsonataNode left)
    {
        var elseExpr = this.Expression(0);
        return new ConditionNode
        {
            Condition = left,
            Then = left,
            Else = elseExpr,
            Position = token.Position,
        };
    }

    private JsonataNode LedCoalesce(Token token, JsonataNode left)
    {
        var elseExpr = this.Expression(0);

        // Desugar: lhs ?? rhs → $exists(lhs) ? lhs : rhs
        return new ConditionNode
        {
            Condition = new FunctionCallNode
            {
                Procedure = new VariableNode { Name = "exists", Position = token.Position },
                Position = token.Position,
            }.WithArguments([left]),
            Then = left,
            Else = elseExpr,
            Position = token.Position,
        };
    }

    private JsonataNode LedBind(Token token, JsonataNode left)
    {
        if (left is not VariableNode)
        {
            throw new JsonataException(
                "S0212",
                "The left side of := must be a variable",
                left.Position,
                left.ToString());
        }

        // Right-associative: subtract 1 from binding power
        var rhs = this.Expression(OperatorPrecedence[":="] - 1);
        return new BinaryNode
        {
            Operator = ":=",
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    private JsonataNode LedApply(Token token, JsonataNode left)
    {
        int bp = OperatorPrecedence["~>"];
        var rhs = this.Expression(bp);
        return new BinaryNode
        {
            Operator = "~>",
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    private JsonataNode LedSort(Token token, JsonataNode left)
    {
        this.Advance("(");
        var terms = new List<SortTerm>();
        for (; ;)
        {
            bool descending = false;
            if (this.current.Type == TokenType.Operator && this.current.Value == "<")
            {
                this.Advance("<");
            }
            else if (this.current.Type == TokenType.Operator && this.current.Value == ">")
            {
                descending = true;
                this.Advance(">");
            }

            var expr = this.Expression(0);
            terms.Add(new SortTerm { Descending = descending, Expression = expr });

            if (!(this.current.Type == TokenType.Operator && this.current.Value == ","))
            {
                break;
            }

            this.Advance(",");
        }

        this.Advance(")");

        return new BinaryNode
        {
            Operator = "^",
            Lhs = left,
            Rhs = new SortNode { Position = token.Position }.WithTerms(terms),
            Position = token.Position,
        };
    }

    private JsonataNode LedFocus(Token token, JsonataNode left)
    {
        var rhs = this.Expression(OperatorPrecedence["@"]);
        if (rhs is not VariableNode)
        {
            throw new JsonataException(
                "S0214",
                "The right side of @ must be a variable",
                rhs.Position,
                "@");
        }

        return new BinaryNode
        {
            Operator = "@",
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    private JsonataNode LedIndex(Token token, JsonataNode left)
    {
        var rhs = this.Expression(OperatorPrecedence["#"]);
        if (rhs is not VariableNode)
        {
            throw new JsonataException(
                "S0214",
                "The right side of # must be a variable",
                rhs.Position,
                "#");
        }

        return new BinaryNode
        {
            Operator = "#",
            Lhs = left,
            Rhs = rhs,
            Position = token.Position,
        };
    }

    /// <summary>
    /// Post-processes the raw parse tree: flattens paths, attaches predicates and group-by
    /// clauses to steps, resolves parent ancestry, and applies tail-call optimization.
    /// </summary>
    private JsonataNode ProcessAst(JsonataNode node)
    {
        switch (node)
        {
            case BinaryNode binary:
                return this.ProcessBinary(binary);
            case UnaryNode unary:
                return this.ProcessUnary(unary);
            case ArrayConstructorNode array:
                return this.ProcessArrayConstructor(array);
            case ObjectConstructorNode obj:
                return this.ProcessObjectConstructor(obj);
            case FunctionCallNode func:
                return this.ProcessFunctionCall(func);
            case PartialNode partial:
                return this.ProcessPartial(partial);
            case LambdaNode lambda:
                return this.ProcessLambda(lambda);
            case ConditionNode cond:
                return this.ProcessCondition(cond);
            case BlockNode block:
                return this.ProcessBlock(block);
            case TransformNode transform:
                return this.ProcessTransform(transform);
            case NameNode name:
                // A bare name becomes a single-step path
                var path = new PathNode { Position = name.Position };
                path.Steps.Add(name);
                if (name.KeepArray)
                {
                    path.KeepSingletonArray = true;
                }

                return path;
            case ParentNode parent:
                parent.Slot = new ParentSlot
                {
                    Label = $"!{this.ancestorLabel++}",
                    Level = 1,
                    Index = this.ancestorIndex++,
                };
                this.ancestry.Add(parent);
                return parent;

            // Terminal nodes pass through unchanged
            case StringNode:
            case NumberNode:
            case ValueNode:
            case VariableNode:
            case WildcardNode:
            case DescendantNode:
            case RegexNode:
            case PlaceholderNode:
                return node;

            default:
                throw new JsonataException(
                    "S0206",
                    $"Unexpected node type: {node.Type}",
                    node.Position);
        }
    }

    private JsonataNode ProcessBinary(BinaryNode binary)
    {
        switch (binary.Operator)
        {
            case ".":
                return this.ProcessDot(binary);
            case "[":
                return this.ProcessPredicate(binary);
            case "{":
                return this.ProcessGroupBy(binary);
            case "^":
                return this.ProcessSortBinary(binary);
            case ":=":
                return this.ProcessBindBinary(binary);
            case "@":
                return this.ProcessFocusBinary(binary);
            case "#":
                return this.ProcessIndexBinary(binary);
            case "~>":
                return this.ProcessApplyBinary(binary);
            default:
                // Standard binary operators
                var result = new BinaryNode
                {
                    Operator = binary.Operator,
                    Lhs = this.ProcessAst(binary.Lhs),
                    Rhs = this.ProcessAst(binary.Rhs),
                    Position = binary.Position,
                };
                PushAncestry(result, result.Lhs);
                PushAncestry(result, result.Rhs);
                return result;
        }
    }

    private JsonataNode ProcessDot(BinaryNode binary)
    {
        var lstep = this.ProcessAst(binary.Lhs);

        PathNode result;
        if (lstep is PathNode existingPath)
        {
            result = existingPath;
        }
        else
        {
            result = new PathNode { Position = lstep.Position };
            result.Steps.Add(lstep);
        }

        if (lstep is ParentNode parentLhs)
        {
            result.SeekingParent ??= [];
            result.SeekingParent.Add(parentLhs.Slot);
        }

        var rest = this.ProcessAst(binary.Rhs);
        if (rest is PathNode restPath)
        {
            result.Steps.AddRange(restPath.Steps);
        }
        else
        {
            // Move predicate to stages when attaching to a path
            if (rest is NameNode nn && nn.Annotations?.Stages.Count > 0)
            {
                // Already has stages, nothing to move
            }

            result.Steps.Add(rest);
        }

        // String literals in paths become names; numbers and values are errors
        for (int i = 0; i < result.Steps.Count; i++)
        {
            var step = result.Steps[i];
            if (step is StringNode strStep)
            {
                result.Steps[i] = new NameNode { Value = strStep.Value, Position = strStep.Position };
            }
            else if (step is NumberNode numStep)
            {
                throw new JsonataException(
                    "S0213",
                    $"Number used as a step in a path: {numStep.Value}",
                    numStep.Position,
                    numStep.Value.ToString());
            }
            else if (step is ValueNode valStep)
            {
                throw new JsonataException(
                    "S0213",
                    $"Value used as a step in a path: {valStep.Value}",
                    valStep.Position,
                    valStep.Value);
            }
        }

        // Check for keep-array on any step
        if (result.Steps.Exists(s => s.KeepArray))
        {
            result.KeepSingletonArray = true;
        }

        // Flag array constructor at first/last step
        if (result.Steps[0] is ArrayConstructorNode firstArr)
        {
            firstArr.ConsArray = true;
        }

        if (result.Steps[result.Steps.Count - 1] is ArrayConstructorNode lastArr)
        {
            lastArr.ConsArray = true;
        }

        ResolveAncestry(result);
        return result;
    }

    private JsonataNode ProcessPredicate(BinaryNode binary)
    {
        var result = this.ProcessAst(binary.Lhs);
        var predicate = this.ProcessAst(binary.Rhs);

        JsonataNode step = result;

        if (result is PathNode pathNode)
        {
            step = pathNode.Steps[pathNode.Steps.Count - 1];
        }

        // Resolve parent seeking in predicate
        if (predicate.SeekingParent is not null)
        {
            foreach (var slot in predicate.SeekingParent)
            {
                if (slot.Level == 1)
                {
                    SeekParent(step, slot);
                }
                else
                {
                    slot.Level--;
                }
            }

            PushAncestry(step, predicate);
        }

        var filter = new FilterNode { Expression = predicate, Position = binary.Position };

        var stepAnnotations = GetOrCreateAnnotations(step);

        // S0209: A predicate cannot follow a grouping expression in a step
        if (stepAnnotations.Group is not null)
        {
            throw new JsonataException(
                "S0209",
                "A predicate cannot follow a grouping expression in a step",
                binary.Position);
        }

        stepAnnotations.Stages.Add(filter);

        // Propagate KeepArray from the binary node — the [] modifier may have
        // set it on this BinaryNode (predicate), and ProcessPredicate must
        // forward it to the resulting AST node so that CompilePath or WrapKeepArray
        // see it.
        if (binary.KeepArray)
        {
            if (result is PathNode pn)
            {
                // Set KeepArray on the last step so ProcessDot picks it up,
                // and set KeepSingletonArray directly for paths that aren't
                // further combined by a dot expression.
                pn.Steps[pn.Steps.Count - 1].KeepArray = true;
                pn.KeepSingletonArray = true;
            }
            else
            {
                result.KeepArray = true;
            }
        }

        return result;
    }

    private JsonataNode ProcessGroupBy(BinaryNode binary)
    {
        var result = this.ProcessAst(binary.Lhs);

        // Check for existing group-by
        var annotations = GetOrCreateAnnotations(result);
        if (annotations.Group is not null)
        {
            throw new JsonataException(
                "S0210",
                "Multiple group-by clauses",
                binary.Position);
        }

        // Process each pair in the object constructor RHS
        var objRhs = (ObjectConstructorNode)binary.Rhs;
        var groupBy = new GroupBy { Position = binary.Position };
        foreach (var (key, value) in objRhs.Pairs)
        {
            groupBy.Pairs.Add((this.ProcessAst(key), this.ProcessAst(value)));
        }

        annotations.Group = groupBy;
        return result;
    }

    private JsonataNode ProcessSortBinary(BinaryNode binary)
    {
        var result = this.ProcessAst(binary.Lhs);
        if (result is not PathNode pathResult)
        {
            pathResult = new PathNode { Position = result.Position };
            pathResult.Steps.Add(result);
        }

        var sortRhs = (SortNode)binary.Rhs;
        var sortStep = new SortNode { Position = binary.Position };
        foreach (var term in sortRhs.Terms)
        {
            var processedExpr = this.ProcessAst(term.Expression);
            PushAncestry(sortStep, processedExpr);
            sortStep.Terms.Add(new SortTerm { Descending = term.Descending, Expression = processedExpr });
        }

        pathResult.Steps.Add(sortStep);
        ResolveAncestry(pathResult);
        return pathResult;
    }

    private JsonataNode ProcessBindBinary(BinaryNode binary)
    {
        var result = new BindNode
        {
            Lhs = this.ProcessAst(binary.Lhs),
            Rhs = this.ProcessAst(binary.Rhs),
            Position = binary.Position,
        };
        PushAncestry(result, result.Rhs);
        return result;
    }

    private JsonataNode ProcessFocusBinary(BinaryNode binary)
    {
        var result = this.ProcessAst(binary.Lhs);
        JsonataNode step = result;
        if (result is PathNode pathNode)
        {
            step = pathNode.Steps[pathNode.Steps.Count - 1];
        }

        // Cannot have focus after predicates or sort
        var annotations = GetOrCreateAnnotations(step);
        if (annotations.Stages.Count > 0)
        {
            throw new JsonataException("S0215", "Focus binding after predicate", binary.Position);
        }

        if (step is SortNode)
        {
            throw new JsonataException("S0216", "Focus binding after sort", binary.Position);
        }

        if (binary.KeepArray)
        {
            step.KeepArray = true;
        }

        var varNode = (VariableNode)this.ProcessAst(binary.Rhs);
        annotations.Focus = varNode.Name;
        annotations.Tuple = true;
        return result;
    }

    private JsonataNode ProcessIndexBinary(BinaryNode binary)
    {
        var result = this.ProcessAst(binary.Lhs);
        JsonataNode step = result;
        PathNode? pathResult = result as PathNode;

        if (pathResult is not null)
        {
            step = pathResult.Steps[pathResult.Steps.Count - 1];
        }
        else
        {
            pathResult = new PathNode { Position = result.Position };
            pathResult.Steps.Add(result);

            // Move predicate annotations to stages
            var ann = GetAnnotations(result);
            if (ann?.Stages.Count > 0)
            {
                // Already in stages, nothing to move
            }
        }

        var varNode = (VariableNode)this.ProcessAst(binary.Rhs);
        var annotations = GetOrCreateAnnotations(step);
        if (annotations.Stages.Count == 0)
        {
            annotations.Index = varNode.Name;
        }
        else
        {
            // Add as an index stage (represented as a variable node tagged with the position)
            annotations.Stages.Add(new VariableNode
            {
                Name = varNode.Name,
                Position = binary.Position,
            });
        }

        annotations.Tuple = true;
        return pathResult;
    }

    private JsonataNode ProcessApplyBinary(BinaryNode binary)
    {
        var result = new ApplyNode
        {
            Lhs = this.ProcessAst(binary.Lhs),
            Rhs = this.ProcessAst(binary.Rhs),
            Position = binary.Position,
        };
        result.KeepArray = result.Lhs.KeepArray || result.Rhs.KeepArray;
        return result;
    }

    private JsonataNode ProcessUnary(UnaryNode unary)
    {
        var processedExpr = this.ProcessAst(unary.Expression);

        // Fold unary minus into numeric literals
        if (unary.Operator == "-" && processedExpr is NumberNode num)
        {
            num.Value = -num.Value;
            return num;
        }

        var result = new UnaryNode
        {
            Operator = unary.Operator,
            Expression = processedExpr,
            Position = unary.Position,
        };
        PushAncestry(result, processedExpr);
        return result;
    }

    private JsonataNode ProcessArrayConstructor(ArrayConstructorNode array)
    {
        var result = new ArrayConstructorNode { Position = array.Position, ConsArray = array.ConsArray, KeepArray = array.KeepArray };
        foreach (var item in array.Expressions)
        {
            var processed = this.ProcessAst(item);
            PushAncestry(result, processed);
            result.Expressions.Add(processed);
        }

        return result;
    }

    private JsonataNode ProcessObjectConstructor(ObjectConstructorNode obj)
    {
        var result = new ObjectConstructorNode { Position = obj.Position };
        foreach (var (key, value) in obj.Pairs)
        {
            var processedKey = this.ProcessAst(key);
            PushAncestry(result, processedKey);
            var processedValue = this.ProcessAst(value);
            PushAncestry(result, processedValue);
            result.Pairs.Add((processedKey, processedValue));
        }

        return result;
    }

    private JsonataNode ProcessFunctionCall(FunctionCallNode func)
    {
        var result = new FunctionCallNode
        {
            Procedure = this.ProcessAst(func.Procedure),
            Position = func.Position,
            KeepArray = func.KeepArray,
        };
        foreach (var arg in func.Arguments)
        {
            var processed = this.ProcessAst(arg);
            PushAncestry(result, processed);
            result.Arguments.Add(processed);
        }

        return result;
    }

    private JsonataNode ProcessPartial(PartialNode partial)
    {
        var result = new PartialNode
        {
            Procedure = this.ProcessAst(partial.Procedure),
            Position = partial.Position,
        };
        foreach (var arg in partial.Arguments)
        {
            var processed = this.ProcessAst(arg);
            PushAncestry(result, processed);
            result.Arguments.Add(processed);
        }

        return result;
    }

    private LambdaNode ProcessLambda(LambdaNode lambda)
    {
        var body = this.ProcessAst(lambda.Body);

        // TODO: Re-enable TCO once a proper trampoline is implemented in LambdaValue.Invoke.
        // The thunk-based approach requires evaluating thunk bodies in the thunk's defining
        // environment and re-resolving function calls, which the current compiled delegate
        // model doesn't support without restructuring.
        //// body = TailCallOptimize(body);

        return new LambdaNode
        {
            Body = body,
            Signature = lambda.Signature,
            Position = lambda.Position,
        }.WithParameters(lambda.Parameters);
    }

    private JsonataNode ProcessCondition(ConditionNode cond)
    {
        var result = new ConditionNode
        {
            Condition = this.ProcessAst(cond.Condition),
            Then = this.ProcessAst(cond.Then),
            Position = cond.Position,
        };
        PushAncestry(result, result.Condition);
        PushAncestry(result, result.Then);

        if (cond.Else is not null)
        {
            result.Else = this.ProcessAst(cond.Else);
            PushAncestry(result, result.Else);
        }

        return result;
    }

    private JsonataNode ProcessBlock(BlockNode block)
    {
        var result = new BlockNode { Position = block.Position };
        foreach (var expr in block.Expressions)
        {
            var processed = this.ProcessAst(expr);
            PushAncestry(result, processed);

            if (processed is ArrayConstructorNode { ConsArray: true }
                || (processed is PathNode p && p.Steps.Count > 0 && p.Steps[0] is ArrayConstructorNode { ConsArray: true }))
            {
                result.ConsArray = true;
            }

            result.Expressions.Add(processed);
        }

        return result;
    }

    private JsonataNode ProcessTransform(TransformNode transform)
    {
        var result = new TransformNode
        {
            Pattern = this.ProcessAst(transform.Pattern),
            Update = this.ProcessAst(transform.Update),
            Position = transform.Position,
        };

        if (transform.Delete is not null)
        {
            result.Delete = this.ProcessAst(transform.Delete);
        }

        return result;
    }

    private static JsonataNode TailCallOptimize(JsonataNode expr)
    {
        if (expr is FunctionCallNode funcCall)
        {
            // Wrap the function call in a thunk lambda for trampoline
            return new LambdaNode
            {
                Thunk = true,
                Body = funcCall,
                Position = funcCall.Position,
            };
        }

        if (expr is ConditionNode cond)
        {
            cond.Then = TailCallOptimize(cond.Then);
            if (cond.Else is not null)
            {
                cond.Else = TailCallOptimize(cond.Else);
            }

            return cond;
        }

        if (expr is BlockNode block && block.Expressions.Count > 0)
        {
            block.Expressions[block.Expressions.Count - 1] = TailCallOptimize(block.Expressions[block.Expressions.Count - 1]);
            return block;
        }

        return expr;
    }

    private static void PushAncestry(JsonataNode result, JsonataNode value)
    {
        List<ParentSlot>? slots = value.SeekingParent;
        if (value is ParentNode pn)
        {
            slots ??= [];
            slots.Add(pn.Slot);
        }

        if (slots is null)
        {
            return;
        }

        result.SeekingParent ??= [];
        result.SeekingParent.AddRange(slots);
    }

    private static void SeekParent(JsonataNode node, ParentSlot slot)
    {
        switch (node)
        {
            case NameNode name:
            case WildcardNode:
                slot.Level--;
                if (slot.Level == 0)
                {
                    var ann = GetOrCreateAnnotations(node);
                    ann.Tuple = true;
                }

                break;
            case ParentNode:
                slot.Level++;
                break;
            case BlockNode block:
                if (block.Expressions.Count > 0)
                {
                    SeekParent(block.Expressions[block.Expressions.Count - 1], slot);
                }

                break;
            case PathNode path:
                int index = path.Steps.Count - 1;
                SeekParent(path.Steps[index--], slot);
                while (slot.Level > 0 && index >= 0)
                {
                    SeekParent(path.Steps[index--], slot);
                }

                break;
            default:
                throw new JsonataException(
                    "S0217",
                    "Cannot derive ancestor",
                    node.Position,
                    node.Type.ToString());
        }
    }

    private static void ResolveAncestry(PathNode path)
    {
        if (path.Steps.Count == 0)
        {
            return;
        }

        var lastStep = path.Steps[path.Steps.Count - 1];
        List<ParentSlot>? slots = lastStep.SeekingParent is not null
            ? new(lastStep.SeekingParent)
            : [];

        if (lastStep is ParentNode pn)
        {
            slots.Add(pn.Slot);
        }

        foreach (var slot in slots)
        {
            int index = path.Steps.Count - 2;
            var currentSlot = slot;
            while (currentSlot.Level > 0)
            {
                if (index < 0)
                {
                    path.SeekingParent ??= [];
                    path.SeekingParent.Add(currentSlot);
                    break;
                }

                var step = path.Steps[index--];

                // Skip contiguous focus-bound steps
                var stepAnn = GetAnnotations(step);
                while (index >= 0 && stepAnn?.Focus is not null)
                {
                    var prevAnn = GetAnnotations(path.Steps[index]);
                    if (prevAnn?.Focus is null)
                    {
                        break;
                    }

                    step = path.Steps[index--];
                    stepAnn = prevAnn;
                }

                SeekParent(step, currentSlot);
            }
        }
    }

    private static StepAnnotations GetOrCreateAnnotations(JsonataNode node)
    {
        node.Annotations ??= new StepAnnotations();
        return node.Annotations;
    }

    private static StepAnnotations? GetAnnotations(JsonataNode node)
    {
        return node.Annotations;
    }
}
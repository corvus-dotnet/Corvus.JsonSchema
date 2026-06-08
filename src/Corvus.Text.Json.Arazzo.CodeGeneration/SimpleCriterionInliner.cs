// <copyright file="SimpleCriterionInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Inlines a <c>simple</c> success criterion that is a single comparison or a lone truthy operand
/// (plan §3.1, reification-free rebuild stage 3). The condition is parsed at generation time and
/// emitted as a direct evaluation against the live response / inputs / prior-step outputs, reusing
/// <see cref="Comparand"/> for the operand semantics (case-insensitive UTF-8 string equality, numeric
/// string coercion, JSON equality) so the inlined code matches the runtime interpreter exactly.
/// </summary>
/// <remarks>
/// <para>
/// Only single comparisons (<c>operand op operand</c>) and lone truthy operands are inlined. Anything
/// using the logical grammar (<c>||</c>, <c>&amp;&amp;</c>, a leading <c>!</c>, or grouping) or an
/// operand whose source cannot be navigated statically (<c>$response.header</c>, <c>$request.*</c>,
/// <c>$workflows</c>, …, or <c>$response.body</c> on a step that did not bind a body) causes
/// <see cref="TryEmit"/> to return <see langword="false"/>, and the caller falls back to compiling a
/// <see cref="CompiledCriterion"/>. Operands are resolved as side-effect-free statements before the
/// gate, so pre-computing them (rather than short-circuiting) is behaviour-preserving.
/// </para>
/// </remarks>
internal static class SimpleCriterionInliner
{
    private enum Op
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    /// <summary>
    /// Attempts to inline a <c>simple</c> condition.
    /// </summary>
    /// <param name="condition">The condition string.</param>
    /// <param name="responseVar">The in-scope response variable (for <c>$statusCode</c>).</param>
    /// <param name="responseBodyLocal">The in-scope live response-body local, or <see langword="null"/> if the step bound no body.</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="tmpPrefix">A unique prefix for emitted temporaries and baked literal fields.</param>
    /// <param name="fields">Accumulates any baked string-literal <c>static readonly byte[]</c> fields.</param>
    /// <param name="statements">When this method returns <see langword="true"/>, the operand-resolution statements to emit before the gate.</param>
    /// <param name="expression">When this method returns <see langword="true"/>, the boolean gate expression.</param>
    /// <returns><see langword="true"/> if the condition was inlined.</returns>
    public static bool TryEmit(
        string condition,
        string responseVar,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string tmpPrefix,
        StringBuilder fields,
        out string statements,
        out string expression)
    {
        statements = string.Empty;
        expression = string.Empty;

        var parser = new Parser(condition);

        // The logical grammar (||, &&, leading !, grouping) is not inlined yet — fall back.
        if (!parser.TryReadOperand(out string leftToken)
            || !parser.TryReadOptionalOperator(out Op op, out bool hasOperator))
        {
            return false;
        }

        string? rightToken = null;
        if (hasOperator)
        {
            if (!parser.TryReadOperand(out string read))
            {
                return false;
            }

            rightToken = read;
        }

        if (!parser.AtEnd)
        {
            return false;
        }

        var statementBuilder = new StringBuilder();

        if (!TryEmitOperand(leftToken, $"{tmpPrefix}L", responseVar, responseBodyLocal, inputsVariable, stepOutputLocals, fields, statementBuilder, out string leftExpr))
        {
            return false;
        }

        if (!hasOperator)
        {
            statements = statementBuilder.ToString();
            expression = $"{leftExpr}.IsTrue";
            return true;
        }

        if (!TryEmitOperand(rightToken!, $"{tmpPrefix}R", responseVar, responseBodyLocal, inputsVariable, stepOutputLocals, fields, statementBuilder, out string rightExpr))
        {
            return false;
        }

        statements = statementBuilder.ToString();
        expression = $"{leftExpr}.{MapOperator(op)}({rightExpr})";
        return true;
    }

    private static string MapOperator(Op op)
        => op switch
        {
            Op.Equal => "ValueEquals",
            Op.NotEqual => "ValueNotEquals",
            Op.LessThan => "LessThan",
            Op.LessThanOrEqual => "LessThanOrEqual",
            Op.GreaterThan => "GreaterThan",
            _ => "GreaterThanOrEqual",
        };

    /// <summary>
    /// Emits an operand as a <see cref="Comparand"/>-typed C# expression, appending any resolution
    /// statements for navigated operands. Returns <see langword="false"/> for an operand the executor
    /// cannot resolve statically.
    /// </summary>
    private static bool TryEmitOperand(
        string token,
        string baseName,
        string responseVar,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        StringBuilder fields,
        StringBuilder statements,
        out string comparandExpr)
    {
        comparandExpr = string.Empty;

        if (token.Length > 0 && token[0] == '$')
        {
            return TryEmitExpressionOperand(token, baseName, responseVar, responseBodyLocal, inputsVariable, stepOutputLocals, statements, out comparandExpr);
        }

        return TryEmitLiteralOperand(token, baseName, fields, out comparandExpr);
    }

    private static bool TryEmitExpressionOperand(
        string token,
        string baseName,
        string responseVar,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        StringBuilder statements,
        out string comparandExpr)
    {
        comparandExpr = string.Empty;
        (ArazzoExpression expression, string? navigationPointer) = SplitNavigation(token);

        // $statusCode: only the bare form (no navigation) is a number; anything else is undefined.
        if (expression.Source == ArazzoExpressionSource.StatusCode)
        {
            if (expression.HasJsonPointer || navigationPointer is not null)
            {
                return false;
            }

            comparandExpr = $"Comparand.FromNumber({responseVar}.StatusCode)";
            return true;
        }

        // Determine the navigable root and any leading property name.
        string root;
        string? name = null;
        switch (expression.Source)
        {
            case ArazzoExpressionSource.ResponseBody when responseBodyLocal is not null:
                root = responseBodyLocal;
                break;

            case ArazzoExpressionSource.Inputs when expression.Name is { } inputName:
                root = $"((JsonElement){inputsVariable})";
                name = inputName;
                break;

            case ArazzoExpressionSource.Steps when expression.ContainerId is { } stepId
                && expression.Name is { } outputName
                && stepOutputLocals.TryGetValue(stepId, out string? stepLocal):
                root = stepLocal;
                name = outputName;
                break;

            default:
                return false;
        }

        // Build the navigation chain: optional property, then the '#' pointer, then the '.'/'[]' pointer.
        var steps = new List<(bool IsProperty, string Value)>();
        if (name is not null)
        {
            steps.Add((true, name));
        }

        if (expression.JsonPointer is { Length: > 0 } fragmentPointer)
        {
            steps.Add((false, fragmentPointer));
        }

        if (navigationPointer is { Length: > 0 })
        {
            steps.Add((false, navigationPointer));
        }

        if (steps.Count == 0)
        {
            comparandExpr = baseName;
            statements.Append("Comparand ").Append(baseName).Append(" = Comparand.FromJsonElement(").Append(root).AppendLine(");");
            return true;
        }

        statements.Append("Comparand ").Append(baseName).AppendLine(" = Comparand.Undefined;");
        statements.Append("if (");
        for (int i = 0; i < steps.Count; i++)
        {
            string source = i == 0 ? root : $"{baseName}{(i - 1).ToString(CultureInfo.InvariantCulture)}";
            string outVar = $"{baseName}{i.ToString(CultureInfo.InvariantCulture)}";
            (bool isProperty, string value) = steps[i];
            string method = isProperty ? "TryGetProperty" : "TryResolvePointer";
            if (i > 0)
            {
                statements.Append(" && ");
            }

            statements.Append(source).Append('.').Append(method).Append('(')
                .Append(EmitText.Quote(value)).Append("u8, out JsonElement ").Append(outVar).Append(')');
        }

        statements.AppendLine(")");
        string last = $"{baseName}{(steps.Count - 1).ToString(CultureInfo.InvariantCulture)}";
        statements.AppendLine("{");
        statements.Append("    ").Append(baseName).Append(" = Comparand.FromJsonElement(").Append(last).AppendLine(");");
        statements.AppendLine("}");

        comparandExpr = baseName;
        return true;
    }

    private static bool TryEmitLiteralOperand(string token, string baseName, StringBuilder fields, out string comparandExpr)
    {
        comparandExpr = string.Empty;

        if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
        {
            string content = token[1..^1].Replace("''", "'", StringComparison.Ordinal);
            comparandExpr = BakeStringLiteral(content, baseName, fields);
            return true;
        }

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            comparandExpr = BakeStringLiteral(token[1..^1], baseName, fields);
            return true;
        }

        if (token == "true")
        {
            comparandExpr = "Comparand.FromBoolean(true)";
            return true;
        }

        if (token == "false")
        {
            comparandExpr = "Comparand.FromBoolean(false)";
            return true;
        }

        if (token == "null")
        {
            comparandExpr = "Comparand.Null";
            return true;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            comparandExpr = $"Comparand.FromNumber({number.ToString("R", CultureInfo.InvariantCulture)})";
            return true;
        }

        return false;
    }

    private static string BakeStringLiteral(string content, string baseName, StringBuilder fields)
    {
        string field = $"{baseName}Lit";
        fields.Append("private static readonly byte[] ").Append(field).Append(" = ")
            .Append(EmitText.Quote(content)).AppendLine("u8.ToArray();");
        return $"Comparand.FromUtf8String({field})";
    }

    /// <summary>
    /// Splits an operand token into a runtime expression and an optional JSON Pointer for trailing
    /// <c>.property</c>/<c>[index]</c> navigation — the exact algorithm the runtime
    /// <c>SimpleConditionEvaluator</c> uses, so the inlined navigation matches.
    /// </summary>
    private static (ArazzoExpression Expression, string? NavigationPointer) SplitNavigation(string token)
    {
        string baseToken = token;
        List<string>? segmentsRightToLeft = null;

        while (true)
        {
            ArazzoExpression expression = ArazzoExpression.Parse(baseToken);
            if (expression.Source != ArazzoExpressionSource.Literal)
            {
                return (expression, segmentsRightToLeft is null ? null : BuildPointer(segmentsRightToLeft));
            }

            if (!TryStripTrailingSegment(ref baseToken, out string segment))
            {
                return (expression, null);
            }

            (segmentsRightToLeft ??= []).Add(segment);
        }
    }

    private static bool TryStripTrailingSegment(ref string token, out string segment)
    {
        if (token.Length > 0 && token[^1] == ']')
        {
            int open = token.LastIndexOf('[');
            if (open >= 0)
            {
                segment = token[(open + 1)..^1];
                token = token[..open];
                return true;
            }
        }

        int dot = token.LastIndexOf('.');
        if (dot > 0)
        {
            segment = token[(dot + 1)..];
            token = token[..dot];
            return true;
        }

        segment = string.Empty;
        return false;
    }

    private static string BuildPointer(List<string> segmentsRightToLeft)
    {
        var builder = new StringBuilder();
        for (int i = segmentsRightToLeft.Count - 1; i >= 0; i--)
        {
            builder.Append('/');

            // RFC 6901 escaping: '~' -> '~0', '/' -> '~1'.
            builder.Append(segmentsRightToLeft[i].Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A minimal single-comparison parser mirroring the runtime tokenizer's operand/operator rules.
    /// </summary>
    private ref struct Parser(string text)
    {
        private readonly ReadOnlySpan<char> span = text;
        private int position = 0;

        public readonly bool AtEnd
        {
            get
            {
                int p = this.position;
                while (p < this.span.Length && char.IsWhiteSpace(this.span[p]))
                {
                    p++;
                }

                return p == this.span.Length;
            }
        }

        public bool TryReadOperand(out string token)
        {
            token = string.Empty;
            this.SkipWhitespace();

            // A leading '!' / '(' is the logical grammar — not inlined here.
            if (this.position >= this.span.Length || this.span[this.position] is '(' or ')' or '!')
            {
                return false;
            }

            int start = this.position;
            if (this.span[this.position] is '\'' or '"')
            {
                char quote = this.span[this.position];
                this.position++;
                while (this.position < this.span.Length)
                {
                    if (this.span[this.position] == quote)
                    {
                        if (this.position + 1 < this.span.Length && this.span[this.position + 1] == quote)
                        {
                            this.position += 2;
                            continue;
                        }

                        this.position++;
                        break;
                    }

                    this.position++;
                }
            }
            else
            {
                while (this.position < this.span.Length && !IsDelimiter(this.span[this.position]))
                {
                    this.position++;
                }
            }

            if (this.position == start)
            {
                return false;
            }

            token = this.span[start..this.position].ToString();
            return true;
        }

        public bool TryReadOptionalOperator(out Op op, out bool hasOperator)
        {
            op = default;
            hasOperator = false;
            this.SkipWhitespace();

            if (this.position >= this.span.Length)
            {
                return true;
            }

            // Logical operators are not inlined — bail so the whole criterion compiles.
            if (this.Matches("||") || this.Matches("&&"))
            {
                return false;
            }

            if (this.TryConsume("==")) { op = Op.Equal; hasOperator = true; return true; }
            if (this.TryConsume("!=")) { op = Op.NotEqual; hasOperator = true; return true; }
            if (this.TryConsume("<=")) { op = Op.LessThanOrEqual; hasOperator = true; return true; }
            if (this.TryConsume(">=")) { op = Op.GreaterThanOrEqual; hasOperator = true; return true; }
            if (this.TryConsume("<")) { op = Op.LessThan; hasOperator = true; return true; }
            if (this.TryConsume(">")) { op = Op.GreaterThan; hasOperator = true; return true; }

            // Trailing content that is not an operator (e.g. an unsupported token) — bail.
            return false;
        }

        private readonly bool Matches(string symbol) => this.span[this.position..].StartsWith(symbol);

        private bool TryConsume(string symbol)
        {
            if (this.span[this.position..].StartsWith(symbol))
            {
                this.position += symbol.Length;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (this.position < this.span.Length && char.IsWhiteSpace(this.span[this.position]))
            {
                this.position++;
            }
        }

        private static bool IsDelimiter(char c)
            => char.IsWhiteSpace(c) || c is '&' or '|' or '=' or '!' or '<' or '>' or '(' or ')';
    }
}
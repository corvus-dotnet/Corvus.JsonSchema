// <copyright file="SimpleCriterionInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Inlines a <c>simple</c> success criterion (plan §3.1, reification-free rebuild stage 3). The whole
/// condition grammar — <c>||</c>, <c>&amp;&amp;</c>, <c>!</c>, grouping, comparisons, and lone truthy
/// operands — is parsed at generation time and emitted as a direct evaluation against the live
/// response / inputs / prior-step outputs, reusing <see cref="Comparand"/> for the operand semantics
/// (case-insensitive UTF-8 string equality, numeric string coercion, JSON equality) so the inlined
/// code matches the runtime <c>SimpleConditionEvaluator</c> exactly.
/// </summary>
/// <remarks>
/// <para>
/// Operands are resolved as side-effect-free statements before the gate; the logical structure is then
/// emitted as a C# boolean expression referencing the resolved operands. Pre-computing the operands
/// (rather than short-circuiting through <c>&amp;&amp;</c>/<c>||</c>) is behaviour-preserving because
/// operand resolution has no side effects.
/// </para>
/// <para>
/// <see cref="TryEmit"/> returns <see langword="false"/> — and the caller falls back to compiling a
/// <see cref="CompiledCriterion"/> — when an operand's source cannot be navigated statically
/// (<c>$response.header</c>, <c>$request.*</c>, <c>$workflows</c>, …, or <c>$response.body</c> on a step
/// that did not bind a body) or the condition is malformed.
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

        var statementBuilder = new StringBuilder();
        var parser = new Parser(condition, responseVar, responseBodyLocal, inputsVariable, stepOutputLocals, tmpPrefix, fields, statementBuilder);

        string? expr = parser.ParseOr();
        if (expr is null || !parser.AtEnd)
        {
            return false;
        }

        statements = statementBuilder.ToString();
        expression = expr;
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
            string source = i == 0 ? root : $"{baseName}n{(i - 1).ToString(CultureInfo.InvariantCulture)}";
            string outVar = $"{baseName}n{i.ToString(CultureInfo.InvariantCulture)}";
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
        string last = $"{baseName}n{(steps.Count - 1).ToString(CultureInfo.InvariantCulture)}";
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
    /// A recursive-descent parser that mirrors the runtime <c>SimpleConditionEvaluator</c> grammar but
    /// emits a C# boolean expression (and operand-resolution statements) instead of a node tree.
    /// </summary>
    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> span;
        private readonly string responseVar;
        private readonly string? responseBodyLocal;
        private readonly string inputsVariable;
        private readonly IReadOnlyDictionary<string, string> stepOutputLocals;
        private readonly string tmpPrefix;
        private readonly StringBuilder fields;
        private readonly StringBuilder statements;
        private int position;
        private int operandCount;

        public Parser(
            string text,
            string responseVar,
            string? responseBodyLocal,
            string inputsVariable,
            IReadOnlyDictionary<string, string> stepOutputLocals,
            string tmpPrefix,
            StringBuilder fields,
            StringBuilder statements)
        {
            this.span = text;
            this.responseVar = responseVar;
            this.responseBodyLocal = responseBodyLocal;
            this.inputsVariable = inputsVariable;
            this.stepOutputLocals = stepOutputLocals;
            this.tmpPrefix = tmpPrefix;
            this.fields = fields;
            this.statements = statements;
            this.position = 0;
            this.operandCount = 0;
        }

        public bool AtEnd
        {
            get
            {
                this.SkipWhitespace();
                return this.position == this.span.Length;
            }
        }

        public string? ParseOr()
        {
            string? left = this.ParseAnd();
            while (left is not null && this.TryConsume("||"))
            {
                string? right = this.ParseAnd();
                left = right is null ? null : $"({left} || {right})";
            }

            return left;
        }

        private string? ParseAnd()
        {
            string? left = this.ParseNot();
            while (left is not null && this.TryConsume("&&"))
            {
                string? right = this.ParseNot();
                left = right is null ? null : $"({left} && {right})";
            }

            return left;
        }

        private string? ParseNot()
        {
            this.SkipWhitespace();

            // A leading '!' is the NOT operator — but not when it is the start of '!='.
            if (this.position < this.span.Length
                && this.span[this.position] == '!'
                && (this.position + 1 >= this.span.Length || this.span[this.position + 1] != '='))
            {
                this.position++;
                string? inner = this.ParseNot();
                return inner is null ? null : $"!({inner})";
            }

            return this.ParseComparison();
        }

        private string? ParseComparison()
        {
            this.SkipWhitespace();
            if (this.TryConsume("("))
            {
                string? grouped = this.ParseOr();
                if (grouped is null || !this.TryConsume(")"))
                {
                    return null;
                }

                // No extra parentheses: ParseOr/ParseAnd already parenthesize multi-term expressions,
                // and a single term is atomic (a method call or a negation), so precedence is preserved.
                return grouped;
            }

            if (!this.TryEmitNextOperand(out string leftExpr))
            {
                return null;
            }

            if (this.TryParseOperator(out Op op))
            {
                if (!this.TryEmitNextOperand(out string rightExpr))
                {
                    return null;
                }

                return $"{leftExpr}.{MapOperator(op)}({rightExpr})";
            }

            return $"{leftExpr}.IsTrue";
        }

        private bool TryEmitNextOperand(out string comparandExpr)
        {
            comparandExpr = string.Empty;
            this.SkipWhitespace();
            string token = this.ReadOperandToken();
            if (token.Length == 0)
            {
                return false;
            }

            string baseName = $"{this.tmpPrefix}o{this.operandCount.ToString(CultureInfo.InvariantCulture)}";
            this.operandCount++;
            return TryEmitOperand(
                token, baseName, this.responseVar, this.responseBodyLocal, this.inputsVariable,
                this.stepOutputLocals, this.fields, this.statements, out comparandExpr);
        }

        private string ReadOperandToken()
        {
            int start = this.position;
            if (this.position < this.span.Length && (this.span[this.position] == '\'' || this.span[this.position] == '"'))
            {
                char quote = this.span[this.position];
                this.position++;
                while (this.position < this.span.Length)
                {
                    if (this.span[this.position] == quote)
                    {
                        // A doubled quote ('') is an escaped quote, not the terminator.
                        if (this.position + 1 < this.span.Length && this.span[this.position + 1] == quote)
                        {
                            this.position += 2;
                            continue;
                        }

                        this.position++; // closing quote
                        break;
                    }

                    this.position++;
                }

                return this.span[start..this.position].ToString();
            }

            while (this.position < this.span.Length && !IsDelimiter(this.span[this.position]))
            {
                this.position++;
            }

            return this.span[start..this.position].ToString();
        }

        private bool TryParseOperator(out Op op)
        {
            this.SkipWhitespace();
            if (this.TryConsume("==")) { op = Op.Equal; return true; }
            if (this.TryConsume("!=")) { op = Op.NotEqual; return true; }
            if (this.TryConsume("<=")) { op = Op.LessThanOrEqual; return true; }
            if (this.TryConsume(">=")) { op = Op.GreaterThanOrEqual; return true; }
            if (this.TryConsume("<")) { op = Op.LessThan; return true; }
            if (this.TryConsume(">")) { op = Op.GreaterThan; return true; }
            op = default;
            return false;
        }

        private bool TryConsume(string symbol)
        {
            this.SkipWhitespace();
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
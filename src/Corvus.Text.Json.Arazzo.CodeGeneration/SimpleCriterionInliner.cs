// <copyright file="SimpleCriterionInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.OpenApi.CodeGeneration;

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
    /// <param name="sources">The step's live JSON sources (response body / message payload / message headers).</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor on the inputs model, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="responseHeaders">The operation's declared response headers (for <c>$response.header.&lt;name&gt;</c> operands).</param>
    /// <param name="requestContext">The step's request-side values (for <c>$method</c> and <c>$request.*</c> operands).</param>
    /// <param name="tmpPrefix">A unique prefix for emitted temporaries and baked literal fields.</param>
    /// <param name="fields">Accumulates any baked string-literal <c>static readonly byte[]</c> fields.</param>
    /// <param name="statements">When this method returns <see langword="true"/>, the operand-resolution statements to emit before the gate.</param>
    /// <param name="expression">When this method returns <see langword="true"/>, the boolean gate expression.</param>
    /// <returns><see langword="true"/> if the condition was inlined.</returns>
    public static bool TryEmit(
        string condition,
        string responseVar,
        CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        in StepRequestContext requestContext,
        string tmpPrefix,
        StringBuilder fields,
        out string statements,
        out string expression)
    {
        statements = string.Empty;
        expression = string.Empty;

        var statementBuilder = new StringBuilder();
        var parser = new Parser(condition, responseVar, sources, inputsVariable, stepOutputLocals, inputAccessors, responseHeaders, requestContext, tmpPrefix, fields, statementBuilder);

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
        CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        in StepRequestContext requestContext,
        StringBuilder fields,
        StringBuilder statements,
        out string comparandExpr)
    {
        comparandExpr = string.Empty;

        if (token.Length > 0 && token[0] == '$')
        {
            return TryEmitExpressionOperand(token, baseName, responseVar, sources, inputsVariable, stepOutputLocals, inputAccessors, responseHeaders, requestContext, fields, statements, out comparandExpr);
        }

        return TryEmitLiteralOperand(token, baseName, fields, out comparandExpr);
    }

    private static bool TryEmitExpressionOperand(
        string token,
        string baseName,
        string responseVar,
        CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        in StepRequestContext requestContext,
        StringBuilder fields,
        StringBuilder statements,
        out string comparandExpr)
    {
        comparandExpr = string.Empty;
        (ArazzoExpression expression, string? navigationPointer) = CriterionExpressionParsing.SplitNavigation(token);
        bool bare = !expression.HasJsonPointer && navigationPointer is null;

        // $statusCode: only the bare form (no navigation) is a number; anything else is undefined.
        if (expression.Source == ArazzoExpressionSource.StatusCode)
        {
            if (!bare)
            {
                return false;
            }

            comparandExpr = $"Comparand.FromNumber({responseVar}.StatusCode)";
            return true;
        }

        // $method: the operation's HTTP method — a compile-time constant baked as a string.
        if (expression.Source == ArazzoExpressionSource.Method && bare)
        {
            comparandExpr = BakeStringLiteral(requestContext.Method, baseName, fields);
            return true;
        }

        // $request.<location>.<name> / $request.body: resolve the value the step bound to the request.
        if (bare && TryGetRequestValue(expression.Source, expression.Name, requestContext, out ArgumentValueKind requestKind, out string? requestValue))
        {
            return TryEmitRequestValue(
                requestKind, requestValue!, baseName, responseVar, sources, inputsVariable,
                stepOutputLocals, inputAccessors, responseHeaders, requestContext, fields, statements, out comparandExpr);
        }

        // $response.header.<name>: read the generated response property. A schema-less header is a
        // string?; a typed header is a generated JSON value read as a JsonElement. Both map an absent
        // header to an undefined comparand. Only the bare form (no navigation) is supported.
        if (expression.Source == ArazzoExpressionSource.ResponseHeader
            && expression.Name is { } headerName
            && bare
            && CriterionExpressionParsing.TryResolveResponseHeader(responseHeaders, headerName, out ResponseHeaderInfo header))
        {
            comparandExpr = header.IsString
                ? $"Comparand.FromString({responseVar}.{header.PropertyName})"
                : $"Comparand.FromJsonElement((JsonElement){responseVar}.{header.PropertyName})";
            return true;
        }

        // Navigate the operand to a JsonElement (default when absent), then read it as a Comparand —
        // FromJsonElement maps an undefined element to an undefined comparand, so a missing operand
        // makes the comparison false, matching the runtime.
        if (!CriterionExpressionParsing.TryEmitElementNavigation(
                expression, navigationPointer, baseName, sources, inputsVariable, stepOutputLocals, inputAccessors, statements, out string elementLocal))
        {
            return false;
        }

        comparandExpr = $"Comparand.FromJsonElement({elementLocal})";
        return true;
    }

    /// <summary>
    /// Looks up the step's bound value for a <c>$request.&lt;location&gt;.&lt;name&gt;</c> or
    /// <c>$request.body</c> operand.
    /// </summary>
    private static bool TryGetRequestValue(
        ArazzoExpressionSource source,
        string? name,
        in StepRequestContext requestContext,
        out ArgumentValueKind kind,
        out string? value)
    {
        kind = default;
        value = null;

        if (source is ArazzoExpressionSource.RequestPath or ArazzoExpressionSource.RequestQuery or ArazzoExpressionSource.RequestHeader)
        {
            if (name is null)
            {
                return false;
            }

            foreach (StepArgument argument in requestContext.Arguments)
            {
                if (string.Equals(argument.Name, name, StringComparison.Ordinal))
                {
                    kind = argument.Kind;
                    value = argument.Value;
                    return true;
                }
            }

            return false;
        }

        if (source == ArazzoExpressionSource.RequestBody && requestContext.Body is { } body)
        {
            kind = body.Kind;
            value = body.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Emits a comparand for a request-bound value: an expression value is resolved like any other
    /// operand (so it picks up typed input accessors, prior-step outputs, …); a scalar literal is baked
    /// directly. Interpolated/composite request values are not inlined.
    /// </summary>
    private static bool TryEmitRequestValue(
        ArgumentValueKind kind,
        string value,
        string baseName,
        string responseVar,
        CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        in StepRequestContext requestContext,
        StringBuilder fields,
        StringBuilder statements,
        out string comparandExpr)
    {
        comparandExpr = string.Empty;

        switch (kind)
        {
            case ArgumentValueKind.Expression:
                return TryEmitExpressionOperand(
                    value, baseName, responseVar, sources, inputsVariable, stepOutputLocals,
                    inputAccessors, responseHeaders, requestContext, fields, statements, out comparandExpr);

            case ArgumentValueKind.LiteralString:
                comparandExpr = BakeStringLiteral(value, baseName, fields);
                return true;

            case ArgumentValueKind.LiteralNumber:
                comparandExpr = $"Comparand.FromNumber({value})";
                return true;

            case ArgumentValueKind.LiteralBoolean:
                comparandExpr = $"Comparand.FromBoolean({value})";
                return true;

            case ArgumentValueKind.LiteralNull:
                comparandExpr = "Comparand.Null";
                return true;

            default:
                // Interpolation / composite request values are not inlined.
                return false;
        }
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
    /// A recursive-descent parser that mirrors the runtime <c>SimpleConditionEvaluator</c> grammar but
    /// emits a C# boolean expression (and operand-resolution statements) instead of a node tree.
    /// </summary>
    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> span;
        private readonly string responseVar;
        private readonly CriterionSources sources;
        private readonly string inputsVariable;
        private readonly IReadOnlyDictionary<string, string> stepOutputLocals;
        private readonly IReadOnlyDictionary<string, string>? inputAccessors;
        private readonly IReadOnlyList<ResponseHeaderInfo>? responseHeaders;
        private readonly StepRequestContext requestContext;
        private readonly string tmpPrefix;
        private readonly StringBuilder fields;
        private readonly StringBuilder statements;
        private int position;
        private int operandCount;

        public Parser(
            string text,
            string responseVar,
            CriterionSources sources,
            string inputsVariable,
            IReadOnlyDictionary<string, string> stepOutputLocals,
            IReadOnlyDictionary<string, string>? inputAccessors,
            IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
            in StepRequestContext requestContext,
            string tmpPrefix,
            StringBuilder fields,
            StringBuilder statements)
        {
            this.span = text;
            this.responseVar = responseVar;
            this.sources = sources;
            this.inputsVariable = inputsVariable;
            this.stepOutputLocals = stepOutputLocals;
            this.inputAccessors = inputAccessors;
            this.responseHeaders = responseHeaders;
            this.requestContext = requestContext;
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
                token, baseName, this.responseVar, this.sources, this.inputsVariable,
                this.stepOutputLocals, this.inputAccessors, this.responseHeaders, this.requestContext, this.fields, this.statements, out comparandExpr);
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
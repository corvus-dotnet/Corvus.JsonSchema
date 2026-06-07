// <copyright file="CompiledCriterion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text;
using Corvus.Text.Json.JsonPath;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A compiled Arazzo Criterion Object, ready to evaluate against a
/// <see cref="WorkflowExecutionContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// All expensive work — parsing a <c>simple</c> condition, compiling a <c>regex</c>, and parsing
/// the context runtime expression — happens once in <see cref="Compile"/>. <see cref="Evaluate"/>
/// is the hot path and performs no parsing or compilation: the <see cref="Regex"/> instance is
/// reused, and <c>jsonpath</c> queries are run through <see cref="JsonPathEvaluator.Default"/>,
/// which caches the compiled query by expression so it is compiled at most once per process.
/// </para>
/// <para>
/// Per the spec, <c>regex</c>/<c>jsonpath</c>/<c>xpath</c> conditions may contain embedded
/// <c>{expression}</c> fragments that are substituted before the pattern/query is evaluated. When a
/// condition contains such fragments it is <em>dynamic</em>: it is interpolated against the context
/// on each evaluation (the resulting pattern/query is still cache-compiled by the regex engine /
/// <see cref="JsonPathEvaluator"/>). Conditions without embedded expressions are fully compiled once.
/// </para>
/// <para>
/// This is the <em>interpreted</em> evaluator, used for dynamic execution. Code-generated
/// executors (.NET 10+) instead emit criteria ahead-of-time for zero per-evaluation overhead:
/// JSONPath via the Corvus JSONPath source generator and regular expressions via
/// <c>[GeneratedRegex]</c>.
/// </para>
/// </remarks>
public sealed class CompiledCriterion
{
    private readonly CriterionType type;
    private readonly SimpleConditionEvaluator? simple;
    private readonly Regex? regex;
    private readonly TimeSpan regexTimeout;
    private readonly byte[]? jsonPathUtf8;                       // static jsonpath query, baked UTF-8.
    private readonly CompiledInterpolationTemplate? dynamicTemplate; // dynamic regex/jsonpath condition.
    private readonly ArazzoExpression context;
    private readonly bool isDynamic;

    private CompiledCriterion(
        CriterionType type,
        SimpleConditionEvaluator? simple,
        Regex? regex,
        TimeSpan regexTimeout,
        byte[]? jsonPathUtf8,
        CompiledInterpolationTemplate? dynamicTemplate,
        ArazzoExpression context,
        bool isDynamic)
    {
        this.type = type;
        this.simple = simple;
        this.regex = regex;
        this.regexTimeout = regexTimeout;
        this.jsonPathUtf8 = jsonPathUtf8;
        this.dynamicTemplate = dynamicTemplate;
        this.context = context;
        this.isDynamic = isDynamic;
    }

    /// <summary>
    /// Compiles a criterion.
    /// </summary>
    /// <param name="type">The criterion type.</param>
    /// <param name="condition">
    /// The condition: a simple expression, a regular expression pattern, or a JSONPath query,
    /// depending on <paramref name="type"/>. Regex/JSONPath conditions may embed <c>{expression}</c>
    /// fragments.
    /// </param>
    /// <param name="contextExpression">
    /// The runtime expression supplying the value the criterion is evaluated against. Required for
    /// <see cref="CriterionType.Regex"/> and <see cref="CriterionType.JsonPath"/>; ignored for
    /// <see cref="CriterionType.Simple"/>.
    /// </param>
    /// <param name="regexTimeout">
    /// The maximum time a <c>regex</c> match may run before it is abandoned (returning a failed
    /// match). Defaults to one second. Bounds catastrophic backtracking.
    /// </param>
    /// <returns>The compiled criterion.</returns>
    /// <exception cref="ArgumentException">A required context expression was not supplied.</exception>
    public static CompiledCriterion Compile(
        CriterionType type,
        string condition,
        string? contextExpression = null,
        TimeSpan? regexTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        switch (type)
        {
            case CriterionType.Simple:
                return new CompiledCriterion(type, SimpleConditionEvaluator.Compile(condition), null, default, null, null, default, false);

            case CriterionType.Regex:
            {
                ArazzoExpression ctx = RequireContext(contextExpression);
                TimeSpan timeout = regexTimeout ?? TimeSpan.FromSeconds(1);
                if (HasEmbeddedExpression(condition))
                {
                    return new CompiledCriterion(type, null, null, timeout, null, CompiledInterpolationTemplate.Compile(condition), ctx, true);
                }

                return new CompiledCriterion(type, null, new Regex(condition, RegexOptions.CultureInvariant, timeout), timeout, null, null, ctx, false);
            }

            case CriterionType.JsonPath:
            {
                ArazzoExpression ctx = RequireContext(contextExpression);
                if (HasEmbeddedExpression(condition))
                {
                    return new CompiledCriterion(type, null, null, default, null, CompiledInterpolationTemplate.Compile(condition), ctx, true);
                }

                return new CompiledCriterion(type, null, null, default, Encoding.UTF8.GetBytes(condition), null, ctx, false);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /// <summary>
    /// Evaluates the criterion against the supplied context.
    /// </summary>
    /// <param name="executionContext">The workflow execution context.</param>
    /// <returns>The boolean result; <see langword="false"/> if the context value cannot be resolved.</returns>
    public bool Evaluate(WorkflowExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        switch (this.type)
        {
            case CriterionType.Simple:
                return this.simple!.Evaluate(executionContext);

            case CriterionType.Regex:
                return this.EvaluateRegex(executionContext);

            case CriterionType.JsonPath:
                return this.EvaluateJsonPath(executionContext);

            default:
                return false;
        }
    }

    private static bool HasEmbeddedExpression(string condition)
        => condition.Contains('{', StringComparison.Ordinal);

    private static ArazzoExpression RequireContext(string? contextExpression)
    {
        if (string.IsNullOrEmpty(contextExpression))
        {
            throw new ArgumentException("A context expression is required for regex and jsonpath criteria.", nameof(contextExpression));
        }

        return ArazzoExpression.Parse(contextExpression);
    }

    private bool EvaluateRegex(WorkflowExecutionContext executionContext)
    {
        // A compile-time literal context is already an unescaped string on the expression.
        if (this.context.Source == ArazzoExpressionSource.Literal)
        {
            return this.Match(executionContext, this.context.LiteralValue ?? string.Empty);
        }

        // Zero-allocation path for string/scalar contexts: resolve as the *unescaped* UTF-8 string
        // (GetUtf8String), transcode to a stack/pooled char span, and match. Regex requires UTF-16.
        if (executionContext.TryResolveUtf8(this.context, out ResolvedUtf8 resolved))
        {
            using (resolved)
            {
                return this.MatchUtf8(executionContext, resolved.Span);
            }
        }

        // $statusCode is an integer with no resident string; format it straight into a char buffer
        // rather than allocating a managed string on the hot path.
        if (executionContext.TryGetStatusCode(this.context, out int status))
        {
            Span<char> buffer = stackalloc char[16];
            status.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture);
            return this.Match(executionContext, buffer[..written]);
        }

        // A regex criterion needs textual context; a non-string JSON value does not match.
        return false;
    }

    private bool MatchUtf8(WorkflowExecutionContext executionContext, ReadOnlySpan<byte> utf8)
    {
        const int StackThreshold = 256;

        int charCount = Encoding.UTF8.GetCharCount(utf8);
        char[]? rented = null;
        Span<char> chars = charCount <= StackThreshold ? stackalloc char[StackThreshold] : (rented = ArrayPool<char>.Shared.Rent(charCount));

        try
        {
            int written = Encoding.UTF8.GetChars(utf8, chars);
            return this.Match(executionContext, chars[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private bool Match(WorkflowExecutionContext executionContext, ReadOnlySpan<char> input)
    {
        try
        {
            if (!this.isDynamic)
            {
                return this.regex!.IsMatch(input);
            }

            // Build the pattern from the compiled template (the parse happened at Compile time).
            // A managed string is required here only because Regex has no span-pattern overload;
            // the static IsMatch caches the compiled pattern, so a dynamic pattern is not
            // recompiled every evaluation.
            var builder = new Utf8ValueStringBuilder(64);
            try
            {
                if (!executionContext.TryAppendTemplate(this.dynamicTemplate!, ref builder))
                {
                    return false;
                }

                string pattern = Encoding.UTF8.GetString(builder.AsSpan());
                return Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant, this.regexTimeout);
            }
            finally
            {
                builder.Dispose();
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private bool EvaluateJsonPath(WorkflowExecutionContext executionContext)
    {
        if (!executionContext.TryResolveValue(this.context, out JsonElement data))
        {
            return false;
        }

        if (!this.isDynamic)
        {
            // Static query: baked UTF-8, evaluated via the zero-allocation UTF-8 overload.
            using JsonPathResult staticResult = JsonPathEvaluator.Default.QueryNodes(this.jsonPathUtf8!, data);
            return staticResult.Count > 0;
        }

        // Dynamic query: build UTF-8 from the compiled template, then query without a managed string.
        var builder = new Utf8ValueStringBuilder(64);
        try
        {
            if (!executionContext.TryAppendTemplate(this.dynamicTemplate!, ref builder))
            {
                return false;
            }

            using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(builder.AsSpan(), data);
            return result.Count > 0;
        }
        finally
        {
            builder.Dispose();
        }
    }
}
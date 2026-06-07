// <copyright file="CompiledCriterion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
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
    private readonly string? jsonPathQuery;
    private readonly ArazzoExpression context;

    private CompiledCriterion(
        CriterionType type,
        SimpleConditionEvaluator? simple,
        Regex? regex,
        string? jsonPathQuery,
        ArazzoExpression context)
    {
        this.type = type;
        this.simple = simple;
        this.regex = regex;
        this.jsonPathQuery = jsonPathQuery;
        this.context = context;
    }

    /// <summary>
    /// Compiles a criterion.
    /// </summary>
    /// <param name="type">The criterion type.</param>
    /// <param name="condition">
    /// The condition: a simple expression, a regular expression pattern, or a JSONPath query,
    /// depending on <paramref name="type"/>.
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
                return new CompiledCriterion(type, SimpleConditionEvaluator.Compile(condition), null, null, default);

            case CriterionType.Regex:
            {
                ArazzoExpression ctx = RequireContext(contextExpression);
                var compiledRegex = new Regex(
                    condition,
                    RegexOptions.CultureInvariant,
                    regexTimeout ?? TimeSpan.FromSeconds(1));
                return new CompiledCriterion(type, null, compiledRegex, null, ctx);
            }

            case CriterionType.JsonPath:
            {
                ArazzoExpression ctx = RequireContext(contextExpression);
                return new CompiledCriterion(type, null, null, condition, ctx);
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
                if (!executionContext.TryResolveString(this.context, out string text))
                {
                    return false;
                }

                try
                {
                    return this.regex!.IsMatch(text);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }

            case CriterionType.JsonPath:
                if (!executionContext.TryResolveValue(this.context, out JsonElement data))
                {
                    return false;
                }

                using (JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(this.jsonPathQuery!, data))
                {
                    return result.Count > 0;
                }

            default:
                return false;
        }
    }

    private static ArazzoExpression RequireContext(string? contextExpression)
    {
        if (string.IsNullOrEmpty(contextExpression))
        {
            throw new ArgumentException("A context expression is required for regex and jsonpath criteria.", nameof(contextExpression));
        }

        return ArazzoExpression.Parse(contextExpression);
    }
}
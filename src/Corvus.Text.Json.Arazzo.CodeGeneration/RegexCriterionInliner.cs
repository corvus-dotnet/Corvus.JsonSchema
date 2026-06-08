// <copyright file="RegexCriterionInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Inlines a <c>regex</c> success criterion (plan §3.1, reification-free rebuild stage 3) by emitting a
/// <c>[GeneratedRegex]</c> partial method on the executor — so the pattern is compiled ahead-of-time by
/// the regular-expression source generator — and resolving the criterion's context value statically,
/// matching via the runtime <see cref="RegexCriterion"/> helper (which owns the spec's behaviour: a
/// non-string value never matches and a match timeout yields <see langword="false"/>).
/// </summary>
/// <remarks>
/// <para>
/// Falls back to a runtime <see cref="CompiledCriterion"/> when the pattern embeds an
/// <c>{expression}</c> fragment (a <em>dynamic</em> pattern, which <c>[GeneratedRegex]</c> cannot
/// express) or the context is a source that cannot be resolved statically (a header, <c>$url</c>,
/// <c>$request.*</c>, …, or <c>$response.body</c> on a step that did not bind a body).
/// </para>
/// </remarks>
internal static class RegexCriterionInliner
{
    /// <summary>
    /// Attempts to inline a <c>regex</c> criterion.
    /// </summary>
    /// <param name="pattern">The regular-expression pattern (the criterion condition).</param>
    /// <param name="contextExpression">The runtime expression supplying the value matched against the pattern.</param>
    /// <param name="responseVar">The in-scope response variable (for <c>$statusCode</c>).</param>
    /// <param name="responseBodyLocal">The in-scope live response-body local, or <see langword="null"/> if the step bound no body.</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor on the inputs model, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="responseHeaders">The operation's declared response headers (for a <c>$response.header.&lt;name&gt;</c> context).</param>
    /// <param name="requestContext">The step's request-side values (for a <c>$method</c> context).</param>
    /// <param name="tmpPrefix">A unique prefix for the generated regex method and any temporaries.</param>
    /// <param name="members">Accumulates the <c>[GeneratedRegex]</c> partial-method declaration (a class member).</param>
    /// <param name="statements">When this method returns <see langword="true"/>, the context-resolution statements to emit before the gate.</param>
    /// <param name="expression">When this method returns <see langword="true"/>, the boolean gate expression.</param>
    /// <returns><see langword="true"/> if the criterion was inlined.</returns>
    public static bool TryEmit(
        string pattern,
        string? contextExpression,
        string responseVar,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        in StepRequestContext requestContext,
        string tmpPrefix,
        StringBuilder members,
        out string statements,
        out string expression)
    {
        statements = string.Empty;
        expression = string.Empty;

        if (string.IsNullOrEmpty(contextExpression))
        {
            return false;
        }

        // An embedded {expression} makes the pattern dynamic; [GeneratedRegex] needs a constant pattern.
        // (Mirrors the runtime's HasEmbeddedExpression, which keys on '{'.)
        if (pattern.Contains('{', StringComparison.Ordinal))
        {
            return false;
        }

        ArazzoExpression context = ArazzoExpression.Parse(contextExpression);
        var statementBuilder = new StringBuilder();
        string contextValue;

        if (context.Source == ArazzoExpressionSource.Literal)
        {
            // A literal context is matched verbatim (the runtime uses LiteralValue directly).
            contextValue = EmitText.Quote(context.LiteralValue ?? string.Empty);
        }
        else if (context.Source == ArazzoExpressionSource.StatusCode && !context.HasJsonPointer)
        {
            contextValue = $"{responseVar}.StatusCode";
        }
        else if (context.Source == ArazzoExpressionSource.Method && !context.HasJsonPointer)
        {
            // The operation's HTTP method is a compile-time constant string.
            contextValue = EmitText.Quote(requestContext.Method);
        }
        else if (context.Source == ArazzoExpressionSource.ResponseHeader
            && context.Name is { } headerName
            && !context.HasJsonPointer
            && CriterionExpressionParsing.TryResolveResponseHeader(responseHeaders, headerName, out ResponseHeaderInfo header))
        {
            // A schema-less header is a string? (the string? overload handles null); a typed header is
            // read as a JsonElement (the JsonElement overload matches only string-kind values).
            contextValue = header.IsString
                ? $"{responseVar}.{header.PropertyName}"
                : $"(JsonElement){responseVar}.{header.PropertyName}";
        }
        else if (CriterionExpressionParsing.TryEmitElementNavigation(
            context, null, $"{tmpPrefix}ctx", responseBodyLocal, inputsVariable, stepOutputLocals, inputAccessors, statementBuilder, out string elementLocal))
        {
            contextValue = elementLocal;
        }
        else
        {
            return false;
        }

        string method = $"{tmpPrefix}Regex";
        members.Append("[GeneratedRegex(").Append(EmitText.Quote(pattern)).AppendLine(", RegexOptions.CultureInvariant, 1000)]");
        members.Append("private static partial Regex ").Append(method).AppendLine("();");

        statements = statementBuilder.ToString();
        expression = $"RegexCriterion.IsMatch({method}(), {contextValue})";
        return true;
    }
}
// <copyright file="JsonPathCriterionInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.JsonPath;
using Corvus.Text.Json.JsonPath.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Inlines a <c>jsonpath</c> success criterion (plan §3.1, reification-free rebuild stage 3) by
/// compiling the JSONPath query ahead-of-time — via <see cref="JsonPathCodeGenerator"/> — into a
/// sibling class emitted alongside the executor, then querying the statically-resolved context value
/// and testing for at least one match. There is no runtime interpreter, no <c>{expression}</c>
/// substitution, and no per-evaluation query compilation.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the regex inliner (which relies on the consumer-side <c>[GeneratedRegex]</c> generator), the
/// JSONPath engine is invoked directly at Arazzo-generation time and its output is grafted into the
/// executor's source, so the result is fully self-contained — no companion <c>.jsonpath</c> files and
/// no <c>AdditionalFiles</c> wiring.
/// </para>
/// <para>
/// Falls back to a runtime <see cref="CompiledCriterion"/> when the query embeds an <c>{expression}</c>
/// fragment (a <em>dynamic</em> query), the context is not a statically navigable JSON value
/// (<c>$statusCode</c>, a header, a literal, …), or the query fails to compile.
/// </para>
/// </remarks>
internal static class JsonPathCriterionInliner
{
    /// <summary>
    /// Attempts to inline a <c>jsonpath</c> criterion.
    /// </summary>
    /// <param name="query">The JSONPath query (the criterion condition).</param>
    /// <param name="contextExpression">The runtime expression supplying the JSON value the query runs against.</param>
    /// <param name="sources">The step's live JSON sources (response body / message payload / message headers).</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor on the inputs model, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="namespaceName">The executor's namespace (the sibling query class is emitted into it).</param>
    /// <param name="tmpPrefix">A unique prefix for the query class and any temporaries.</param>
    /// <param name="auxiliaryTypes">Accumulates the generated sibling query class source.</param>
    /// <param name="statements">When this method returns <see langword="true"/>, the context-resolution and match statements to emit before the gate.</param>
    /// <param name="expression">When this method returns <see langword="true"/>, the boolean gate expression.</param>
    /// <returns><see langword="true"/> if the criterion was inlined.</returns>
    public static bool TryEmit(
        string query,
        string? contextExpression,
        CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string namespaceName,
        string tmpPrefix,
        StringBuilder auxiliaryTypes,
        out string statements,
        out string expression)
    {
        statements = string.Empty;
        expression = string.Empty;

        if (string.IsNullOrEmpty(contextExpression))
        {
            return false;
        }

        // An embedded {expression} makes the query dynamic; the ahead-of-time generator needs a constant
        // query (JSONPath itself never uses '{', so this matches the runtime's HasEmbeddedExpression).
        if (query.Contains('{', StringComparison.Ordinal))
        {
            return false;
        }

        // The query data must be a statically navigable JSON value (body / inputs / prior-step outputs).
        ArazzoExpression context = ArazzoExpression.Parse(contextExpression);
        var statementBuilder = new StringBuilder();
        if (!CriterionExpressionParsing.TryEmitElementNavigation(
            context, null, $"{tmpPrefix}data", sources, inputsVariable, stepOutputLocals, inputAccessors, statementBuilder, out string dataLocal))
        {
            return false;
        }

        string className = $"{tmpPrefix}JsonPath";
        string generated;
        try
        {
            generated = JsonPathCodeGenerator.Generate(query, className, namespaceName, accessibility: "internal", isPartial: false);
        }
        catch (JsonPathException)
        {
            return false;
        }

        // Graft the generated class (same namespace) into the executor file. Its file header (usings +
        // namespace) is dropped — the executor file supplies those usings — keeping only the class.
        string marker = $"internal static class {className}";
        int classStart = generated.IndexOf(marker, StringComparison.Ordinal);
        if (classStart < 0)
        {
            return false;
        }

        if (auxiliaryTypes.Length > 0)
        {
            auxiliaryTypes.AppendLine();
        }

        auxiliaryTypes.Append(generated, classStart, generated.Length - classStart).AppendLine();

        // Evaluate against the RUN workspace rather than calling QueryNodes, which spins up its own
        // per-call JsonWorkspace; any intermediate values the query allocates land in the run workspace
        // and are released when the caller resets/disposes it. JsonElement is a managed type so the
        // node buffer cannot be stack-allocated — CreatePooled rents it from the array pool and Dispose
        // returns it (and any spilled growth).
        string matchLocal = $"{tmpPrefix}Match";
        string resultLocal = $"{tmpPrefix}Result";
        statementBuilder.Append("bool ").Append(matchLocal).AppendLine(";");
        statementBuilder.Append("JsonPathResult ").Append(resultLocal).AppendLine(" = JsonPathResult.CreatePooled(16);");
        statementBuilder.AppendLine("try");
        statementBuilder.AppendLine("{");
        statementBuilder.Append("    ").Append(className).Append(".EvaluateNodes(").Append(dataLocal)
            .Append(", workspace, ref ").Append(resultLocal).AppendLine(");");
        statementBuilder.Append("    ").Append(matchLocal).Append(" = ").Append(resultLocal).AppendLine(".Count > 0;");
        statementBuilder.AppendLine("}");
        statementBuilder.AppendLine("finally");
        statementBuilder.AppendLine("{");
        statementBuilder.Append("    ").Append(resultLocal).AppendLine(".Dispose();");
        statementBuilder.AppendLine("}");

        statements = statementBuilder.ToString();
        expression = matchLocal;
        return true;
    }
}
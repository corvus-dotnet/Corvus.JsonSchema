// <copyright file="RequestUrlEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Resolves a step's request URL for the <c>$url</c> runtime expression (plan §3.1). The Arazzo executor
/// invokes the generated client's convenience method, so it never holds the request struct; when a step's
/// criteria reference <c>$url</c> this emitter rebuilds the request struct from the already-resolved
/// parameter <c>Source</c>s and reuses its own <c>WriteResolvedPath</c>/<c>WriteQueryString</c> to produce
/// the <em>relative</em> URL (path + query, escaped exactly as the transport would send it) — contained
/// entirely within the generated code, with no runtime change to the OpenAPI client.
/// </summary>
internal static class RequestUrlEmitter
{
    /// <summary>
    /// Determines whether any of the supplied criteria reference the <c>$url</c> runtime expression.
    /// </summary>
    /// <param name="criteria">The criteria to inspect (condition and optional context).</param>
    /// <returns><see langword="true"/> if <c>$url</c> appears in any criterion.</returns>
    public static bool ReferencesUrl(IEnumerable<StepCriterion> criteria)
    {
        foreach (StepCriterion criterion in criteria)
        {
            if (criterion.Condition.Contains("$url", StringComparison.Ordinal)
                || (criterion.Context is { } context && context.Contains("$url", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Emits the statements that resolve the step's relative request URL into a string local and feed it
    /// to the context (so a <c>$url</c> criterion resolves against it). Built from the already-emitted
    /// parameter <c>Source</c>s, so it must be emitted after the request binding's resolution statements
    /// and before the client call's cleanup (while any pooled buffers are still live).
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="parameterBindings">The request binding's per-parameter bindings.</param>
    /// <param name="prefix">A unique prefix for the emitted locals (e.g. the step's <c>{id}_</c>).</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <returns>The statements to emit.</returns>
    public static string Emit(
        in ResolvedOperation operation,
        IReadOnlyList<RequestParameterBinding>? parameterBindings,
        string prefix,
        string workspaceVariable,
        string contextVariable)
    {
        IReadOnlyList<RequestParameterBinding> bindings = parameterBindings ?? [];
        var pathAndQuery = new List<RequestParameterBinding>(bindings.Count);
        bool hasPath = false;
        bool hasQuery = false;
        foreach (RequestParameterBinding binding in bindings)
        {
            if (binding.Location == ParameterLocation.Path)
            {
                hasPath = true;
                pathAndQuery.Add(binding);
            }
            else if (binding.Location == ParameterLocation.Query)
            {
                hasQuery = true;
                pathAndQuery.Add(binding);
            }
        }

        string requestType = operation.Operation.RequestTypeName;
        string requestLocal = $"{prefix}UrlRequest";
        string bufferLocal = $"{prefix}UrlBuffer";
        string urlLocal = $"{prefix}Url";

        var sb = new StringBuilder();
        sb.Append("var ").Append(bufferLocal).AppendLine(" = new System.Buffers.ArrayBufferWriter<byte>();");

        if (pathAndQuery.Count > 0)
        {
            // Rebuild the request struct from the resolved Sources (the default constructor plus init
            // setters), materialising each into its model type exactly as the generated client does.
            sb.Append("var ").Append(requestLocal).Append(" = new ").Append(requestType).Append(" {");
            for (int i = 0; i < pathAndQuery.Count; i++)
            {
                RequestParameterBinding binding = pathAndQuery[i];
                sb.Append(' ').Append(binding.PropertyName).Append(" = (").Append(binding.TypeName).Append(')')
                    .Append(binding.TypeName).Append(".CreateBuilder(").Append(workspaceVariable).Append(", ")
                    .Append(binding.Source).Append(", 30).RootElement");
                sb.Append(i == pathAndQuery.Count - 1 ? " " : ",");
            }

            sb.AppendLine("};");
        }

        if (hasPath)
        {
            sb.Append(requestLocal).Append(".WriteResolvedPath(").Append(bufferLocal).AppendLine(");");
        }
        else
        {
            // A static path (no path parameters): WriteResolvedPath would throw, so write the template.
            sb.Append(bufferLocal).Append(".Write(").Append(requestType).AppendLine(".PathTemplateUtf8);");
        }

        if (hasQuery)
        {
            string queryLocal = $"{prefix}UrlQuery";
            sb.Append("var ").Append(queryLocal).AppendLine(" = new System.Buffers.ArrayBufferWriter<byte>();");
            sb.Append(requestLocal).Append(".WriteQueryString(").Append(queryLocal).AppendLine(");");
            sb.Append("if (").Append(queryLocal).AppendLine(".WrittenCount > 0)");
            sb.AppendLine("{");
            sb.Append("    ").Append(bufferLocal).AppendLine(".Write(\"?\"u8);");
            sb.Append("    ").Append(bufferLocal).Append(".Write(").Append(queryLocal).AppendLine(".WrittenSpan);");
            sb.AppendLine("}");
        }

        sb.Append("string ").Append(urlLocal).Append(" = System.Text.Encoding.UTF8.GetString(")
            .Append(bufferLocal).AppendLine(".WrittenSpan);");
        sb.Append(contextVariable).Append(".SetRequest(").Append(urlLocal).Append(", ")
            .Append(EmitText.Quote(operation.Operation.Method.ToString().ToUpperInvariant())).AppendLine(");");

        return sb.ToString();
    }
}
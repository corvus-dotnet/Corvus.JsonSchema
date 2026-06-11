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
    /// The executor-owned local that holds the resolved relative request URL (UTF-8 bytes) for a
    /// <c>$url</c> criterion inlined via <see cref="Emit"/>.
    /// </summary>
    /// <param name="prefix">The step's unique local prefix (e.g. <c>{id}_</c>).</param>
    /// <returns>The local name.</returns>
    public static string UrlLocal(string prefix) => $"{prefix}Url";

    /// <summary>
    /// Emits the statements that resolve the step's relative request URL into an executor-owned
    /// <c>byte[]</c> local (<see cref="UrlLocal"/>), so an inlined <c>$url</c> criterion compares against
    /// it directly with no <see cref="WorkflowExecutionContext"/>. The URL is written through a per-step
    /// <c>[ThreadStatic]</c> <see cref="System.Buffers.ArrayBufferWriter{T}"/> field on the executor class
    /// (reused across runs, so only the final URL bytes allocate). Built from the already-emitted parameter
    /// <c>Source</c>s, so it must be emitted after the request binding's resolution statements and before
    /// the client call's cleanup (while any pooled buffers are still live).
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="parameterBindings">The request binding's per-parameter bindings.</param>
    /// <param name="prefix">A unique prefix for the emitted locals (e.g. the step's <c>{id}_</c>).</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="fields">Accumulates the <c>[ThreadStatic]</c> buffer field declarations.</param>
    /// <returns>The statements to emit.</returns>
    public static string Emit(
        in ResolvedOperation operation,
        IReadOnlyList<RequestParameterBinding>? parameterBindings,
        string prefix,
        string workspaceVariable,
        StringBuilder fields)
    {
        string requestType = operation.Operation.RequestTypeName;
        string requestLocal = $"{prefix}UrlRequest";
        var sb = new StringBuilder();
        AppendRequestStruct(sb, requestType, requestLocal, parameterBindings, workspaceVariable, out bool hasPath, out bool hasQuery);

        // Reuse a per-step thread-static writer (allocated once per thread, like the OpenAPI transport's
        // URI writer): the URL is built synchronously from the resolved Sources before the client call, so
        // the writer never overlaps and the path allocates only the final URL bytes.
        string bufferField = $"{prefix}UrlBuffer";
        string writerLocal = $"{prefix}UrlWriter";
        fields.AppendLine("[ThreadStatic]");
        fields.Append("private static ArrayBufferWriter<byte>? ").Append(bufferField).AppendLine(";");
        sb.Append("ArrayBufferWriter<byte> ").Append(writerLocal).Append(" = ").Append(bufferField).AppendLine(" ??= new ArrayBufferWriter<byte>(128);");
        sb.Append(writerLocal).AppendLine(".ResetWrittenCount();");
        if (hasPath)
        {
            sb.Append(requestLocal).Append(".WriteResolvedPath(").Append(writerLocal).AppendLine(");");
        }
        else
        {
            // A static path (no path parameters): WriteResolvedPath would throw, so write the template.
            sb.Append(writerLocal).Append(".Write(").Append(requestType).AppendLine(".PathTemplateUtf8);");
        }

        if (hasQuery)
        {
            string queryField = $"{prefix}UrlQueryBuffer";
            string queryWriter = $"{prefix}UrlQueryWriter";
            fields.AppendLine("[ThreadStatic]");
            fields.Append("private static ArrayBufferWriter<byte>? ").Append(queryField).AppendLine(";");
            sb.Append("ArrayBufferWriter<byte> ").Append(queryWriter).Append(" = ").Append(queryField).AppendLine(" ??= new ArrayBufferWriter<byte>(64);");
            sb.Append(queryWriter).AppendLine(".ResetWrittenCount();");
            sb.Append(requestLocal).Append(".WriteQueryString(").Append(queryWriter).AppendLine(");");
            sb.Append("if (").Append(queryWriter).AppendLine(".WrittenCount > 0)");
            sb.AppendLine("{");
            sb.Append("    ").Append(writerLocal).AppendLine(".Write(\"?\"u8);");
            sb.Append("    ").Append(writerLocal).Append(".Write(").Append(queryWriter).AppendLine(".WrittenSpan);");
            sb.AppendLine("}");
        }

        sb.Append("byte[] ").Append(UrlLocal(prefix)).Append(" = ").Append(writerLocal).AppendLine(".WrittenSpan.ToArray();");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the statements that resolve the step's relative request URL and feed it to the context (so a
    /// non-inlined <c>$url</c> criterion — one that falls back to a <see cref="CompiledCriterion"/> —
    /// resolves against <c>context.url</c>). Built from the already-emitted parameter <c>Source</c>s.
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="parameterBindings">The request binding's per-parameter bindings.</param>
    /// <param name="prefix">A unique prefix for the emitted locals (e.g. the step's <c>{id}_</c>).</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <returns>The statements to emit.</returns>
    public static string EmitToContext(
        in ResolvedOperation operation,
        IReadOnlyList<RequestParameterBinding>? parameterBindings,
        string prefix,
        string workspaceVariable,
        string contextVariable)
    {
        string requestType = operation.Operation.RequestTypeName;
        string requestLocal = $"{prefix}UrlRequest";
        string bufferLocal = $"{prefix}UrlBuffer";
        string methodLiteral = $"{EmitText.Quote(operation.Operation.Method.ToString().ToUpperInvariant())}u8";

        var sb = new StringBuilder();
        AppendRequestStruct(sb, requestType, requestLocal, parameterBindings, workspaceVariable, out bool hasPath, out bool hasQuery);

        // Write the relative URL into a buffer the context owns and reuses across steps (never a per-step
        // heap-allocated writer), then store it via the context — so the $url path allocates only the
        // stored URL bytes, no intermediate writer or managed string.
        sb.Append("var ").Append(bufferLocal).Append(" = ").Append(contextVariable).AppendLine(".BeginRequestUrl();");
        if (hasPath)
        {
            sb.Append(requestLocal).Append(".WriteResolvedPath(").Append(bufferLocal).AppendLine(");");
        }
        else
        {
            sb.Append(bufferLocal).Append(".Write(").Append(requestType).AppendLine(".PathTemplateUtf8);");
        }

        if (hasQuery)
        {
            string queryLocal = $"{prefix}UrlQuery";
            sb.Append("var ").Append(queryLocal).Append(" = ").Append(contextVariable).AppendLine(".BeginRequestUrlQuery();");
            sb.Append(requestLocal).Append(".WriteQueryString(").Append(queryLocal).AppendLine(");");
        }

        sb.Append(contextVariable).Append(".EndRequestUrl(").Append(methodLiteral).AppendLine(");");

        return sb.ToString();
    }

    /// <summary>
    /// Rebuilds the request struct from the resolved parameter <c>Source</c>s (default constructor plus
    /// init setters), materialising each into its model type exactly as the generated client does. A
    /// static-path, no-query operation needs no parameters, so a default instance suffices.
    /// </summary>
    private static void AppendRequestStruct(
        StringBuilder sb,
        string requestType,
        string requestLocal,
        IReadOnlyList<RequestParameterBinding>? parameterBindings,
        string workspaceVariable,
        out bool hasPath,
        out bool hasQuery)
    {
        IReadOnlyList<RequestParameterBinding> bindings = parameterBindings ?? [];
        var pathAndQuery = new List<RequestParameterBinding>(bindings.Count);
        hasPath = false;
        hasQuery = false;
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

        if (pathAndQuery.Count > 0)
        {
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
        else
        {
            sb.Append("var ").Append(requestLocal).Append(" = default(").Append(requestType).AppendLine(");");
        }
    }
}
// <copyright file="CriterionExpressionParsing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Shared generation-time parsing of criterion operand/context tokens, mirroring the runtime
/// <c>SimpleConditionEvaluator</c> so inlined criteria navigate values exactly as the interpreter does.
/// </summary>
/// <summary>
/// The in-scope locals holding a step's live JSON sources for criterion operand navigation: the step's
/// body (an HTTP response body <c>$response.body</c> or a received message payload <c>$message.payload</c>
/// — a step has at most one) and, for a channel receive step, the message headers object
/// (<c>$message.header.&lt;name&gt;</c>). Bundled so a new source does not change every inliner signature.
/// </summary>
/// <param name="BodyLocal">The live body local, or <see langword="null"/> if the step bound none.</param>
/// <param name="HeadersLocal">The message headers local, or <see langword="null"/> if none is available.</param>
internal readonly record struct CriterionSources(string? BodyLocal, string? HeadersLocal = null);

internal static class CriterionExpressionParsing
{
    /// <summary>
    /// Resolves a <c>$response.header.&lt;name&gt;</c> operand/context to the generated response property
    /// that exposes the header (HTTP header names are case-insensitive).
    /// </summary>
    /// <param name="responseHeaders">The operation's declared response headers, or <see langword="null"/>.</param>
    /// <param name="headerName">The header name from the expression.</param>
    /// <param name="header">The matched header descriptor.</param>
    /// <returns><see langword="true"/> if a declared header matches.</returns>
    public static bool TryResolveResponseHeader(IReadOnlyList<ResponseHeaderInfo>? responseHeaders, string headerName, out ResponseHeaderInfo header)
    {
        if (responseHeaders is not null)
        {
            foreach (ResponseHeaderInfo candidate in responseHeaders)
            {
                if (string.Equals(candidate.HeaderName, headerName, StringComparison.OrdinalIgnoreCase))
                {
                    header = candidate;
                    return true;
                }
            }
        }

        header = default;
        return false;
    }

    /// <summary>
    /// Emits navigation of a JSON-valued expression (<c>$response.body</c>, <c>$inputs.&lt;name&gt;</c>,
    /// or <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c>) to a <see cref="JsonElement"/>, used by the
    /// criterion inliners to resolve an operand/context value statically.
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="navigationPointer">An additional <c>.</c>/<c>[]</c> navigation pointer (simple-condition operands), or <see langword="null"/>.</param>
    /// <param name="baseName">A unique base name for any emitted temporaries.</param>
    /// <param name="sources">The step's live JSON sources (response body / message payload / message headers).</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor on the inputs model, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="statements">Accumulates the navigation statements (none when the whole root is used).</param>
    /// <param name="elementLocal">The in-scope expression yielding the resolved <see cref="JsonElement"/> (a local, or the root directly).</param>
    /// <returns><see langword="true"/> if the source is statically navigable.</returns>
    public static bool TryEmitElementNavigation(
        in ArazzoExpression expression,
        string? navigationPointer,
        string baseName,
        in CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder statements,
        out string elementLocal)
    {
        elementLocal = string.Empty;

        string root;
        string? name = null;
        switch (expression.Source)
        {
            case ArazzoExpressionSource.ResponseBody when sources.BodyLocal is not null:
                root = sources.BodyLocal;
                break;

            case ArazzoExpressionSource.MessagePayload when sources.BodyLocal is not null:
                // A channel receive step's live body local holds the received message payload.
                root = sources.BodyLocal;
                break;

            case ArazzoExpressionSource.MessageHeader when sources.HeadersLocal is not null && expression.Name is { } headerName:
                // A named value off the received message headers object (then any pointer/navigation).
                root = sources.HeadersLocal;
                name = headerName;
                break;

            case ArazzoExpressionSource.Inputs when expression.Name is { } inputName
                && inputAccessors is not null && inputAccessors.TryGetValue(inputName, out string? accessor):
                // Strongly-typed inputs model: the accessor IS the navigation (no property lookup).
                root = $"((JsonElement){inputsVariable}.{accessor})";
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
            // The whole root value — a reference, no statement.
            elementLocal = root;
            return true;
        }

        statements.Append("JsonElement ").Append(baseName).AppendLine(" = default;");
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
        statements.AppendLine("{");
        statements.Append("    ").Append(baseName).Append(" = ")
            .Append($"{baseName}n{(steps.Count - 1).ToString(CultureInfo.InvariantCulture)}").AppendLine(";");
        statements.AppendLine("}");

        elementLocal = baseName;
        return true;
    }

    /// <summary>
    /// Splits an operand/context token into a runtime expression and an optional JSON Pointer for
    /// trailing <c>.property</c>/<c>[index]</c> navigation — the exact algorithm the runtime
    /// <c>SimpleConditionEvaluator</c> uses, so the inlined navigation matches.
    /// </summary>
    /// <param name="token">The operand/context token.</param>
    /// <returns>The parsed expression and any trailing navigation pointer.</returns>
    public static (ArazzoExpression Expression, string? NavigationPointer) SplitNavigation(string token)
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
}
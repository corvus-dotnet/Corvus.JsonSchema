// <copyright file="ArazzoExpression.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A parsed Arazzo runtime expression (Arazzo Specification §Runtime Expressions).
/// </summary>
/// <remarks>
/// <para>
/// Runtime expressions extract values from the workflow execution context at run time, for example
/// <c>$inputs.username</c>, <c>$steps.loginStep.outputs.token</c>, or
/// <c>$response.body#/accessToken</c>. Anything that does not begin with <c>$</c> is a literal.
/// </para>
/// <para>
/// This type performs only syntactic parsing; resolving an expression against live JSON values is
/// the responsibility of the workflow execution context.
/// </para>
/// </remarks>
public readonly struct ArazzoExpression
{
    private ArazzoExpression(
        ArazzoExpressionSource source,
        string? containerId,
        string? qualifier,
        string? name,
        string? jsonPointer,
        string? literalValue)
    {
        this.Source = source;
        this.ContainerId = containerId;
        this.Qualifier = qualifier;
        this.Name = name;
        this.JsonPointer = jsonPointer;
        this.LiteralValue = literalValue;
    }

    /// <summary>
    /// Gets the source of the expression.
    /// </summary>
    public ArazzoExpressionSource Source { get; }

    /// <summary>
    /// Gets the container identifier — the <c>stepId</c> for <see cref="ArazzoExpressionSource.Steps"/>,
    /// the <c>workflowId</c> for <see cref="ArazzoExpressionSource.Workflows"/>, or the source-description
    /// name for <see cref="ArazzoExpressionSource.SourceDescriptions"/>. <see langword="null"/> otherwise.
    /// </summary>
    public string? ContainerId { get; }

    /// <summary>
    /// Gets the qualifier — <c>inputs</c> or <c>outputs</c> for
    /// <see cref="ArazzoExpressionSource.Workflows"/>, or the component type
    /// (for example <c>parameters</c>) for <see cref="ArazzoExpressionSource.Components"/>.
    /// <see langword="null"/> otherwise.
    /// </summary>
    public string? Qualifier { get; }

    /// <summary>
    /// Gets the leaf name — the header/query/path/input/output/component name, or the source-description
    /// field. <see langword="null"/> when the source carries no name (for example <c>$url</c>).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the JSON Pointer fragment that followed a <c>#</c> in the expression (for example
    /// <c>/accessToken</c>), or <see langword="null"/> if the expression had no <c>#</c> fragment.
    /// An empty string denotes the whole value (a bare <c>#</c>).
    /// </summary>
    public string? JsonPointer { get; }

    /// <summary>
    /// Gets the literal value when <see cref="Source"/> is <see cref="ArazzoExpressionSource.Literal"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public string? LiteralValue { get; }

    /// <summary>
    /// Gets a value indicating whether the expression carried a <c>#</c> JSON Pointer fragment.
    /// </summary>
    public bool HasJsonPointer => this.JsonPointer is not null;

    /// <summary>
    /// Parses an Arazzo runtime expression. Any value not beginning with <c>$</c>, and any
    /// unrecognized <c>$</c> form, is treated as a <see cref="ArazzoExpressionSource.Literal"/>.
    /// </summary>
    /// <param name="expression">The raw expression string.</param>
    /// <returns>The parsed <see cref="ArazzoExpression"/>.</returns>
    public static ArazzoExpression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.Length == 0 || expression[0] != '$')
        {
            return Literal(expression);
        }

        // Separate any trailing JSON Pointer fragment.
        string head = expression;
        string? pointer = null;
        int hash = expression.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            head = expression[..hash];
            pointer = expression[(hash + 1)..];
        }

        return head switch
        {
            "$url" => new ArazzoExpression(ArazzoExpressionSource.Url, null, null, null, pointer, null),
            "$method" => new ArazzoExpression(ArazzoExpressionSource.Method, null, null, null, pointer, null),
            "$statusCode" => new ArazzoExpression(ArazzoExpressionSource.StatusCode, null, null, null, pointer, null),
            "$self" => new ArazzoExpression(ArazzoExpressionSource.Self, null, null, null, pointer, null),
            _ => ParseQualified(expression, head, pointer),
        };
    }

    private static ArazzoExpression ParseQualified(string expression, string head, string? pointer)
    {
        if (TryStrip(head, "$request.", out string requestRest))
        {
            return ParseRequest(expression, requestRest, pointer);
        }

        if (TryStrip(head, "$response.", out string responseRest))
        {
            return ParseResponse(expression, responseRest, pointer);
        }

        if (TryStrip(head, "$message.", out string messageRest))
        {
            return ParseMessage(expression, messageRest, pointer);
        }

        if (TryStrip(head, "$inputs.", out string inputName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.Inputs, null, null, inputName, pointer, null);
        }

        if (TryStrip(head, "$outputs.", out string outputName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.Outputs, null, null, outputName, pointer, null);
        }

        if (TryStrip(head, "$steps.", out string stepsRest))
        {
            return ParseSteps(expression, stepsRest, pointer);
        }

        if (TryStrip(head, "$workflows.", out string workflowsRest))
        {
            return ParseWorkflows(expression, workflowsRest, pointer);
        }

        if (TryStrip(head, "$sourceDescriptions.", out string sourceRest))
        {
            (string container, string? field) = SplitFirst(sourceRest);
            return new ArazzoExpression(ArazzoExpressionSource.SourceDescriptions, container, null, field, pointer, null);
        }

        if (TryStrip(head, "$components.", out string componentsRest))
        {
            (string type, string? name) = SplitFirst(componentsRest);
            return new ArazzoExpression(ArazzoExpressionSource.Components, null, type, name, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression ParseRequest(string expression, string rest, string? pointer)
    {
        if (rest == "body")
        {
            return new ArazzoExpression(ArazzoExpressionSource.RequestBody, null, null, null, pointer, null);
        }

        if (TryStrip(rest, "header.", out string headerName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.RequestHeader, null, null, headerName, pointer, null);
        }

        if (TryStrip(rest, "query.", out string queryName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.RequestQuery, null, null, queryName, pointer, null);
        }

        if (TryStrip(rest, "path.", out string pathName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.RequestPath, null, null, pathName, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression ParseResponse(string expression, string rest, string? pointer)
    {
        if (rest == "body")
        {
            return new ArazzoExpression(ArazzoExpressionSource.ResponseBody, null, null, null, pointer, null);
        }

        if (TryStrip(rest, "header.", out string headerName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.ResponseHeader, null, null, headerName, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression ParseMessage(string expression, string rest, string? pointer)
    {
        if (rest == "payload")
        {
            return new ArazzoExpression(ArazzoExpressionSource.MessagePayload, null, null, null, pointer, null);
        }

        if (TryStrip(rest, "header.", out string headerName))
        {
            return new ArazzoExpression(ArazzoExpressionSource.MessageHeader, null, null, headerName, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression ParseSteps(string expression, string rest, string? pointer)
    {
        int idx = rest.IndexOf(".outputs.", StringComparison.Ordinal);
        if (idx > 0)
        {
            string stepId = rest[..idx];
            string name = rest[(idx + ".outputs.".Length)..];
            return new ArazzoExpression(ArazzoExpressionSource.Steps, stepId, "outputs", name, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression ParseWorkflows(string expression, string rest, string? pointer)
    {
        int outputsIdx = rest.IndexOf(".outputs.", StringComparison.Ordinal);
        if (outputsIdx > 0)
        {
            string workflowId = rest[..outputsIdx];
            string name = rest[(outputsIdx + ".outputs.".Length)..];
            return new ArazzoExpression(ArazzoExpressionSource.Workflows, workflowId, "outputs", name, pointer, null);
        }

        int inputsIdx = rest.IndexOf(".inputs.", StringComparison.Ordinal);
        if (inputsIdx > 0)
        {
            string workflowId = rest[..inputsIdx];
            string name = rest[(inputsIdx + ".inputs.".Length)..];
            return new ArazzoExpression(ArazzoExpressionSource.Workflows, workflowId, "inputs", name, pointer, null);
        }

        return Literal(expression);
    }

    private static ArazzoExpression Literal(string value)
        => new(ArazzoExpressionSource.Literal, null, null, null, null, value);

    private static bool TryStrip(string value, string prefix, out string rest)
    {
        if (value.Length > prefix.Length && value.StartsWith(prefix, StringComparison.Ordinal))
        {
            rest = value[prefix.Length..];
            return true;
        }

        rest = string.Empty;
        return false;
    }

    private static (string Head, string? Tail) SplitFirst(string value)
    {
        int dot = value.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? (value, null) : (value[..dot], value[(dot + 1)..]);
    }
}
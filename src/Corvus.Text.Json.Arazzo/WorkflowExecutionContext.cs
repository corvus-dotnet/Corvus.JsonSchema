// <copyright file="WorkflowExecutionContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Holds the JSON values available while a workflow executes, and resolves
/// <see cref="ArazzoExpression"/>s that reference them.
/// </summary>
/// <remarks>
/// <para>
/// Values are stored as <see cref="JsonElement"/> structs over pooled
/// <see cref="ParsedJsonDocument{T}"/> memory; the context does not copy or own that memory, so the
/// caller must keep the backing documents alive for as long as the context is in use. Resolution is
/// zero-allocation: property lookups and JSON Pointer resolution operate directly on the pooled
/// memory and return a <see cref="JsonElement"/> view.
/// </para>
/// <para>
/// This type covers the JSON-valued runtime-expression sources (<c>$inputs</c>, <c>$outputs</c>,
/// <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c>, <c>$request.body</c>, <c>$response.body</c>,
/// <c>$message.payload</c>), each with an optional <c>#</c> JSON Pointer fragment. Scalar sources
/// (<c>$url</c>, <c>$method</c>, <c>$statusCode</c>, header/query/path values) and string
/// interpolation are layered on in later increments.
/// </para>
/// </remarks>
public sealed partial class WorkflowExecutionContext
{
    private readonly Dictionary<string, JsonElement> stepOutputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> workflowInputsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> workflowOutputsById = new(StringComparer.Ordinal);

    private JsonElement inputs;
    private JsonElement workflowOutputs;
    private JsonElement requestBody;
    private JsonElement responseBody;
    private JsonElement messagePayload;

    /// <summary>
    /// Sets the workflow inputs object (resolved by <c>$inputs.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="value">The inputs object.</param>
    public void SetInputs(in JsonElement value) => this.inputs = value;

    /// <summary>
    /// Sets the workflow outputs object (resolved by <c>$outputs.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="value">The outputs object.</param>
    public void SetWorkflowOutputs(in JsonElement value) => this.workflowOutputs = value;

    /// <summary>
    /// Sets the named outputs object for a step (resolved by <c>$steps.&lt;stepId&gt;.outputs.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="stepId">The step identifier.</param>
    /// <param name="outputs">The step's outputs object.</param>
    public void SetStepOutputs(string stepId, in JsonElement outputs)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        this.stepOutputs[stepId] = outputs;
    }

    /// <summary>
    /// Sets a referenced workflow's inputs object (resolved by <c>$workflows.&lt;workflowId&gt;.inputs.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="inputs">The workflow's inputs object.</param>
    public void SetWorkflowInputs(string workflowId, in JsonElement inputs)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        this.workflowInputsById[workflowId] = inputs;
    }

    /// <summary>
    /// Sets a referenced workflow's outputs object (resolved by <c>$workflows.&lt;workflowId&gt;.outputs.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="outputs">The workflow's outputs object.</param>
    public void SetWorkflowOutputs(string workflowId, in JsonElement outputs)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        this.workflowOutputsById[workflowId] = outputs;
    }

    /// <summary>
    /// Sets the current request body (resolved by <c>$request.body</c>).
    /// </summary>
    /// <param name="value">The request body.</param>
    public void SetRequestBody(in JsonElement value) => this.requestBody = value;

    /// <summary>
    /// Sets the current response body (resolved by <c>$response.body</c>).
    /// </summary>
    /// <param name="value">The response body.</param>
    public void SetResponseBody(in JsonElement value) => this.responseBody = value;

    /// <summary>
    /// Sets the current message payload (resolved by <c>$message.payload</c>).
    /// </summary>
    /// <param name="value">The message payload.</param>
    public void SetMessagePayload(in JsonElement value) => this.messagePayload = value;

    /// <summary>
    /// Resolves a JSON-valued runtime expression against the current context.
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, the resolved <see cref="JsonElement"/>;
    /// otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the expression names a JSON-valued source that is present and
    /// (where applicable) the property and JSON Pointer resolve; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryResolveValue(in ArazzoExpression expression, out JsonElement value)
    {
        value = default;

        switch (expression.Source)
        {
            case ArazzoExpressionSource.Inputs:
                return TryResolveMember(this.inputs, expression.Name, expression.JsonPointer, out value);

            case ArazzoExpressionSource.Outputs:
                return TryResolveMember(this.workflowOutputs, expression.Name, expression.JsonPointer, out value);

            case ArazzoExpressionSource.Steps:
                return expression.ContainerId is string stepId
                    && this.stepOutputs.TryGetValue(stepId, out JsonElement outputs)
                    && TryResolveMember(outputs, expression.Name, expression.JsonPointer, out value);

            case ArazzoExpressionSource.Workflows:
                return expression.ContainerId is string workflowId
                    && (expression.Qualifier == "inputs" ? this.workflowInputsById : this.workflowOutputsById)
                        .TryGetValue(workflowId, out JsonElement workflowValues)
                    && TryResolveMember(workflowValues, expression.Name, expression.JsonPointer, out value);

            case ArazzoExpressionSource.RequestBody:
                return TryResolveMember(this.requestBody, null, expression.JsonPointer, out value);

            case ArazzoExpressionSource.ResponseBody:
                return TryResolveMember(this.responseBody, null, expression.JsonPointer, out value);

            case ArazzoExpressionSource.MessagePayload:
                return TryResolveMember(this.messagePayload, null, expression.JsonPointer, out value);

            default:
                return false;
        }
    }

    private static bool TryResolveMember(in JsonElement root, string? name, string? jsonPointer, out JsonElement value)
    {
        value = default;

        if (root.IsUndefined())
        {
            return false;
        }

        JsonElement current = root;

        if (name is not null)
        {
            if (!current.TryGetProperty(name, out current))
            {
                return false;
            }
        }

        if (jsonPointer is null || jsonPointer.Length == 0)
        {
            // No fragment, or a bare '#': the whole (already-located) value.
            value = current;
            return true;
        }

        return current.TryResolvePointer(jsonPointer, out value);
    }
}
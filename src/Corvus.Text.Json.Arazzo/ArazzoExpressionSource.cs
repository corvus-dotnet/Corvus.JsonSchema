// <copyright file="ArazzoExpressionSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Identifies the source of a parsed Arazzo runtime expression
/// (Arazzo Specification §Runtime Expressions).
/// </summary>
public enum ArazzoExpressionSource
{
    /// <summary>
    /// A literal value — any expression that does not begin with <c>$</c>.
    /// </summary>
    Literal,

    /// <summary>
    /// <c>$url</c> — the full request URL.
    /// </summary>
    Url,

    /// <summary>
    /// <c>$method</c> — the request HTTP method.
    /// </summary>
    Method,

    /// <summary>
    /// <c>$statusCode</c> — the response HTTP status code.
    /// </summary>
    StatusCode,

    /// <summary>
    /// <c>$request.header.&lt;name&gt;</c> — a request header value.
    /// </summary>
    RequestHeader,

    /// <summary>
    /// <c>$request.query.&lt;name&gt;</c> — a request query parameter.
    /// </summary>
    RequestQuery,

    /// <summary>
    /// <c>$request.path.&lt;name&gt;</c> — a request path parameter.
    /// </summary>
    RequestPath,

    /// <summary>
    /// <c>$request.body</c> (optionally with a <c>#</c> JSON Pointer) — the request body.
    /// </summary>
    RequestBody,

    /// <summary>
    /// <c>$response.header.&lt;name&gt;</c> — a response header value.
    /// </summary>
    ResponseHeader,

    /// <summary>
    /// <c>$response.body</c> (optionally with a <c>#</c> JSON Pointer) — the response body.
    /// </summary>
    ResponseBody,

    /// <summary>
    /// <c>$message.header.&lt;name&gt;</c> — an AsyncAPI message header value.
    /// </summary>
    MessageHeader,

    /// <summary>
    /// <c>$message.payload</c> (optionally with a <c>#</c> JSON Pointer) — an AsyncAPI message payload.
    /// </summary>
    MessagePayload,

    /// <summary>
    /// <c>$inputs.&lt;name&gt;</c> — a workflow input.
    /// </summary>
    Inputs,

    /// <summary>
    /// <c>$outputs.&lt;name&gt;</c> — a workflow output.
    /// </summary>
    Outputs,

    /// <summary>
    /// <c>$steps.&lt;stepId&gt;.outputs.&lt;name&gt;</c> — a named output of a step.
    /// </summary>
    Steps,

    /// <summary>
    /// <c>$workflows.&lt;workflowId&gt;.inputs.&lt;name&gt;</c> or
    /// <c>$workflows.&lt;workflowId&gt;.outputs.&lt;name&gt;</c> — an input or output of a workflow.
    /// </summary>
    Workflows,

    /// <summary>
    /// <c>$sourceDescriptions.&lt;name&gt;.&lt;field&gt;</c> — a field of a source description.
    /// </summary>
    SourceDescriptions,

    /// <summary>
    /// <c>$components.&lt;type&gt;.&lt;name&gt;</c> — a reusable component object.
    /// </summary>
    Components,

    /// <summary>
    /// <c>$self</c> — the current Arazzo document.
    /// </summary>
    Self,
}
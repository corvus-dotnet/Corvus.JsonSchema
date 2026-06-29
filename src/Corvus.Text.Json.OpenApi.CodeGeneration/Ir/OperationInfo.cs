// <copyright file="OperationInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The kind of operation host an <see cref="OperationInfo"/> was discovered under.
/// </summary>
public enum OperationKind
{
    /// <summary>
    /// A standard path operation (under <c>paths</c>).
    /// </summary>
    Path,

    /// <summary>
    /// A webhook operation (under <c>webhooks</c>).
    /// </summary>
    Webhook,

    /// <summary>
    /// A callback operation (under an operation's <c>callbacks</c>).
    /// </summary>
    Callback,
}

/// <summary>
/// The language-neutral, emit-boundary description of a single OpenAPI operation.
/// </summary>
/// <remarks>
/// <para>
/// This record is the unified representation shared by the OpenAPI 3.0, 3.1 and 3.2
/// code generators. It is the OpenAPI 3.2 superset: fields that only the 3.2 walker
/// populates carry defaults so that the 3.0 and 3.1 walkers can continue to construct
/// the common prefix positionally.
/// </para>
/// </remarks>
/// <param name="PathTemplate">The path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method of the operation.</param>
/// <param name="MethodName">The generated client method name.</param>
/// <param name="OperationId">The optional <c>operationId</c>.</param>
/// <param name="Summary">The optional summary.</param>
/// <param name="Description">The optional description.</param>
/// <param name="IsDeprecated">Whether the operation is deprecated.</param>
/// <param name="Tags">The operation tags.</param>
/// <param name="Parameters">The operation parameters.</param>
/// <param name="RequestBody">The optional request body.</param>
/// <param name="Responses">The operation responses.</param>
/// <param name="EffectiveServer">The optional effective server for the operation.</param>
/// <param name="SecurityRequirements">The optional security requirements.</param>
/// <param name="CustomMethodName">
/// The custom HTTP method name (OpenAPI 3.2 <c>additionalOperations</c>). Only populated
/// by the 3.2 walker.
/// </param>
/// <param name="Kind">The kind of host the operation was discovered under.</param>
public readonly record struct OperationInfo(
    string PathTemplate,
    OperationMethod Method,
    string MethodName,
    string? OperationId,
    string? Summary,
    string? Description,
    bool IsDeprecated,
    string[] Tags,
    ParameterInfo[] Parameters,
    RequestBodyInfo? RequestBody,
    ResponseInfo[] Responses,
    ServerInfo? EffectiveServer,
    OperationSecurityRequirementSet[]? SecurityRequirements = null,
    string? CustomMethodName = null,
    OperationKind Kind = OperationKind.Path);
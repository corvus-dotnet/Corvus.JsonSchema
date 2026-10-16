// <copyright file="OperationInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// An operation prepared for emission: everything a client or server emitter needs, resolved from the
/// OpenAPI document, with no language-specific content except <see cref="MethodName"/>.
/// </summary>
/// <param name="PathTemplate">The path template as written in the document (for example <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="CustomMethodName">
/// For an OpenAPI 3.2 additional operation, the custom HTTP method name as written in the document; otherwise
/// <see langword="null"/>.
/// </param>
/// <param name="MethodName">The generated C# method name (PascalCase, without the <c>Async</c> suffix).</param>
/// <param name="OperationId">The <c>operationId</c>, or <see langword="null"/> when the document gives none.</param>
/// <param name="Summary">The operation's <c>summary</c>, or <see langword="null"/>.</param>
/// <param name="Description">The operation's <c>description</c>, or <see langword="null"/>.</param>
/// <param name="IsDeprecated">Whether the operation is marked <c>deprecated</c>.</param>
/// <param name="Tags">The operation's tags, in document order; empty when it has none.</param>
/// <param name="Parameters">The merged path-level and operation-level parameters, in document order.</param>
/// <param name="RequestBody">The request body, or <see langword="null"/> when the operation declares none.</param>
/// <param name="Responses">The responses, in document order, keyed by status code, range or <c>default</c>.</param>
/// <param name="EffectiveServer">
/// The server that applies to this operation after operation-level and path-level overrides, or
/// <see langword="null"/> when the document declares no server.
/// </param>
/// <param name="SecurityRequirements">
/// The operation's security requirement alternatives, or <see langword="null"/> when the operation declares no
/// <c>security</c> of its own and inherits the document's.
/// </param>
public readonly record struct OperationInfo(
    string PathTemplate,
    OperationMethod Method,
    string? CustomMethodName,
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
    OperationSecurityRequirementSet[]? SecurityRequirements = null);
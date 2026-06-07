// <copyright file="ResolvedOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// An operation that a workflow step targets, resolved against a source description (plan §3.1).
/// The generated type names are taken verbatim from the OpenAPI generator's own
/// <see cref="OpenApi.CodeGeneration.OperationSummary"/>, so the Arazzo generator never re-derives
/// the client's naming convention.
/// </summary>
/// <param name="SourceName">The <c>name</c> of the source description that owns the operation.</param>
/// <param name="Path">The API path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="OperationId">The operation's <c>operationId</c>, or <see langword="null"/> if it declares none.</param>
/// <param name="MethodName">The generated method name (PascalCase) the client generator assigned to this operation.</param>
/// <param name="RequestTypeName">The generated request type's simple (unqualified) name.</param>
/// <param name="ResponseTypeName">The generated response type's simple (unqualified) name.</param>
public readonly record struct ResolvedOperation(
    string SourceName,
    string Path,
    OperationMethod Method,
    string? OperationId,
    string MethodName,
    string RequestTypeName,
    string ResponseTypeName);
// <copyright file="OperationSummary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A lightweight summary of an API operation. Used both for display (e.g. the <c>show</c> command)
/// and as the authoritative mapping from an operation to the generated request/response type names —
/// <see cref="MethodName"/>, <see cref="RequestTypeName"/>, and <see cref="ResponseTypeName"/> are
/// computed by the generator itself, so consumers (such as the Arazzo workflow generator) never
/// re-derive the naming heuristic.
/// </summary>
/// <param name="Path">The API path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="OperationId">The <c>operationId</c>, or <see langword="null"/> if not specified.</param>
/// <param name="Tags">The tags assigned to this operation.</param>
/// <param name="IsDeprecated">Whether the operation is marked as deprecated.</param>
/// <param name="ParameterCount">The number of parameters declared on the operation.</param>
/// <param name="HasRequestBody">Whether the operation declares a request body.</param>
/// <param name="Summary">The operation summary text, or <see langword="null"/>.</param>
/// <param name="MethodName">The generated method name (PascalCase) for this operation.</param>
/// <param name="RequestTypeName">The generated request type's simple (unqualified) name.</param>
/// <param name="ResponseTypeName">The generated response type's simple (unqualified) name.</param>
public readonly record struct OperationSummary(
    string Path,
    OperationMethod Method,
    string? OperationId,
    string[] Tags,
    bool IsDeprecated,
    int ParameterCount,
    bool HasRequestBody,
    string? Summary,
    string MethodName,
    string RequestTypeName,
    string ResponseTypeName);
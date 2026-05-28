// <copyright file="OperationSummary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A lightweight summary of an API operation for display purposes (e.g. the <c>show</c> command).
/// </summary>
/// <param name="Path">The API path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="OperationId">The <c>operationId</c>, or <see langword="null"/> if not specified.</param>
/// <param name="Tags">The tags assigned to this operation.</param>
/// <param name="IsDeprecated">Whether the operation is marked as deprecated.</param>
/// <param name="ParameterCount">The number of parameters declared on the operation.</param>
/// <param name="HasRequestBody">Whether the operation declares a request body.</param>
/// <param name="Summary">The operation summary text, or <see langword="null"/>.</param>
public readonly record struct OperationSummary(
    string Path,
    OperationMethod Method,
    string? OperationId,
    string[] Tags,
    bool IsDeprecated,
    int ParameterCount,
    bool HasRequestBody,
    string? Summary);
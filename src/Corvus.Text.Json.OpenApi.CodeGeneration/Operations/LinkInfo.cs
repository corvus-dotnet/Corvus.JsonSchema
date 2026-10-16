// <copyright file="LinkInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A response link: a follow-on operation and how its inputs are bound from this response.
/// </summary>
/// <param name="LinkName">The link's key in the response's <c>links</c> map.</param>
/// <param name="TargetOperationId">The <c>operationId</c> the link targets, or <see langword="null"/> when it could not be resolved.</param>
/// <param name="ParameterBindings">The target operation's parameters bound by runtime expressions, in document order.</param>
/// <param name="RequestBodyExpression">The runtime expression that supplies the target's request body, or <see langword="null"/>.</param>
/// <param name="Description">The link's <c>description</c>, or <see langword="null"/>.</param>
/// <param name="SourceStatusCode">The status code of the response that carries the link.</param>
public readonly record struct LinkInfo(
    string LinkName,
    string? TargetOperationId,
    LinkParameterBinding[] ParameterBindings,
    string? RequestBodyExpression,
    string? Description,
    string SourceStatusCode);
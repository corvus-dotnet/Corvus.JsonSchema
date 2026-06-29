// <copyright file="LinkInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI response link.
/// </summary>
/// <param name="LinkName">The link name.</param>
/// <param name="TargetOperationId">The optional target operation id.</param>
/// <param name="ParameterBindings">The parameter bindings.</param>
/// <param name="RequestBodyExpression">The optional request body expression.</param>
/// <param name="Description">The optional description.</param>
/// <param name="SourceStatusCode">The source response status code.</param>
public readonly record struct LinkInfo(
    string LinkName,
    string? TargetOperationId,
    LinkParameterBinding[] ParameterBindings,
    string? RequestBodyExpression,
    string? Description,
    string SourceStatusCode);
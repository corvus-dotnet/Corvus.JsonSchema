// <copyright file="ResolvedOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// An operation that a workflow step targets, resolved against a source description — the
/// (path, method) pair the generator needs to bind a step to a generated request/response type
/// (plan §3.1).
/// </summary>
/// <param name="SourceName">The <c>name</c> of the source description that owns the operation.</param>
/// <param name="Path">The API path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="OperationId">The operation's <c>operationId</c>, or <see langword="null"/> if it declares none.</param>
public readonly record struct ResolvedOperation(
    string SourceName,
    string Path,
    OperationMethod Method,
    string? OperationId);
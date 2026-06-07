// <copyright file="ResolvedOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// An operation a workflow step targets, resolved against a source description (plan §3.1): the
/// generator's own <see cref="OperationDescriptor"/> — fully-qualified request/response type names
/// and request-parameter metadata — paired with the source it came from. The Arazzo generator adds
/// no naming or type logic of its own; it carries the generator's description through verbatim.
/// </summary>
/// <param name="SourceName">The <c>name</c> of the source description that owns the operation.</param>
/// <param name="Operation">The generator's description of the operation.</param>
public readonly record struct ResolvedOperation(
    string SourceName,
    OperationDescriptor Operation);
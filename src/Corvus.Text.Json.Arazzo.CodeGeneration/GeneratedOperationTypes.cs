// <copyright file="GeneratedOperationTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// The generated client types that a workflow step binds to — the request/response structs the
/// OpenAPI generator emits for the step's operation, named so the emitted executor can reference
/// them directly (plan §3.1).
/// </summary>
/// <param name="MethodName">The operation's generated method name (PascalCase), shared by the request and response types.</param>
/// <param name="RequestTypeName">The fully-qualified generated request type name (e.g. <c>Acme.Pets.GetPetRequest</c>).</param>
/// <param name="ResponseTypeName">The fully-qualified generated response type name (e.g. <c>Acme.Pets.GetPetResponse</c>).</param>
public readonly record struct GeneratedOperationTypes(
    string MethodName,
    string RequestTypeName,
    string ResponseTypeName);
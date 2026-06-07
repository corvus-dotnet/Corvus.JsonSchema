// <copyright file="GeneratedClientTypeNaming.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The single source of truth for the names of the per-operation types the OpenAPI client
/// generator emits. The generators use these suffixes when they emit the request/response structs,
/// and <c>ListOperations</c> uses them when it populates <see cref="OperationSummary.RequestTypeName"/>
/// and <see cref="OperationSummary.ResponseTypeName"/>, so emission and the reported mapping can
/// never drift — and downstream generators (e.g. Arazzo) consume the names rather than re-deriving
/// the convention.
/// </summary>
public static class GeneratedClientTypeNaming
{
    /// <summary>The suffix appended to a method name to form its request struct name.</summary>
    public const string RequestSuffix = "Request";

    /// <summary>The suffix appended to a method name to form its response struct name.</summary>
    public const string ResponseSuffix = "Response";

    /// <summary>
    /// Gets the simple (unqualified) request type name for a generated method name.
    /// </summary>
    /// <param name="methodName">The generated method name.</param>
    /// <returns>The request type name.</returns>
    public static string RequestTypeName(string methodName) => methodName + RequestSuffix;

    /// <summary>
    /// Gets the simple (unqualified) response type name for a generated method name.
    /// </summary>
    /// <param name="methodName">The generated method name.</param>
    /// <returns>The response type name.</returns>
    public static string ResponseTypeName(string methodName) => methodName + ResponseSuffix;
}
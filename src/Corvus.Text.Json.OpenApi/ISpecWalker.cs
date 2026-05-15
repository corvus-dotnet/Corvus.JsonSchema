// <copyright file="ISpecWalker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Walks an API specification document (OpenAPI or AsyncAPI) to enumerate
/// operations and extract referenced schemas for code generation.
/// </summary>
/// <remarks>
/// <para>
/// The walker takes a pre-parsed <see cref="JsonElement"/> root rather than
/// raw bytes. The caller is responsible for parsing the document and managing
/// its lifetime — all returned <see cref="OperationEntry"/> and
/// <see cref="ExtractedSchema"/> values hold element references into the
/// parsed document and are only valid while it is alive.
/// </para>
/// <para>
/// When a <see cref="IOpenApiReferenceResolver"/> is provided, the walker resolves
/// <c>$ref</c> references before extracting metadata. Without a resolver,
/// <c>$ref</c> elements are skipped.
/// </para>
/// </remarks>
public interface ISpecWalker
{
    /// <summary>
    /// Enumerates operations in the specification document, yielding one entry
    /// per (path, HTTP method) pair.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed specification document.</param>
    /// <param name="filter">An optional filter to restrict the paths included.</param>
    /// <param name="referenceResolver">An optional resolver for <c>$ref</c> references.
    /// If <see langword="null"/>, a <see cref="LocalReferenceResolver"/> for the
    /// <paramref name="specRoot"/> is used.</param>
    /// <returns>An enumerable of <see cref="OperationEntry"/> values.</returns>
    IEnumerable<OperationEntry> EnumerateOperations(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null);

    /// <summary>
    /// Extracts all schemas reachable from the (optionally filtered) operations,
    /// plus any component schemas.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed specification document.</param>
    /// <param name="filter">An optional filter to restrict the paths walked.</param>
    /// <param name="referenceResolver">An optional resolver for <c>$ref</c> references.
    /// If <see langword="null"/>, a <see cref="LocalReferenceResolver"/> for the
    /// <paramref name="specRoot"/> is used.</param>
    /// <returns>An enumerable of <see cref="ExtractedSchema"/> values.</returns>
    IEnumerable<ExtractedSchema> ExtractSchemas(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null);
}
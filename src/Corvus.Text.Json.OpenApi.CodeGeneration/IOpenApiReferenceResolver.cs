// <copyright file="IOpenApiReferenceResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Resolves OpenAPI <c>$ref</c> URI-references to JSON elements.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI <c>$ref</c> values are URI-references (RFC 3986) that may be:
/// </para>
/// <list type="bullet">
/// <item><description>Local fragment-only references, e.g. <c>#/components/parameters/PetId</c></description></item>
/// <item><description>Relative URI references, e.g. <c>./common.json#/components/parameters/PetId</c></description></item>
/// <item><description>Absolute URI references, e.g. <c>https://example.com/api.json#/components/parameters/PetId</c></description></item>
/// </list>
/// <para>
/// The resolver separates the URI from the fragment (JSON Pointer), locates the
/// document, and navigates to the target element within it.
/// </para>
/// </remarks>
public interface IOpenApiReferenceResolver
{
    /// <summary>
    /// Attempts to resolve a <c>$ref</c> URI-reference to the target JSON element.
    /// </summary>
    /// <param name="refValue">The <c>$ref</c> value as a UTF-8 byte span (the raw URI-reference).</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the resolved element.</param>
    /// <returns><see langword="true"/> if the reference was resolved successfully; otherwise, <see langword="false"/>.</returns>
    bool TryResolve(ReadOnlySpan<byte> refValue, out JsonElement result);

    /// <summary>
    /// Attempts to resolve a <c>$ref</c> URI-reference to the target JSON element.
    /// </summary>
    /// <param name="refValue">The <c>$ref</c> value as a string (the raw URI-reference).</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the resolved element.</param>
    /// <returns><see langword="true"/> if the reference was resolved successfully; otherwise, <see langword="false"/>.</returns>
    bool TryResolve(string refValue, out JsonElement result);

    /// <summary>
    /// Attempts to resolve a <c>$ref</c> URI-reference to a strongly-typed target,
    /// validating that the resolved element conforms to the target type's schema.
    /// </summary>
    /// <typeparam name="TTarget">The expected target type. The resolved element must
    /// pass <c>EvaluateSchema()</c> for this type.</typeparam>
    /// <param name="refValue">The <c>$ref</c> value as a string (the raw URI-reference).</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains
    /// the resolved and validated instance of <typeparamref name="TTarget"/>.</param>
    /// <returns><see langword="true"/> if the reference was resolved and the target validates
    /// against its schema; otherwise, <see langword="false"/>.</returns>
    bool TryResolve<TTarget>(string refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>;

    /// <summary>
    /// Attempts to resolve a <c>$ref</c> URI-reference to a strongly-typed target,
    /// validating that the resolved element conforms to the target type's schema.
    /// </summary>
    /// <typeparam name="TTarget">The expected target type. The resolved element must
    /// pass <c>EvaluateSchema()</c> for this type.</typeparam>
    /// <param name="refValue">The <c>$ref</c> value as a UTF-8 byte span (the raw URI-reference).</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains
    /// the resolved and validated instance of <typeparamref name="TTarget"/>.</param>
    /// <returns><see langword="true"/> if the reference was resolved and the target validates
    /// against its schema; otherwise, <see langword="false"/>.</returns>
    bool TryResolve<TTarget>(ReadOnlySpan<byte> refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>;
}
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

    /// <summary>
    /// Pushes the base URI context for the document identified by the given <c>$ref</c> value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per RFC 3986 §5, when resolution crosses a document boundary, the target document's
    /// URI becomes the base for resolving subsequent relative and fragment-only references.
    /// Call this method with the <c>$ref</c> string that triggered an external resolution,
    /// and dispose the returned scope when you are finished walking the resolved element's
    /// sub-structure.
    /// </para>
    /// <para>
    /// If <paramref name="refValue"/> is fragment-only (starts with <c>#</c>) or does not
    /// identify a known document, the returned scope is a no-op.
    /// </para>
    /// </remarks>
    /// <param name="refValue">The <c>$ref</c> URI-reference that was resolved.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous base URI context when disposed.</returns>
    IDisposable PushResolvedBase(string refValue);

    /// <summary>
    /// Resolves a raw <c>$ref</c> URI-reference against the current base URI per RFC 3986 §5,
    /// returning an absolute reference string suitable for the JSON Schema type builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs the same URI resolution as <see cref="PushResolvedBase"/> but
    /// returns the resolved absolute reference as a string rather than pushing the base context.
    /// The result preserves any fragment from the original <paramref name="refValue"/>.
    /// </para>
    /// <para>
    /// For fragment-only references (starting with <c>#</c>), the value is returned unchanged
    /// since it resolves against the entry document by convention.
    /// </para>
    /// <para>
    /// For references with a document part, the document portion is resolved against the
    /// current base URI to produce an absolute path (for file URIs) or absolute URI
    /// (for non-file schemes). The fragment portion is preserved as-is.
    /// </para>
    /// </remarks>
    /// <param name="refValue">The raw <c>$ref</c> value from the JSON document.</param>
    /// <returns>
    /// The resolved absolute reference. For file-based URIs this is a local file path
    /// with fragment (e.g. <c>D:\project\common\types.json#/components/schemas/Pet</c>).
    /// For non-file URIs, the full absolute URI with fragment is returned.
    /// </returns>
    string ResolveToAbsolute(string refValue);
}
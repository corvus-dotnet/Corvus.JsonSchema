// <copyright file="LocalReferenceResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Resolves local (<c>#/...</c>) OpenAPI <c>$ref</c> references within a single document.
/// </summary>
/// <remarks>
/// <para>
/// This resolver handles fragment-only URI-references (e.g. <c>#/components/parameters/PetId</c>)
/// by navigating the JSON Pointer against the document root. It does not support
/// external document references.
/// </para>
/// </remarks>
public sealed class LocalReferenceResolver : IOpenApiReferenceResolver
{
    private static readonly byte HashByte = (byte)'#';

    private readonly JsonElement documentRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalReferenceResolver"/> class.
    /// </summary>
    /// <param name="documentRoot">The root element of the OpenAPI document.</param>
    public LocalReferenceResolver(JsonElement documentRoot)
    {
        this.documentRoot = documentRoot;
    }

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> refValue, out JsonElement result)
    {
        // Only handle fragment-only references (#/...)
        if (refValue.Length == 0 || refValue[0] != HashByte)
        {
            result = default;
            return false;
        }

        // Strip the '#' to get the JSON Pointer
        ReadOnlySpan<byte> pointer = refValue.Slice(1);
        return this.documentRoot.TryResolvePointer(pointer, out result);
    }

    /// <inheritdoc/>
    public bool TryResolve(string refValue, out JsonElement result)
    {
        if (string.IsNullOrEmpty(refValue) || refValue[0] != '#')
        {
            result = default;
            return false;
        }

        // Strip the '#' to get the JSON Pointer
        ReadOnlySpan<char> pointer = refValue.AsSpan(1);
        return this.documentRoot.TryResolvePointer(pointer, out result);
    }

    /// <inheritdoc/>
    public bool TryResolve<TTarget>(string refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>
    {
        if (this.TryResolve(refValue, out JsonElement element))
        {
            IJsonElement ie = element;
            TTarget candidate = TTarget.CreateInstance(ie.ParentDocument, ie.ParentDocumentIndex);
            if (candidate.EvaluateSchema())
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryResolve<TTarget>(ReadOnlySpan<byte> refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>
    {
        if (this.TryResolve(refValue, out JsonElement element))
        {
            IJsonElement ie = element;
            TTarget candidate = TTarget.CreateInstance(ie.ParentDocument, ie.ParentDocumentIndex);
            if (candidate.EvaluateSchema())
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public IDisposable PushResolvedBase(string refValue) => EmptyScope.Instance;
}
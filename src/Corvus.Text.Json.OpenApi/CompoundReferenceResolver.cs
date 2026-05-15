// <copyright file="CompoundReferenceResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Resolves OpenAPI <c>$ref</c> references by delegating to an ordered list of resolvers.
/// </summary>
/// <remarks>
/// <para>
/// Each resolver is tried in order. The first resolver that returns <see langword="true"/>
/// from <see cref="IOpenApiReferenceResolver.TryResolve(ReadOnlySpan{byte}, out JsonElement)"/>
/// wins. Typically the <see cref="LocalReferenceResolver"/> is first (for <c>#/...</c> refs),
/// followed by file-system or HTTP resolvers for external documents.
/// </para>
/// </remarks>
public sealed class CompoundReferenceResolver : IOpenApiReferenceResolver
{
    private readonly IOpenApiReferenceResolver[] resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundReferenceResolver"/> class.
    /// </summary>
    /// <param name="resolvers">The ordered list of resolvers to delegate to.</param>
    public CompoundReferenceResolver(params IOpenApiReferenceResolver[] resolvers)
    {
        this.resolvers = resolvers;
    }

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> refValue, out JsonElement result)
    {
        for (int i = 0; i < this.resolvers.Length; i++)
        {
            if (this.resolvers[i].TryResolve(refValue, out result))
            {
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryResolve(string refValue, out JsonElement result)
    {
        for (int i = 0; i < this.resolvers.Length; i++)
        {
            if (this.resolvers[i].TryResolve(refValue, out result))
            {
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryResolve<TTarget>(string refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>
    {
        for (int i = 0; i < this.resolvers.Length; i++)
        {
            if (this.resolvers[i].TryResolve(refValue, out result))
            {
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
        for (int i = 0; i < this.resolvers.Length; i++)
        {
            if (this.resolvers[i].TryResolve(refValue, out result))
            {
                return true;
            }
        }

        result = default;
        return false;
    }
}
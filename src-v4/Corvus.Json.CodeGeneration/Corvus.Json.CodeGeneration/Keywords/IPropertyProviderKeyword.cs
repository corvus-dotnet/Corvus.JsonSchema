// <copyright file="IPropertyProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides properties.
/// </summary>
public interface IPropertyProviderKeyword : IKeyword
{
    /// <summary>
    /// Gets the relative priority at which the property provider will apply properties.
    /// </summary>
    public uint PropertyProviderPriority { get; }

    /// <summary>
    /// Collect the properties for the keyword.
    /// </summary>
    /// <param name="source">The source for the properties.</param>
    /// <param name="target">The target for the properties.</param>
    /// <param name="visitedTypeDeclarations">The type declarations we have already seen.</param>
    /// <param name="treatRequiredAsOptional">Whether to treat the required properties as optional.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void CollectProperties(
        TypeDeclaration source,
        TypeDeclaration target,
        HashSet<TypeDeclaration> visitedTypeDeclarations,
        bool treatRequiredAsOptional,
        CancellationToken cancellationToken);
}
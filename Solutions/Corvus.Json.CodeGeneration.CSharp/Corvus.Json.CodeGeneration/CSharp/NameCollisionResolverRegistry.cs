// <copyright file="NameCollisionResolverRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler registry for implementers of
/// <see cref="ILanguageProvider"/>.
/// </summary>
public sealed class NameCollisionResolverRegistry
{
    private readonly HashSet<INameCollisionResolver> registeredBuilders = [];
    private readonly Dictionary<IKeyword, IReadOnlyCollection<INameCollisionResolver>> buildersByKeyword = [];

    /// <summary>
    /// Gets the registered name heuristics.
    /// </summary>
    public IReadOnlyCollection<INameCollisionResolver> RegisteredCollisionResolvers => this.registeredBuilders;

    /// <summary>
    /// Registers name heuristics with the language provider.
    /// </summary>
    /// <param name="builders">The heuristics to register.</param>
    public void RegisterNameCollisionResolvers(params INameCollisionResolver[] builders)
    {
        foreach (INameCollisionResolver handler in builders)
        {
            this.registeredBuilders.Add(handler);
        }
    }
}
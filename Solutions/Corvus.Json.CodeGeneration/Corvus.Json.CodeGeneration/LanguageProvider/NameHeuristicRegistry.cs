// <copyright file="NameHeuristicRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A name heuristic registry for implementers of
/// <see cref="ILanguageProvider"/>.
/// </summary>
public sealed class NameHeuristicRegistry
{
    private readonly HashSet<INameHeuristic> registeredBuilders = [];
    private readonly Dictionary<IKeyword, IReadOnlyCollection<INameHeuristic>> buildersByKeyword = [];

    /// <summary>
    /// Gets the registered name heuristics.
    /// </summary>
    public IReadOnlyCollection<INameHeuristic> RegisteredHeuristics => this.registeredBuilders;

    /// <summary>
    /// Registers name heuristics with the language provider.
    /// </summary>
    /// <param name="builders">The heuristics to register.</param>
    public void RegisterNameHeuristics(params INameHeuristic[] builders)
    {
        foreach (INameHeuristic handler in builders)
        {
            this.registeredBuilders.Add(handler);
        }
    }
}
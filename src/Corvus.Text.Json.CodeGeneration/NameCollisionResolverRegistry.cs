// <copyright file="NameCollisionResolverRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A validation handler registry for implementers of
/// <see cref="ILanguageProvider"/>.
/// </summary>
public sealed class NameCollisionResolverRegistry
{
    private readonly Dictionary<IKeyword, IReadOnlyCollection<INameCollisionResolver>> buildersByKeyword = [];
    private readonly HashSet<INameCollisionResolver> registeredBuilders = [];

    /// <summary>
    /// Gets the registered name heuristics.
    /// </summary>
    public IReadOnlyCollection<INameCollisionResolver> RegisteredCollisionResolvers => registeredBuilders;

    /// <summary>
    /// Registers name heuristics with the language provider.
    /// </summary>
    /// <param name="builders">The heuristics to register.</param>
    public void RegisterNameCollisionResolvers(params INameCollisionResolver[] builders)
    {
        foreach (INameCollisionResolver handler in builders)
        {
            registeredBuilders.Add(handler);
        }
    }
}
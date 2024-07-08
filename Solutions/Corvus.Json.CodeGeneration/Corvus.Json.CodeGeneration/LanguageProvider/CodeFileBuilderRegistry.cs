// <copyright file="CodeFileBuilderRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation handler registry for implementers of
/// <see cref="ILanguageProvider"/>.
/// </summary>
public sealed class CodeFileBuilderRegistry
{
    private readonly HashSet<ICodeFileBuilder> registeredBuilders = [];
    private readonly Dictionary<IKeyword, IReadOnlyCollection<ICodeFileBuilder>> buildersByKeyword = [];

    /// <summary>
    /// Gets the registered code file builders.
    /// </summary>
    public IReadOnlyCollection<ICodeFileBuilder> RegisteredBuilders => this.registeredBuilders;

    /// <summary>
    /// Registers validation builders with the language provider.
    /// </summary>
    /// <param name="builders">The builders to register.</param>
    public void RegisterCodeFileBuilders(params ICodeFileBuilder[] builders)
    {
        foreach (ICodeFileBuilder handler in builders)
        {
            this.registeredBuilders.Add(handler);
        }
    }
}
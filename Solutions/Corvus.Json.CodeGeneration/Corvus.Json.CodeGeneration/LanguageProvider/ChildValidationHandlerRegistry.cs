// <copyright file="ChildValidationHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation handler registry for implementers of
/// <see cref="IValidationHandler"/>.
/// </summary>
public sealed class ChildValidationHandlerRegistry
{
    private readonly HashSet<IChildValidationHandler> registeredHandlers = [];

    /// <summary>
    /// Gets the registered validation handlers.
    /// </summary>
    public IReadOnlyCollection<IChildValidationHandler> RegisteredHandlers => this.registeredHandlers;

    /// <summary>
    /// Registers validation handlers with the language provider.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    public void RegisterValidationHandlers(params IChildValidationHandler[] handlers)
    {
        foreach (IChildValidationHandler handler in handlers)
        {
            this.registeredHandlers.Add(handler);
        }
    }
}
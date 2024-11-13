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
    private readonly List<IChildValidationHandler> registeredHandlers = [];

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
        this.registeredHandlers.AddRange(handlers);

        this.registeredHandlers.Sort(
            (l, r) =>
            {
                if (l.ValidationHandlerPriority == r.ValidationHandlerPriority)
                {
                    return l.GetType().Name.CompareTo(r.GetType().Name);
                }

                return l.ValidationHandlerPriority.CompareTo(r.ValidationHandlerPriority);
            });
    }
}
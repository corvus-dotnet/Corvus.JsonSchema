// <copyright file="MarkdownHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// A registry for <see cref="IMarkdownHandler"/> instances.
/// </summary>
public sealed class MarkdownHandlerRegistry
{
    private readonly HashSet<IMarkdownHandler> registeredHandlers = [];

    /// <summary>
    /// Gets the registered markdown handlers, ordered by priority.
    /// </summary>
    public IEnumerable<IMarkdownHandler> RegisteredHandlers =>
        this.registeredHandlers
            .OrderBy(h => h.HandlerPriority)
            .ThenBy(h => h.GetType().Name);

    /// <summary>
    /// Registers markdown handlers with the language provider.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    public void RegisterMarkdownHandlers(params IMarkdownHandler[] handlers)
    {
        foreach (IMarkdownHandler handler in handlers)
        {
            this.registeredHandlers.Add(handler);
        }
    }
}
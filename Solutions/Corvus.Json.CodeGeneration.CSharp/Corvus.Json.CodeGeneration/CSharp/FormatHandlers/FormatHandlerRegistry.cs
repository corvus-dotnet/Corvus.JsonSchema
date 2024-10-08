// <copyright file="FormatHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A registry for type format handlers.
/// </summary>
public sealed class FormatHandlerRegistry
{
    private readonly HashSet<IFormatHandler> handlers = [];

    private FormatHandlerRegistry()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="FormatHandlerRegistry"/>.
    /// </summary>
    public static FormatHandlerRegistry Instance { get; } = CreateDefaultInstance();

    /// <summary>
    /// Gets all format handlers.
    /// </summary>
    public IEnumerable<IFormatHandler> FormatHandlers => this.handlers.OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name);

    /// <summary>
    /// Gets all numeric type format handlers.
    /// </summary>
    public IEnumerable<INumberFormatHandler> NumberFormatHandlers => this.handlers.OfType<INumberFormatHandler>().OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name);

    /// <summary>
    /// Gets all string type format handlers.
    /// </summary>
    public IEnumerable<IStringFormatHandler> StringFormatHandlers => this.handlers.OfType<IStringFormatHandler>().OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name);

    /// <summary>
    /// Register a type format handler.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <returns>A reference to the registry having completed the operation.</returns>
    public FormatHandlerRegistry RegisterFormatHandlers(params IFormatHandler[] handlers)
    {
        foreach (IFormatHandler handler in handlers)
        {
            this.handlers.Add(handler);
        }

        return this;
    }

    private static FormatHandlerRegistry CreateDefaultInstance()
    {
        return new FormatHandlerRegistry()
            .RegisterFormatHandlers(
                WellKnownNumericFormatHandler.Instance,
                WellKnownStringFormatHandler.Instance);
    }
}
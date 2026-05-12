// <copyright file="FormatHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A registry for type format handlers.
/// </summary>
public sealed class FormatHandlerRegistry
{
    private readonly HashSet<IFormatHandler> handlers = [];
    private IReadOnlyList<IFormatHandler>? cachedFormatHandlers;
    private IReadOnlyList<INumberFormatHandler>? cachedNumberFormatHandlers;
    private IReadOnlyList<IStringFormatHandler>? cachedStringFormatHandlers;

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
    public IReadOnlyList<IFormatHandler> FormatHandlers => cachedFormatHandlers ??= handlers.OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name).ToArray();

    /// <summary>
    /// Gets all numeric type format handlers.
    /// </summary>
    public IReadOnlyList<INumberFormatHandler> NumberFormatHandlers => cachedNumberFormatHandlers ??= handlers.OfType<INumberFormatHandler>().OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name).ToArray();

    /// <summary>
    /// Gets all string type format handlers.
    /// </summary>
    public IReadOnlyList<IStringFormatHandler> StringFormatHandlers => cachedStringFormatHandlers ??= handlers.OfType<IStringFormatHandler>().OrderBy(h => h.Priority).ThenBy(h => h.GetType().Name).ToArray();

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

        // Invalidate cached lists when handlers change
        cachedFormatHandlers = null;
        cachedNumberFormatHandlers = null;
        cachedStringFormatHandlers = null;

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
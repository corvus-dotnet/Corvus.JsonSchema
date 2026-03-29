// <copyright file="KeywordValidationHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation handler registry for implementers of
/// <see cref="ILanguageProvider"/>.
/// </summary>
public sealed class KeywordValidationHandlerRegistry
{
    private readonly HashSet<IKeywordValidationHandler> registeredHandlers = [];
    private readonly Dictionary<IKeyword, IReadOnlyCollection<IKeywordValidationHandler>> handlersByKeyword = [];

    /// <summary>
    /// Gets the registered validation handlers.
    /// </summary>
    public IReadOnlyCollection<IKeywordValidationHandler> RegisteredHandlers => this.registeredHandlers;

    /// <summary>
    /// Registers validation handlers with the language provider.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    public void RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        foreach (IKeywordValidationHandler handler in handlers)
        {
            this.registeredHandlers.Add(handler);
        }
    }

    /// <summary>
    /// Gets the registered validation handlers for the given keyword.
    /// </summary>
    /// <param name="keyword">The keyword for which to get the handlers.</param>
    /// <param name="validationHandlers">The collection of <see cref="IKeywordValidationHandler"/> instances for the handler type, or
    /// <see langword="null"/> if no handler was registered.</param>
    /// <returns><see langword="true"/> if any handlers were found for the handler type.</returns>
    public bool TryGetHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
    {
        if (this.handlersByKeyword.TryGetValue(keyword, out validationHandlers))
        {
            return true;
        }

        validationHandlers =
            this.registeredHandlers
                .OfType<IKeywordValidationHandler>()
                .Where(h => h.HandlesKeyword(keyword))
                .ToArray();

        this.handlersByKeyword.Add(keyword, validationHandlers);
        return true;
    }
}
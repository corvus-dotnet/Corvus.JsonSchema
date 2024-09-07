// <copyright file="IValidatingLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Provides code generation semantics for the output language.
/// </summary>
public interface IValidatingLanguageProvider : ILanguageProvider
{
    /// <summary>
    /// Gets the registered validation handlers for the given keyword.
    /// </summary>
    /// <param name="keyword">The given keyword.</param>
    /// <param name="validationHandlers">The collection of <see cref="IValidationHandler"/> instances for the handler type, or
    /// <see langword="null"/> if no handler was registered.</param>
    /// <returns><see langword="true"/> if any handlers were found for the handler type.</returns>
    bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers);
}
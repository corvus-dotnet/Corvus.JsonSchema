// <copyright file="IKeywordValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation handler that is capable of handling one or more keywords.
/// </summary>
public interface IKeywordValidationHandler : IValidationHandler
{
    /// <summary>
    /// Gets a value indicating whether the validation handler can handle
    /// the given keyword.
    /// </summary>
    /// <param name="keyword">The keyword being handled.</param>
    /// <returns><see langword="true"/> if the handler handles the keyword.</returns>
    bool HandlesKeyword(IKeyword keyword);
}
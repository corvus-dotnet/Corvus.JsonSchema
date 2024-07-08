// <copyright file="IValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validation keywords.
/// </summary>
public interface IValidationKeyword : IKeyword
{
    /// <summary>
    /// Gets the relative priority at which the keyword will provide validation.
    /// </summary>
    uint ValidationPriority { get; }
}
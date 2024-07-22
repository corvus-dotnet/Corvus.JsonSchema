// <copyright file="IStringRegexValidationProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides a single validation regular expression for a string.
/// </summary>
/// <remarks>
/// These are regular expression values that may be used by code generators
/// to minimize overhead when implementing validators. They may be
/// cached, or provided as static values as appropriate.
/// </remarks>
public interface IStringRegexValidationProviderKeyword : IStringValueValidationKeyword, IValidationRegexProviderKeyword
{
}
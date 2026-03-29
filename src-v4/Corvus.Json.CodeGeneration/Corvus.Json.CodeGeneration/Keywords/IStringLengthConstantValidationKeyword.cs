// <copyright file="IStringLengthConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates string lengths against a constant.
/// </summary>
public interface IStringLengthConstantValidationKeyword : IStringValueValidationKeyword, IIntegerConstantValidationKeyword
{
    /// <summary>
    /// Gets the path modifier for the keyword.
    /// </summary>
    /// <returns>The path modifier for the keyword.</returns>
    string GetPathModifier();
}
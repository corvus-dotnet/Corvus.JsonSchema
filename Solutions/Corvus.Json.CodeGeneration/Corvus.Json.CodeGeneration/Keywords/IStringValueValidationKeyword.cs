// <copyright file="IStringValueValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.String"/> values,
/// where the actual string value is required.
/// </summary>
public interface IStringValueValidationKeyword : IStringValidationKeyword
{
}
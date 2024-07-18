// <copyright file="INumberValueValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Number"/> values
/// and requires the actual number.
/// </summary>
public interface INumberValueValidationKeyword : INumberValidationKeyword;
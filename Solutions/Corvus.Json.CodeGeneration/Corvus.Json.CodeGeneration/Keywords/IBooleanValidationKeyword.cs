// <copyright file="IBooleanValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.True"/> and
/// <see cref="System.Text.Json.JsonValueKind.False"/> values.
/// </summary>
public interface IBooleanValidationKeyword : IValueKindValidationKeyword;
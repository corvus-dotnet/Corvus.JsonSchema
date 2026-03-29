// <copyright file="INullValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Null"/> values.
/// </summary>
public interface INullValidationKeyword : IValueKindValidationKeyword;
// <copyright file="ILocalEvaluatedPropertyValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> properties using
/// local evaluated property tracking.
/// </summary>
/// <remarks>
/// Contrast with <see cref="ILocalAndAppliedEvaluatedPropertyValidationKeyword"/>.
/// </remarks>
public interface ILocalEvaluatedPropertyValidationKeyword
    : IObjectValidationKeyword,
      IFallbackObjectPropertyTypeProviderKeyword;
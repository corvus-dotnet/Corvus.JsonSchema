// <copyright file="ILocalAndAppliedEvaluatedPropertyValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> properties using
/// local and applied evaluated property tracking.
/// </summary>
/// /// <remarks>
/// Contrast with <see cref="ILocalEvaluatedPropertyValidationKeyword"/>.
/// </remarks>
public interface ILocalAndAppliedEvaluatedPropertyValidationKeyword
    : IObjectValidationKeyword,
      IFallbackObjectPropertyTypeProviderKeyword;
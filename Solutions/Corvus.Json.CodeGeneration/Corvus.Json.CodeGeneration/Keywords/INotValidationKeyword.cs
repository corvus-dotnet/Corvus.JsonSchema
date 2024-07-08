// <copyright file="INotValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where the composed validation condition must *not* be met.
/// </summary>
public interface INotValidationKeyword : IValidationKeyword, ISubschemaTypeBuilderKeyword;
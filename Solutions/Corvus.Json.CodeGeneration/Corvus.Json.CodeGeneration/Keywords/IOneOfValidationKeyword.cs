// <copyright file="IOneOfValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where exactly one of the composed validation conditions must be met.
/// </summary>
public interface IOneOfValidationKeyword : IValidationKeyword;
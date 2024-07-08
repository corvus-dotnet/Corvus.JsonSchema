// <copyright file="IAnyOfConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where one or more of the composed validation conditions must be met.
/// </summary>
public interface IAnyOfConstantValidationKeyword : IAnyOfValidationKeyword, IValidationConstantProviderKeyword;
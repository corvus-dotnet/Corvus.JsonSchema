// <copyright file="IPropertyCountConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates the number of properties in an object against a constant.
/// </summary>
public interface IPropertyCountConstantValidationKeyword : IIntegerConstantValidationKeyword, IObjectValidationKeyword;
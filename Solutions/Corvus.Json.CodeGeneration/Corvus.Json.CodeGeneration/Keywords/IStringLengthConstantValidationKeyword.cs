// <copyright file="IStringLengthConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates string lengths against a constant.
/// </summary>
public interface IStringLengthConstantValidationKeyword : IStringValidationKeyword, IIntegerConstantValidationKeyword;
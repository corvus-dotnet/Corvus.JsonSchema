// <copyright file="IArrayContainsCountConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that works with an <see cref="IArrayContainsValidationKeyword"/> to validate the number of items that match the contains value.
/// </summary>
public interface IArrayContainsCountConstantValidationKeyword : IIntegerConstantValidationKeyword, IArrayValidationKeyword
{
}
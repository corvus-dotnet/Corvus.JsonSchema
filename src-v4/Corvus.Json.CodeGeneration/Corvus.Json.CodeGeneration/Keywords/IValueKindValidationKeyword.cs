// <copyright file="IValueKindValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation keyword that is sensitive to the value kind of the
/// instance being validated.
/// </summary>
/// <remarks>
/// Most validation keywords are sensitive to the value kind being validated
/// (e.g. <see cref="IObjectValidationKeyword"/>, <see cref="IArrayValidationKeyword"/>)
/// and are ignored if the instance is not of the matching value kind.
/// </remarks>
public interface IValueKindValidationKeyword : IValidationKeyword;
// <copyright file="IReferenceKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword with reference semantics.
/// </summary>
public interface IReferenceKeyword : ISubschemaTypeBuilderKeyword, IPropertyProviderKeyword, IAllOfSubschemaValidationKeyword;
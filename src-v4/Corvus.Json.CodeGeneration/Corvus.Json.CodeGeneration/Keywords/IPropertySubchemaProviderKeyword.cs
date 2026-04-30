// <copyright file="IPropertySubchemaProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that directly provides properties, by defining their subschema.
/// </summary>
public interface IPropertySubchemaProviderKeyword : IPropertyProviderKeyword, ISubschemaProviderKeyword;
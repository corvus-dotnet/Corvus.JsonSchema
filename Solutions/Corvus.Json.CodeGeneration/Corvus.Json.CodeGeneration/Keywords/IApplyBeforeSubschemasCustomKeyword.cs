// <copyright file="IApplyBeforeSubschemasCustomKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A custom keyword that is applied before a scope is entered.
/// </summary>
public interface IApplyBeforeSubschemasCustomKeyword : ISchemaRegistrationCustomKeyword, ITypeBuilderCustomKeyword;
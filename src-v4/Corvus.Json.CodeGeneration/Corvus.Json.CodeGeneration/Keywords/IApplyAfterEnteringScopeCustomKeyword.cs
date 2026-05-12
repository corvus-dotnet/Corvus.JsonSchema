// <copyright file="IApplyAfterEnteringScopeCustomKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A custom keyword that is applied after a scope is entered.
/// </summary>
public interface IApplyAfterEnteringScopeCustomKeyword : ISchemaRegistrationCustomKeyword, ITypeBuilderCustomKeyword;
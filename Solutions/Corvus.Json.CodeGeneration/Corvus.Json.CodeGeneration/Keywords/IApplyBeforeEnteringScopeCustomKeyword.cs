// <copyright file="IApplyBeforeEnteringScopeCustomKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A custom keyword that is applied after a scope is entered.
/// </summary>
public interface IApplyBeforeEnteringScopeCustomKeyword : ISchemaRegistrationCustomKeyword, ITypeBuilderCustomKeyword;
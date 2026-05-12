// <copyright file="IDefinitionsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// A keyword that hosts reusable subschema definitions.
/// </summary>
public interface IDefinitionsKeyword : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword;
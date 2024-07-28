// <copyright file="ICompositionKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword which appplies subschema with some kind of composition relationship.
/// </summary>
public interface ICompositionKeyword : IKeyword;
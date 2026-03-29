// <copyright file="INameHeuristicBeforeSubschema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Defines a heuristic for generating names before <see cref="TypeDeclaration"/> subschema have been processed.
/// </summary>
public interface INameHeuristicBeforeSubschema : INameHeuristic;
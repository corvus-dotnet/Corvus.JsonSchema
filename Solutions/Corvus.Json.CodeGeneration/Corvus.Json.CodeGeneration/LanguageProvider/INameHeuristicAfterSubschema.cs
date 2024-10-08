// <copyright file="INameHeuristicAfterSubschema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Defines a heuristic for generating names after <see cref="TypeDeclaration"/> subschema have been processed.
/// </summary>
public interface INameHeuristicAfterSubschema : INameHeuristic;
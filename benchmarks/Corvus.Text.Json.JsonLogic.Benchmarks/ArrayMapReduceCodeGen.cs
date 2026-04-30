// <copyright file="ArrayMapReduceCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Source-generated evaluator for fused map-reduce:
/// reduce(map(items, item * 2), current + accumulator, 0).
/// </summary>
[JsonLogicRule("Rules/array-map-reduce.json")]
public static partial class ArrayMapReduceCodeGen
{
}

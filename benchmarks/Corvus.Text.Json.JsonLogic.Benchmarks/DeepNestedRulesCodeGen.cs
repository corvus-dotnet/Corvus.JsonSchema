// <copyright file="DeepNestedRulesCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Source-generated evaluator for a 50-deep if/else chain.
/// Rule: if x > 49 then 50, else if x > 48 then 49, ..., else 0.
/// </summary>
[JsonLogicRule("Rules/deep-nested.json")]
public static partial class DeepNestedRulesCodeGen
{
}

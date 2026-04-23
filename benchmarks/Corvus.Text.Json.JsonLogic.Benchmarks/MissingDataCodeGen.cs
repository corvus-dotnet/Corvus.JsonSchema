// <copyright file="MissingDataCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Source-generated evaluator for missing data with conditional:
/// if any of ["a","b"] missing → cat("Missing: ", missing), else a + b.
/// </summary>
[JsonLogicRule("Rules/missing-data.json")]
public static partial class MissingDataCodeGen
{
}

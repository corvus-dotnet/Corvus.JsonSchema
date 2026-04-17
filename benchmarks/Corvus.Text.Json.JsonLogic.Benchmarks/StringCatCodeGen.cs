// <copyright file="StringCatCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Source-generated evaluator for: {"cat":["Hello, ",{"var":"name"},"!"]}.
/// </summary>
[JsonLogicRule("Rules/string-cat.json")]
public static partial class StringCatCodeGen
{
}

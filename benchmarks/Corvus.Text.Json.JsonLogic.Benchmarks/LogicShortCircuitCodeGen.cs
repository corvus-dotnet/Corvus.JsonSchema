// <copyright file="LogicShortCircuitCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Source-generated evaluator for short-circuit logic:
/// {"and":[{"&gt;":[{"var":"temp"},0]},{"&lt;":[{"var":"temp"},110]},{"==":[{"var":"pie.filling"},"apple"]}]}.
/// </summary>
[JsonLogicRule("Rules/logic-short-circuit.json")]
public static partial class LogicShortCircuitCodeGen
{
}

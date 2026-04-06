// <copyright file="CustomOpRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator using a custom operator: double_it(x) = x * 2.
/// Rule: {"+":[{"double_it":[{"var":"x"}]}, {"var":"y"}]}.
/// </summary>
[JsonLogicRule("Rules/custom-op-rule.json")]
internal static partial class CustomOpRule
{
}
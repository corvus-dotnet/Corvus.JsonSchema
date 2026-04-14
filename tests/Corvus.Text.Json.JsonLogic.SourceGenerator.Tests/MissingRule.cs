// <copyright file="MissingRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for {"missing":["a","b","c"]}.
/// </summary>
[JsonLogicRule("Rules/missing-rule.json")]
internal static partial class MissingRule
{
}
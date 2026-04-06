// <copyright file="CatRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for {"cat":["Hello, ",{"var":"name"},"!"]}.
/// </summary>
[JsonLogicRule("Rules/cat-rule.json")]
internal static partial class CatRule
{
}
// <copyright file="AddRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for {"+":[{"var":"a"},{"var":"b"}]}.
/// </summary>
[JsonLogicRule("Rules/add-rule.json")]
internal static partial class AddRule
{
}
// <copyright file="IfRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for {"if":[{">":[{"var":"age"},18]},"adult","minor"]}.
/// </summary>
[JsonLogicRule("Rules/if-rule.json")]
internal static partial class IfRule
{
}
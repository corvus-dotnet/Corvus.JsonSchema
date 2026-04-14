// <copyright file="MapExpr.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;

namespace Corvus.Text.Json.Jsonata.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for $map(Account.Order, function($o) { $o.OrderID }).
/// </summary>
[JsonataExpression("Expressions/map-expr.jsonata")]
internal static partial class MapExpr
{
}
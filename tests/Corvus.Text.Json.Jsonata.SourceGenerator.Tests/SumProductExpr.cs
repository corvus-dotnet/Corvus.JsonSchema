// <copyright file="SumProductExpr.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;

namespace Corvus.Text.Json.Jsonata.SourceGenerator.Tests;

/// <summary>
/// Source-generated evaluator for $sum(Account.Order.Product.(Price * Quantity)).
/// </summary>
[JsonataExpression("Expressions/sum-product.jsonata")]
internal static partial class SumProductExpr
{
}
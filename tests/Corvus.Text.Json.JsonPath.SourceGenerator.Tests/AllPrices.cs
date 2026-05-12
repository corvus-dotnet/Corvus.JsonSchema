// <copyright file="AllPrices.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;

namespace Corvus.Text.Json.JsonPath.SourceGenerator.Tests;

/// <summary>
/// Extracts all prices using recursive descent.
/// </summary>
[JsonPathExpression("Expressions/all-prices.jsonpath")]
internal static partial class AllPrices;

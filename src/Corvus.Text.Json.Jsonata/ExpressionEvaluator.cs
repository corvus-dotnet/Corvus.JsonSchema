// <copyright file="ExpressionEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Delegate representing a compiled JSONata expression that evaluates against
/// input data and produces a <see cref="Sequence"/> result.
/// </summary>
/// <param name="input">The current context value being evaluated.</param>
/// <param name="environment">The variable binding environment (scope chain).</param>
/// <returns>The result sequence.</returns>
internal delegate Sequence ExpressionEvaluator(in JsonElement input, Environment environment);
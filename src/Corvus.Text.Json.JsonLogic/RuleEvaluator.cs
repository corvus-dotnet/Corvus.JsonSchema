// <copyright file="RuleEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// A compiled delegate that evaluates a JsonLogic rule (or sub-expression)
/// against a data context.
/// </summary>
/// <param name="data">The current data context.</param>
/// <param name="workspace">The workspace for intermediate document allocation.</param>
/// <returns>The evaluation result.</returns>
public delegate EvalResult RuleEvaluator(in JsonElement data, JsonWorkspace workspace);
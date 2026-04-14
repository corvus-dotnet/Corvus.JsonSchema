// <copyright file="DeepNestedRulesCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Hand-written code-generated evaluator for a 50-deep if/else chain.
/// Rule: if x > 49 then 50, else if x > 48 then 49, ..., else 0.
/// Uses a simple loop rather than a deeply nested call tree, which is
/// the natural optimisation a code generator would apply.
/// </summary>
public static class DeepNestedRulesCodeGen
{
    private const int Depth = 50;

    /// <summary>
    /// Evaluates the 50-deep if/else chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)
    {
        double x = CodeGenHelpers.CoerceToDouble(CodeGenHelpers.VarSimple(data, "x"u8));

        // The deeply nested if/else collapses to: find the highest threshold x exceeds
        for (int i = Depth; i > 0; i--)
        {
            if (x > i - 1)
            {
                return CodeGenHelpers.MaterializeDouble(i, workspace);
            }
        }

        return CodeGenHelpers.MaterializeDouble(0.0, workspace);
    }
}

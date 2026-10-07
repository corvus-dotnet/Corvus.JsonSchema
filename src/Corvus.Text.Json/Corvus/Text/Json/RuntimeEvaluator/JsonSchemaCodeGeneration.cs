// <copyright file="JsonSchemaCodeGeneration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// Whether, and when, a <see cref="JsonSchemaEvaluator"/> compiles its schema to code at run time
/// (see <see cref="JsonSchemaEvaluatorOptions.CodeGeneration"/>).
/// </summary>
/// <remarks>
/// <para>
/// Generated code gives the same results as the evaluator's interpreter and runs evaluations that collect no results
/// (<see cref="JsonSchemaEvaluator.Evaluate{T}(in T, IJsonSchemaResultsCollector?)"/> without a collector) about twice
/// as fast. An evaluation that collects results always uses the interpreter.
/// </para>
/// <para>
/// Code is generated only where the runtime can compile code at run time. Where it cannot (native AOT, and runtimes
/// that only interpret), every value behaves as <see cref="Disabled"/>. A schema that uses <c>$dynamicRef</c> or
/// <c>$recursiveRef</c> with a live dynamic scope is not compiled either.
/// </para>
/// </remarks>
public enum JsonSchemaCodeGeneration
{
    /// <summary>
    /// The schema is never compiled: the interpreter evaluates every instance. This is the default.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The schema is compiled on a background thread once the evaluator has made 1,000 evaluations that collect no
    /// results, and later evaluations use the code when it is ready. An evaluator that is used a few times never pays
    /// for compiling, and none waits for it.
    /// </summary>
    AfterWarmUp = 1,

    /// <summary>
    /// The schema is compiled on the calling thread before the evaluator's first evaluation that collects no results,
    /// which waits for it (milliseconds for a small schema, tens of milliseconds for a large one). For a long-lived
    /// evaluator whose first evaluations should already run at full speed.
    /// </summary>
    Eager = 2,
}
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
/// (<see cref="JsonSchemaEvaluator.Evaluate{T}(in T, IJsonSchemaResultsCollector?)"/> without a collector) faster:
/// about 2.8 times as fast over the 37 Sourcemeta benchmark corpora (geometric mean). An evaluation that collects
/// results always uses the interpreter.
/// </para>
/// <para>
/// The evaluators of one program (the entry points of one schema document set, such as the types of a generated
/// program) share its generated code: it is compiled once, by the first of them to compile, and each of the others
/// adds only a small entry of its own.
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
    /// The schema is never compiled: the interpreter evaluates every instance.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The schema's code is generated on a background thread once the evaluator has made 1,000 evaluations that collect
    /// no results, and later evaluations use it when it is ready. Each generated method is compiled by the JIT at its
    /// first call, so only the code that instances reach is compiled (a large schema's instances reach a fraction of
    /// it), and that call pays for its method. An evaluator that is used a few times never pays for generating code.
    /// This is the default.
    /// </summary>
    AfterWarmUp = 1,

    /// <summary>
    /// The schema's code is generated, and every method compiled, on the calling thread when the evaluator is made,
    /// which waits for it (milliseconds for a small schema, tens to hundreds of milliseconds for a large one), so that
    /// no evaluation waits for it. For a long-lived evaluator whose first evaluations should already run at full speed.
    /// An entry point added to the program afterwards compiles it again, at the next evaluation that collects no results.
    /// </summary>
    Eager = 2,
}
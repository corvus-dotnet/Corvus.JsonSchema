// <copyright file="IOperatorCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Compiles a custom JsonLogic operator into a <see cref="RuleEvaluator"/> delegate.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered with <see cref="JsonLogicEvaluator"/> by operator name.
/// When the evaluator encounters the operator during rule compilation, it calls
/// <see cref="Compile"/> with the pre-compiled operand evaluators.
/// </para>
/// <para>
/// Custom operators registered on a <see cref="JsonLogicEvaluator"/> are checked
/// <b>before</b> the built-in operators, allowing overrides of standard behaviour.
/// </para>
/// </remarks>
public interface IOperatorCompiler
{
    /// <summary>
    /// Compiles the operator given the pre-compiled operand evaluators.
    /// </summary>
    /// <param name="operands">
    /// An array of <see cref="RuleEvaluator"/> delegates, one per argument in the
    /// JsonLogic rule. Each delegate evaluates its sub-expression and returns an
    /// <see cref="EvalResult"/>.
    /// </param>
    /// <returns>
    /// A <see cref="RuleEvaluator"/> delegate that applies this operator to the
    /// evaluated operand results.
    /// </returns>
    RuleEvaluator Compile(RuleEvaluator[] operands);
}
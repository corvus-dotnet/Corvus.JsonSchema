// <copyright file="JsonLogicEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.JsonLogic.Operators;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Evaluates JsonLogic rules against JSON data.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator compiles rules into bytecode on first use, then caches the
/// compiled form keyed by the rule's raw JSON text.
/// Subsequent evaluations of the same rule skip compilation entirely.
/// </para>
/// <para>
/// Custom operators can be registered via <see cref="WithOperator(IJsonLogicOperator)"/>.
/// Each evaluator instance has its own operator set and cache.
/// </para>
/// </remarks>
public sealed class JsonLogicEvaluator
{
    /// <summary>
    /// Gets a default evaluator with all standard built-in operators.
    /// </summary>
    public static readonly JsonLogicEvaluator Default = new(BuiltInOperators.CreateAll());

    private readonly Dictionary<string, IJsonLogicOperator> operators;
    private readonly ConcurrentDictionary<string, CompiledRule> cache = new();

    private JsonLogicEvaluator(Dictionary<string, IJsonLogicOperator> operators)
    {
        this.operators = operators;
    }

    /// <summary>
    /// Creates a new evaluator with an additional custom operator registered.
    /// </summary>
    /// <param name="op">The operator to register.</param>
    /// <returns>A new evaluator instance with the operator added.</returns>
    /// <remarks>
    /// If an operator with the same name already exists, it is replaced.
    /// The new evaluator has its own empty cache.
    /// </remarks>
    public JsonLogicEvaluator WithOperator(IJsonLogicOperator op)
    {
        Dictionary<string, IJsonLogicOperator> newOps = new(this.operators)
        {
            [op.OperatorName] = op,
        };
        return new JsonLogicEvaluator(newOps);
    }

    /// <summary>
    /// Creates a new evaluator with the specified operator removed.
    /// </summary>
    /// <param name="operatorName">The name of the operator to remove.</param>
    /// <returns>A new evaluator instance without the specified operator.</returns>
    public JsonLogicEvaluator WithoutOperator(string operatorName)
    {
        Dictionary<string, IJsonLogicOperator> newOps = new(this.operators);
        newOps.Remove(operatorName);
        return new JsonLogicEvaluator(newOps);
    }

    /// <summary>
    /// Evaluates a JsonLogic rule against the provided data.
    /// </summary>
    /// <param name="rule">The JsonLogic rule to evaluate.</param>
    /// <param name="data">The data to evaluate the rule against.</param>
    /// <returns>The result of the evaluation as a <see cref="JsonElement"/>.</returns>
    /// <remarks>
    /// The rule is compiled to bytecode on first use, then cached.
    /// Subsequent calls with the same rule content skip compilation.
    /// </remarks>
    public JsonElement Evaluate(in JsonLogicRule rule, in JsonElement data)
    {
        CompiledRule compiled = this.GetOrCompile(rule);
        return JsonLogicVM.Execute(compiled, data);
    }

    /// <summary>
    /// Clears the compiled rule cache.
    /// </summary>
    /// <remarks>
    /// Call this if operator registrations have changed or to free memory.
    /// </remarks>
    public void ClearCache()
    {
        this.cache.Clear();
    }

    private CompiledRule GetOrCompile(in JsonLogicRule rule)
    {
        string key = rule.Rule.GetRawText();

        if (this.cache.TryGetValue(key, out CompiledRule existing))
        {
            return existing;
        }

        JsonLogicCompiler compiler = new(this.operators);
        CompiledRule compiled = compiler.Compile(rule.Rule);
        this.cache.TryAdd(key, compiled);
        return compiled;
    }
}
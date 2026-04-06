// <copyright file="JsonLogicEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Evaluates JsonLogic rules against JSON data.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator compiles rules into delegate trees on first use, then caches the
/// compiled form keyed by the rule's raw JSON text.
/// Subsequent evaluations of the same rule skip compilation entirely.
/// </para>
/// </remarks>
public sealed class JsonLogicEvaluator
{
    /// <summary>
    /// Gets a default evaluator with all standard built-in operators.
    /// </summary>
    public static readonly JsonLogicEvaluator Default = new();

    private readonly ConcurrentDictionary<string, FunctionalEvaluator.RuleEvaluator> cache = new();
    private FunctionalEvaluator.RuleEvaluator? _lastCompiled;
    private JsonElement _lastRule;

    private JsonLogicEvaluator()
    {
    }

    /// <summary>
    /// Evaluates a JsonLogic rule against the provided data.
    /// </summary>
    /// <param name="rule">The JsonLogic rule to evaluate.</param>
    /// <param name="data">The data to evaluate the rule against.</param>
    /// <returns>The result of the evaluation as a <see cref="JsonElement"/>.</returns>
    /// <remarks>
    /// <para>
    /// The rule is compiled to a delegate tree on first use, then cached.
    /// Subsequent calls with the same rule content skip compilation.
    /// </para>
    /// <para>
    /// This overload creates and disposes an internal workspace. The returned
    /// <see cref="JsonElement"/> is cloned so it is safe to use after the call returns.
    /// For zero-allocation evaluation, use the overload that accepts a <see cref="JsonWorkspace"/>.
    /// </para>
    /// </remarks>
    public JsonElement Evaluate(in JsonLogicRule rule, in JsonElement data)
    {
        FunctionalEvaluator.RuleEvaluator evaluator = this.GetOrCompile(rule);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        return FunctionalEvaluator.Execute(evaluator, data, workspace, cloneResult: true);
    }

    /// <summary>
    /// Evaluates a JsonLogic rule against the provided data, using the caller's workspace.
    /// </summary>
    /// <param name="rule">The JsonLogic rule to evaluate.</param>
    /// <param name="data">The data to evaluate the rule against.</param>
    /// <param name="workspace">
    /// The workspace for intermediate document allocation. The returned <see cref="JsonElement"/>
    /// may reference documents owned by this workspace, so it remains valid only while the
    /// workspace is alive and has not been reset.
    /// </param>
    /// <returns>The result of the evaluation as a <see cref="JsonElement"/>.</returns>
    /// <remarks>
    /// <para>
    /// The rule is compiled to a delegate tree on first use, then cached.
    /// Subsequent calls with the same rule content skip compilation.
    /// </para>
    /// <para>
    /// Because the result is not cloned, this overload avoids allocation at the evaluation
    /// boundary. The caller is responsible for ensuring the workspace outlives any use of
    /// the returned element.
    /// </para>
    /// </remarks>
    public JsonElement Evaluate(in JsonLogicRule rule, in JsonElement data, JsonWorkspace workspace)
    {
        FunctionalEvaluator.RuleEvaluator evaluator = this.GetOrCompile(rule);
        return FunctionalEvaluator.Execute(evaluator, data, workspace, cloneResult: false);
    }

    /// <summary>
    /// Clears the compiled rule cache.
    /// </summary>
    /// <remarks>
    /// Call this to free memory used by cached compiled rules.
    /// </remarks>
    public void ClearCache()
    {
        this.cache.Clear();
    }

    private FunctionalEvaluator.RuleEvaluator GetOrCompile(in JsonLogicRule rule)
    {
        // Fast path: same rule element as last call (zero allocation)
        if (_lastCompiled is not null && rule.Rule.Equals(_lastRule))
        {
            return _lastCompiled;
        }

        string key = rule.Rule.GetRawText();

        if (this.cache.TryGetValue(key, out FunctionalEvaluator.RuleEvaluator? existing))
        {
            _lastRule = rule.Rule;
            _lastCompiled = existing;
            return existing;
        }

        FunctionalEvaluator.RuleEvaluator compiled = FunctionalEvaluator.Compile(rule.Rule);
        this.cache.TryAdd(key, compiled);
        _lastRule = rule.Rule;
        _lastCompiled = compiled;
        return compiled;
    }
}
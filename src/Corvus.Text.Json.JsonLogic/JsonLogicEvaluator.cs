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

    private readonly ConcurrentDictionary<string, RuleEvaluator> cache = new();
    private readonly IReadOnlyDictionary<string, IOperatorCompiler>? customOperators;
    private RuleEvaluator? _lastCompiled;
    private JsonElement _lastRule;

    private JsonLogicEvaluator()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLogicEvaluator"/> class
    /// with the specified custom operator compilers.
    /// </summary>
    /// <param name="customOperators">
    /// A dictionary mapping operator names to their compilers.
    /// Custom operators are checked <b>before</b> the built-in operators,
    /// so they can override standard behaviour.
    /// </param>
    public JsonLogicEvaluator(IReadOnlyDictionary<string, IOperatorCompiler> customOperators)
    {
        this.customOperators = customOperators ?? throw new ArgumentNullException(nameof(customOperators));
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
        RuleEvaluator evaluator = this.GetOrCompile(rule);
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
        RuleEvaluator evaluator = this.GetOrCompile(rule);
        return FunctionalEvaluator.Execute(evaluator, data, workspace, cloneResult: false);
    }

    /// <summary>
    /// Evaluates a JsonLogic rule expressed as a JSON string against a JSON data string.
    /// </summary>
    /// <param name="ruleJson">The JsonLogic rule as a JSON string.</param>
    /// <param name="dataJson">The input data as a JSON string.</param>
    /// <returns>The result as a JSON string, or <c>null</c> if the result is undefined.</returns>
    public string? EvaluateToString(string ruleJson, string dataJson)
    {
        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(ruleJson));
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(dataJson));
        var result = this.Evaluate(new JsonLogicRule(ruleDoc.RootElement), dataDoc.RootElement);
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return result.GetRawText();
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

    private RuleEvaluator GetOrCompile(in JsonLogicRule rule)
    {
        // Fast path: same rule element as last call (zero allocation).
        // The cached element may outlive the document that owned it,
        // so guard against ObjectDisposedException.
        if (_lastCompiled is not null)
        {
            try
            {
                if (rule.Rule.Equals(_lastRule))
                {
                    return _lastCompiled;
                }
            }
            catch (ObjectDisposedException)
            {
                _lastCompiled = null;
            }
        }

        string key = rule.Rule.GetRawText();

        if (this.cache.TryGetValue(key, out RuleEvaluator? existing))
        {
            _lastRule = rule.Rule;
            _lastCompiled = existing;
            return existing;
        }

        RuleEvaluator compiled = FunctionalEvaluator.Compile(rule.Rule, this.customOperators);
        this.cache.TryAdd(key, compiled);
        _lastRule = rule.Rule;
        _lastCompiled = compiled;
        return compiled;
    }
}
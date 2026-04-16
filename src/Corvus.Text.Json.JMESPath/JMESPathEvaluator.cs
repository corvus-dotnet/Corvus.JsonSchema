// <copyright file="JMESPathEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Evaluates JMESPath expressions against JSON data. Compiled expressions
/// are cached for efficient repeated evaluation.
/// </summary>
public sealed class JMESPathEvaluator
{
    private static readonly JMESPathEvaluator DefaultInstance = new();
    private readonly ConcurrentDictionary<string, JMESPathEval> cache = new();

    /// <summary>
    /// Gets the default shared evaluator instance.
    /// </summary>
    public static JMESPathEvaluator Default => DefaultInstance;

    /// <summary>
    /// Evaluates a JMESPath expression against the given data, using an internally
    /// managed workspace. The result is cloned and safe to use after this call returns.
    /// </summary>
    /// <param name="expression">The JMESPath expression string.</param>
    /// <param name="data">The JSON data to query.</param>
    /// <returns>The query result.</returns>
    public JsonElement Search(string expression, in JsonElement data)
    {
        JMESPathEval compiled = this.GetOrCompile(expression);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = compiled(data, workspace);
        if (result.IsNullOrUndefined())
        {
            return JsonElement.ParseValue("null"u8);
        }

        return result.Clone();
    }

    /// <summary>
    /// Evaluates a JMESPath expression against the given data, using the caller's workspace.
    /// The result lifetime is tied to the workspace.
    /// </summary>
    /// <param name="expression">The JMESPath expression string.</param>
    /// <param name="data">The JSON data to query.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>The query result.</returns>
    public JsonElement Search(string expression, in JsonElement data, JsonWorkspace workspace)
    {
        JMESPathEval compiled = this.GetOrCompile(expression);
        return compiled(data, workspace);
    }

    private JMESPathEval GetOrCompile(string expression)
    {
        if (this.cache.TryGetValue(expression, out JMESPathEval? existing))
        {
            return existing;
        }

        JMESPathEval compiled = Compiler.Compile(expression);
        this.cache.TryAdd(expression, compiled);
        return compiled;
    }
}

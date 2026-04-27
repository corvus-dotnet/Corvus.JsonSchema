// <copyright file="JsonPathEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Evaluates JSONPath (RFC 9535) expressions against JSON data. Compiled expressions
/// are cached for efficient repeated evaluation.
/// </summary>
public sealed class JsonPathEvaluator
{
    private static readonly JsonPathEvaluator DefaultInstance = new();
    private static readonly JsonElement EmptyArray = JsonElement.ParseValue("[]"u8);
    private readonly ConcurrentDictionary<string, JsonPathEval> cache = new();

    /// <summary>
    /// Gets the default shared evaluator instance.
    /// </summary>
    public static JsonPathEvaluator Default => DefaultInstance;

    /// <summary>
    /// Evaluates a JSONPath expression against the given data, using an internally
    /// managed workspace. The result is cloned and safe to use after this call returns.
    /// </summary>
    /// <param name="expression">The JSONPath expression string (e.g., <c>"$.store.book[*].title"</c>).</param>
    /// <param name="data">The JSON data to query.</param>
    /// <returns>A JSON array containing the matched node list. Returns an empty array if no nodes match.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonElement Query(string expression, in JsonElement data)
    {
        JsonPathEval compiled = this.GetOrCompile(expression);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        try
        {
            JsonElement result = compiled(data, workspace);
            if (result.IsNullOrUndefined() || result.GetArrayLength() == 0)
            {
                return EmptyArray;
            }

            return result.Clone();
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Error evaluating JSONPath expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Evaluates a JSONPath expression against the given data, using the caller's workspace.
    /// The result lifetime is tied to the workspace.
    /// </summary>
    /// <param name="expression">The JSONPath expression string.</param>
    /// <param name="data">The JSON data to query.</param>
    /// <param name="workspace">The workspace for intermediate allocations.</param>
    /// <returns>A JSON array containing the matched node list.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonElement Query(string expression, in JsonElement data, JsonWorkspace workspace)
    {
        JsonPathEval compiled = this.GetOrCompile(expression);
        try
        {
            return compiled(data, workspace);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Error evaluating JSONPath expression: {ex.Message}", ex);
        }
    }

    private JsonPathEval GetOrCompile(string expression)
    {
        if (this.cache.TryGetValue(expression, out JsonPathEval? existing))
        {
            return existing;
        }

        JsonPathEval compiled;
        try
        {
            compiled = Compiler.Compile(expression);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Error compiling JSONPath expression: {ex.Message}", ex);
        }

        this.cache.TryAdd(expression, compiled);
        return compiled;
    }
}

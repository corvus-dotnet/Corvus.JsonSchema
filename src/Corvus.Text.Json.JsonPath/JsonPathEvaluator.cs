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
    private readonly ConcurrentDictionary<string, Compiler.CompiledJsonPath> cache = new();

    /// <summary>
    /// Gets the default shared evaluator instance.
    /// </summary>
    public static JsonPathEvaluator Default => DefaultInstance;

    /// <summary>
    /// Evaluates a JSONPath expression against the given data, returning matched nodes
    /// in a disposable <see cref="JsonPathResult"/>. Uses an <see cref="System.Buffers.ArrayPool{T}"/>-backed
    /// buffer internally.
    /// </summary>
    /// <param name="expression">The JSONPath expression string (e.g., <c>"$.store.book[*].title"</c>).</param>
    /// <param name="data">The JSON data to query.</param>
    /// <returns>
    /// A <see cref="JsonPathResult"/> containing the matched nodes. The caller must
    /// dispose the result to return any rented memory.
    /// </returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonPathResult QueryNodes(string expression, in JsonElement data)
    {
        Compiler.CompiledJsonPath compiled = this.GetOrCompile(expression);
        JsonPathResult result = JsonPathResult.CreatePooled(16);
        try
        {
            compiled.ExecuteNodes(data, ref result);
            return result;
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            result.Dispose();
            throw new JsonPathException($"Error evaluating JSONPath expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Evaluates a JSONPath expression against the given data, returning matched nodes
    /// in a disposable <see cref="JsonPathResult"/>. The initial buffer is caller-provided
    /// and can be stack-allocated for zero-heap-allocation queries.
    /// </summary>
    /// <param name="expression">The JSONPath expression string (e.g., <c>"$.store.book[*].title"</c>).</param>
    /// <param name="data">The JSON data to query.</param>
    /// <param name="initialBuffer">
    /// A caller-provided span (typically stack-allocated) for storing result nodes.
    /// If the result exceeds this buffer, an <see cref="System.Buffers.ArrayPool{T}"/>
    /// rental is used transparently.
    /// </param>
    /// <returns>
    /// A <see cref="JsonPathResult"/> containing the matched nodes. The caller must
    /// dispose the result to return any rented memory.
    /// </returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonPathResult QueryNodes(string expression, in JsonElement data, Span<JsonElement> initialBuffer)
    {
        Compiler.CompiledJsonPath compiled = this.GetOrCompile(expression);
        JsonPathResult result = new(initialBuffer);
        try
        {
            compiled.ExecuteNodes(data, ref result);
            return result;
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            result.Dispose();
            throw new JsonPathException($"Error evaluating JSONPath expression: {ex.Message}", ex);
        }
    }

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
        using JsonPathResult result = this.QueryNodes(expression, data);

        if (result.Count == 0)
        {
            return EmptyArray;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        return JsonPathCodeGenHelpers.BuildArrayFromSpan(result.Nodes, workspace).Clone();
    }

    private Compiler.CompiledJsonPath GetOrCompile(string expression)
    {
        if (this.cache.TryGetValue(expression, out Compiler.CompiledJsonPath? existing))
        {
            return existing;
        }

        Compiler.CompiledJsonPath compiled;
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

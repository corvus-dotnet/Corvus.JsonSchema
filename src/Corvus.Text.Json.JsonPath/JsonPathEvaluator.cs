// <copyright file="JsonPathEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;

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
#if NET9_0_OR_GREATER
    private readonly ConcurrentDictionary<byte[], Compiler.CompiledJsonPath> utf8Cache = new(Utf8KeyComparer.Instance);
#endif
    private readonly IReadOnlyDictionary<string, IJsonPathFunction>? customFunctions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathEvaluator"/> class
    /// with no custom functions.
    /// </summary>
    public JsonPathEvaluator()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathEvaluator"/> class
    /// with the specified custom function registry.
    /// </summary>
    /// <param name="customFunctions">
    /// A dictionary mapping function names to their implementations.
    /// The built-in function names (<c>length</c>, <c>count</c>, <c>value</c>,
    /// <c>match</c>, <c>search</c>) are reserved and cannot be overridden.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if any key conflicts with a built-in function name.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Custom functions participate in RFC 9535 well-typedness checking at parse time.
    /// Implementations must be thread-safe.
    /// </para>
    /// </remarks>
    public JsonPathEvaluator(IReadOnlyDictionary<string, IJsonPathFunction> customFunctions)
    {
        ValidateNoBuiltInOverrides(customFunctions);
        this.customFunctions = customFunctions;
    }

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
    /// Evaluates a UTF-8 JSONPath expression against the given data, returning matched nodes in a
    /// disposable <see cref="JsonPathResult"/>. Avoids materializing a managed query string;
    /// compiled queries are cached by UTF-8 content (looked up without allocation).
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 JSONPath expression (e.g., <c>"$.store.book[*].title"u8</c>).</param>
    /// <param name="data">The JSON data to query.</param>
    /// <returns>A <see cref="JsonPathResult"/> containing the matched nodes; dispose to return rented memory.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonPathResult QueryNodes(ReadOnlySpan<byte> utf8Expression, in JsonElement data)
    {
        Compiler.CompiledJsonPath compiled = this.GetOrCompile(utf8Expression);
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
    /// Evaluates a UTF-8 JSONPath expression against the given data with a caller-provided initial
    /// buffer (typically stack-allocated) for zero-heap-allocation queries.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 JSONPath expression.</param>
    /// <param name="data">The JSON data to query.</param>
    /// <param name="initialBuffer">A caller-provided span for result nodes; an <see cref="System.Buffers.ArrayPool{T}"/> rental is used if exceeded.</param>
    /// <returns>A <see cref="JsonPathResult"/> containing the matched nodes; dispose to return rented memory.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonPathResult QueryNodes(ReadOnlySpan<byte> utf8Expression, in JsonElement data, Span<JsonElement> initialBuffer)
    {
        Compiler.CompiledJsonPath compiled = this.GetOrCompile(utf8Expression);
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
    /// Evaluates a JSONPath expression against the given data, returning a JSON array
    /// of matched nodes. The returned element is backed by the provided workspace.
    /// </summary>
    /// <param name="expression">The JSONPath expression string (e.g., <c>"$.store.book[*].title"</c>).</param>
    /// <param name="data">The JSON data to query.</param>
    /// <param name="workspace">The workspace for document allocation. The returned element is backed by this workspace.</param>
    /// <returns>A JSON array containing the matched node list. Returns an empty array if no nodes match.</returns>
    /// <exception cref="JsonPathException">Thrown if the expression is syntactically invalid.</exception>
    public JsonElement Query(string expression, in JsonElement data, JsonWorkspace workspace)
    {
        using JsonPathResult result = this.QueryNodes(expression, data);

        if (result.Count == 0)
        {
            return EmptyArray;
        }

        return JsonPathCodeGenHelpers.BuildArrayFromSpan(result.Nodes, workspace);
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
            compiled = Compiler.Compile(expression, this.customFunctions);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Error compiling JSONPath expression: {ex.Message}", ex);
        }

        this.cache.TryAdd(expression, compiled);
        return compiled;
    }

    private Compiler.CompiledJsonPath GetOrCompile(ReadOnlySpan<byte> utf8Expression)
    {
#if NET9_0_OR_GREATER
        if (this.utf8Cache.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(utf8Expression, out Compiler.CompiledJsonPath? existing))
        {
            return existing;
        }

        byte[] key = utf8Expression.ToArray();
        Compiler.CompiledJsonPath compiled;
        try
        {
            compiled = Compiler.Compile(key, this.customFunctions);
        }
        catch (Exception ex) when (ex is not JsonPathException)
        {
            throw new JsonPathException($"Error compiling JSONPath expression: {ex.Message}", ex);
        }

        this.utf8Cache.TryAdd(key, compiled);
        return compiled;
#else
        // Older targets lack alternate-key lookup; transcode and use the string cache.
        return this.GetOrCompile(System.Text.Encoding.UTF8.GetString(utf8Expression.ToArray()));
#endif
    }

    private static void ValidateNoBuiltInOverrides(IReadOnlyDictionary<string, IJsonPathFunction> functions)
    {
        ReadOnlySpan<string> reserved = ["length", "count", "value", "match", "search"];
        foreach (string name in reserved)
        {
            if (functions.ContainsKey(name))
            {
                throw new ArgumentException(
                    $"Cannot override built-in function '{name}'. Built-in function names are reserved.",
                    nameof(functions));
            }
        }
    }

#if NET9_0_OR_GREATER
    /// <summary>
    /// Equality comparer for UTF-8 query keys that supports zero-allocation lookup by
    /// <see cref="ReadOnlySpan{T}"/> (a byte array is materialized only when a new key is added).
    /// </summary>
    private sealed class Utf8KeyComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
    {
        public static Utf8KeyComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => GetHashCode((ReadOnlySpan<byte>)obj);

        public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

        public int GetHashCode(ReadOnlySpan<byte> alternate)
        {
            HashCode hash = default;
            hash.AddBytes(alternate);
            return hash.ToHashCode();
        }

        public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
    }
#endif
}
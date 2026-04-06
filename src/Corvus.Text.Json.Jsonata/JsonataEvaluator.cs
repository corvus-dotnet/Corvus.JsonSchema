// <copyright file="JsonataEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text.Json;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Evaluates JSONata expressions against JSON data.
/// </summary>
/// <remarks>
/// <para>
/// Expressions are compiled to delegate trees on first use and cached
/// for subsequent evaluations. This class is thread-safe.
/// </para>
/// <para>
/// For best performance, create a single <see cref="JsonataEvaluator"/> instance
/// and reuse it across evaluations.
/// </para>
/// </remarks>
public sealed class JsonataEvaluator
{
    /// <summary>
    /// The default evaluator instance with no custom bindings.
    /// </summary>
    public static readonly JsonataEvaluator Default = new();

    private readonly ConcurrentDictionary<string, ExpressionEvaluator> cache = new();

    /// <summary>
    /// Evaluates a JSONata expression against JSON data.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data.</param>
    /// <param name="maxDepth">
    /// The maximum recursion depth for function calls. Defaults to
    /// <see cref="Environment.DefaultMaxDepth"/>. Pass a lower value
    /// for safety, or a higher value for deeply recursive expressions.
    /// </param>
    /// <returns>
    /// The result as a <see cref="JsonElement"/>. Returns a <c>default</c>
    /// <see cref="JsonElement"/> (with <see cref="JsonValueKind.Undefined"/>)
    /// if the expression produces no result.
    /// </returns>
    public JsonElement Evaluate(string expression, JsonElement data, int maxDepth = Environment.DefaultMaxDepth)
    {
        var compiled = this.GetOrCompile(expression);

        var env = new Environment
        {
            RootInput = data,
            MaxDepth = maxDepth,
        };

        var result = compiled(data, env);

        if (result.IsUndefined)
        {
            return default;
        }

        if (result.IsSingleton)
        {
            return result.FirstOrDefault;
        }

        using var ms = new MemoryStream(256);
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartArray();
        for (int i = 0; i < result.Count; i++)
        {
            result[i].WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();
        ms.Position = 0;
        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Evaluates a JSONata expression against a JSON string.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="json">The input JSON string.</param>
    /// <returns>The result as a JSON string, or <c>null</c> if undefined.</returns>
    public string? EvaluateToString(string expression, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = this.Evaluate(expression, doc.RootElement);
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return result.GetRawText();
    }

    private ExpressionEvaluator GetOrCompile(string expression)
    {
        if (this.cache.TryGetValue(expression, out var cached))
        {
            return cached;
        }

        var ast = Parser.Parse(expression);
        var compiled = FunctionalCompiler.Compile(ast);

        this.cache.TryAdd(expression, compiled);
        return compiled;
    }
}
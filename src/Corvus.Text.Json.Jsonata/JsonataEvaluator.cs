// <copyright file="JsonataEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

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
    /// <param name="timeLimitMs">
    /// The maximum time in milliseconds for expression evaluation. Zero means no limit.
    /// When the limit is exceeded, a <see cref="JsonataException"/> with code <c>U1001</c> is thrown.
    /// </param>
    /// <returns>
    /// The result as a <see cref="JsonElement"/>. Returns a <c>default</c>
    /// <see cref="JsonElement"/> (with <see cref="JsonValueKind.Undefined"/>)
    /// if the expression produces no result.
    /// </returns>
    public JsonElement Evaluate(string expression, JsonElement data, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        return this.Evaluate(expression, data, (IReadOnlyDictionary<string, JsonElement>?)null, maxDepth, timeLimitMs);
    }

    /// <summary>
    /// Evaluates a JSONata expression against input data with optional variable bindings.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="bindings">Optional pre-defined variable bindings (name → value).</param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>The evaluation result as a <see cref="JsonElement"/>, or <c>default</c> if the result is undefined.</returns>
    public JsonElement Evaluate(string expression, JsonElement data, IReadOnlyDictionary<string, JsonElement>? bindings, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        var compiled = this.GetOrCompile(expression);

        using JsonWorkspace workspace = JsonWorkspace.Create();

        var env = Environment.RentRoot();
        try
        {
            env.RootInput = data;
            env.MaxDepth = maxDepth;
            env.Workspace = workspace;

            if (timeLimitMs > 0)
            {
                env.TimeLimitMs = timeLimitMs;
                env.StartTimer();
            }

            if (bindings is not null)
            {
                foreach (var kvp in bindings)
                {
                    env.Bind(kvp.Key, new Sequence(kvp.Value));
                }
            }

            var result = compiled(data, env);

            if (result.IsUndefined)
            {
                return default;
            }

            JsonElement resultElement = result.IsSingleton
                ? result.FirstOrDefault
                : JsonataHelpers.ArrayFromSequence(result, workspace);

            // All elements consumed — return the backing array to the pool.
            result.ReturnBackingArray();

            if (resultElement.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            // Clone the result into a standalone element before the workspace is disposed.
            // The workspace owns all intermediate documents; Clone() performs a binary copy
            // of the backing metadata and value buffers (no serialize→parse round-trip).
            // The backing ParsedJsonDocument will be collected by GC when the returned
            // element is no longer referenced.
            return resultElement.Clone();
        }
        finally
        {
            Environment.ReturnRoot(env);
        }
    }

    /// <summary>
    /// Evaluates a JSONata expression against a JSON string.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="json">The input JSON string.</param>
    /// <returns>The result as a JSON string, or <c>null</c> if undefined.</returns>
    public string? EvaluateToString(string expression, string json)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(json));
        var result = this.Evaluate(expression, doc.RootElement);
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return result.GetRawText();
    }

    /// <summary>
    /// Evaluates a JSONata expression against input data using the caller's workspace.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">
    /// The workspace for intermediate document allocation. The returned <see cref="JsonElement"/>
    /// may reference documents owned by this workspace, so it remains valid only while the
    /// workspace is alive and has not been reset.
    /// </param>
    /// <param name="bindings">Optional pre-defined variable bindings (name → value).</param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>The evaluation result as a <see cref="JsonElement"/>, or <c>default</c> if the result is undefined.</returns>
    /// <remarks>
    /// <para>
    /// Because the result is not cloned, this overload avoids allocation at the evaluation
    /// boundary. The caller is responsible for ensuring the workspace outlives any use of
    /// the returned element.
    /// </para>
    /// </remarks>
    public JsonElement Evaluate(string expression, JsonElement data, JsonWorkspace workspace, IReadOnlyDictionary<string, JsonElement>? bindings = null, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        var compiled = this.GetOrCompile(expression);

        var env = Environment.RentRoot();
        try
        {
            env.RootInput = data;
            env.MaxDepth = maxDepth;
            env.Workspace = workspace;

            if (timeLimitMs > 0)
            {
                env.TimeLimitMs = timeLimitMs;
                env.StartTimer();
            }

            if (bindings is not null)
            {
                foreach (var kvp in bindings)
                {
                    env.Bind(kvp.Key, new Sequence(kvp.Value));
                }
            }

            var result = compiled(data, env);

            if (result.IsUndefined)
            {
                return default;
            }

            JsonElement resultElement = result.IsSingleton
                ? result.FirstOrDefault
                : JsonataHelpers.ArrayFromSequence(result, workspace);

            // All elements consumed — return the backing array to the pool.
            result.ReturnBackingArray();

            if (resultElement.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            return resultElement;
        }
        finally
        {
            Environment.ReturnRoot(env);
        }
    }

    /// <summary>
    /// Evaluates a JSONata expression against input data with optional typed bindings
    /// that can include both values and callable functions.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="bindings">Optional pre-defined bindings (name → value or function).</param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>The evaluation result as a <see cref="JsonElement"/>, or <c>default</c> if the result is undefined.</returns>
    public JsonElement Evaluate(string expression, JsonElement data, IReadOnlyDictionary<string, JsonataBinding>? bindings, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        var compiled = this.GetOrCompile(expression);

        using JsonWorkspace workspace = JsonWorkspace.Create();

        var env = Environment.RentRoot();
        try
        {
            env.RootInput = data;
            env.MaxDepth = maxDepth;
            env.Workspace = workspace;

            if (timeLimitMs > 0)
            {
                env.TimeLimitMs = timeLimitMs;
                env.StartTimer();
            }

            ApplyBindings(env, bindings, workspace);

            var result = compiled(data, env);

            if (result.IsUndefined)
            {
                return default;
            }

            JsonElement resultElement = result.IsSingleton
                ? result.FirstOrDefault
                : JsonataHelpers.ArrayFromSequence(result, workspace);

            // All elements consumed — return the backing array to the pool.
            result.ReturnBackingArray();

            if (resultElement.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            // Clone the result into a standalone element before the workspace is disposed.
            return resultElement.Clone();
        }
        finally
        {
            Environment.ReturnRoot(env);
        }
    }

    /// <summary>
    /// Evaluates a JSONata expression against input data using the caller's workspace,
    /// with optional typed bindings that can include both values and callable functions.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">
    /// The workspace for intermediate document allocation. The returned <see cref="JsonElement"/>
    /// may reference documents owned by this workspace, so it remains valid only while the
    /// workspace is alive and has not been reset.
    /// </param>
    /// <param name="bindings">Optional pre-defined bindings (name → value or function).</param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>The evaluation result as a <see cref="JsonElement"/>, or <c>default</c> if the result is undefined.</returns>
    /// <remarks>
    /// <para>
    /// Because the result is not cloned, this overload avoids allocation at the evaluation
    /// boundary. The caller is responsible for ensuring the workspace outlives any use of
    /// the returned element.
    /// </para>
    /// </remarks>
    public JsonElement Evaluate(string expression, JsonElement data, JsonWorkspace workspace, IReadOnlyDictionary<string, JsonataBinding>? bindings, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        var compiled = this.GetOrCompile(expression);

        var env = Environment.RentRoot();
        try
        {
            env.RootInput = data;
            env.MaxDepth = maxDepth;
            env.Workspace = workspace;

            if (timeLimitMs > 0)
            {
                env.TimeLimitMs = timeLimitMs;
                env.StartTimer();
            }

            ApplyBindings(env, bindings, workspace);

            var result = compiled(data, env);

            if (result.IsUndefined)
            {
                return default;
            }

            JsonElement resultElement = result.IsSingleton
                ? result.FirstOrDefault
                : JsonataHelpers.ArrayFromSequence(result, workspace);

            // All elements consumed — return the backing array to the pool.
            result.ReturnBackingArray();

            if (resultElement.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            return resultElement;
        }
        finally
        {
            Environment.ReturnRoot(env);
        }
    }

    private static void ApplyBindings(Environment env, IReadOnlyDictionary<string, JsonataBinding>? bindings, JsonWorkspace workspace)
    {
        if (bindings is null)
        {
            return;
        }

        foreach (var kvp in bindings)
        {
            switch (kvp.Value.Kind)
            {
                case JsonataBinding.BindingKind.SequenceFunction:
                    ApplySequenceFunction(env, kvp.Key, kvp.Value, workspace);
                    break;

                case JsonataBinding.BindingKind.ElementFunction:
                    ApplyElementFunction(env, kvp.Key, kvp.Value, workspace);
                    break;

                case JsonataBinding.BindingKind.DoubleValue:
                    env.Bind(kvp.Key, Sequence.FromDouble(kvp.Value.GetDoubleValue(), workspace));
                    break;

                case JsonataBinding.BindingKind.StringValue:
                    env.Bind(kvp.Key, Sequence.FromString(kvp.Value.GetStringValue(), workspace));
                    break;

                case JsonataBinding.BindingKind.BoolValue:
                    env.Bind(kvp.Key, Sequence.FromBool(kvp.Value.GetBoolValue()));
                    break;

                default: // ElementValue
                    env.Bind(kvp.Key, new Sequence(kvp.Value.GetElementValue()));
                    break;
            }
        }
    }

    private static void ApplySequenceFunction(
        Environment env,
        string name,
        JsonataBinding binding,
        JsonWorkspace workspace)
    {
        var func = binding.GetSequenceFunction();
        int paramCount = binding.GetParameterCount();
        string? signature = binding.GetSignature();

        var lambda = new LambdaValue(
            (Sequence[] args, JsonElement input, Environment callEnv) =>
            {
                if (signature is not null)
                {
                    SignatureValidator.ValidateArgs(signature, args, -1);
                }

                return func(args, workspace);
            },
            paramCount);

        env.Bind(name, new Sequence(lambda));
    }

    private static void ApplyElementFunction(
        Environment env,
        string name,
        JsonataBinding binding,
        JsonWorkspace workspace)
    {
        var func = binding.GetElementFunction();
        int paramCount = binding.GetParameterCount();
        string? signature = binding.GetSignature();

        var lambda = new LambdaValue(
            (Sequence[] args, JsonElement input, Environment callEnv) =>
            {
                // Convert Sequence args to JsonElement[]
                JsonElement[] jsonArgs = new JsonElement[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    jsonArgs[i] = args[i].IsSingleton
                        ? args[i].FirstOrDefault
                        : args[i].IsUndefined
                            ? default
                            : JsonataHelpers.ArrayFromSequence(args[i], workspace);
                }

                if (signature is not null)
                {
                    SignatureValidator.ValidateArgs(signature, args, -1);
                }

                JsonElement result = func(jsonArgs, workspace);
                return new Sequence(result);
            },
            paramCount);

        env.Bind(name, new Sequence(lambda));
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
// <copyright file="JsonataEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text;

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
    /// Evaluates a JSONata expression and writes the JSON result as UTF-8 bytes
    /// into the caller-provided buffer.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <param name="utf8Destination">The buffer to write the UTF-8 JSON result into.</param>
    /// <param name="bytesWritten">
    /// When this method returns <see langword="true"/>, the number of bytes written.
    /// Zero when the expression produces an undefined result.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>
    /// <see langword="true"/> if the result was successfully written to the buffer;
    /// <see langword="false"/> if the buffer was too small.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method evaluates the expression, serializes the result to compact JSON, and copies
    /// the bytes into <paramref name="utf8Destination"/>. If the expression produces an undefined
    /// result, <paramref name="bytesWritten"/> is set to zero and the method returns <see langword="true"/>
    /// (a valid JSON value always produces at least one byte, so zero length unambiguously indicates undefined).
    /// </para>
    /// <para>
    /// The return value indicates only whether the final write to the destination buffer
    /// succeeded. The evaluation itself may throw exceptions (e.g. <see cref="JsonataException"/>
    /// for expression errors, <see cref="InvalidOperationException"/> for disposed documents).
    /// This is not a <c>TryEvaluateToString</c> method — it is not inexpensive when it
    /// &quot;fails&quot;, because the full evaluation runs before the buffer-size check.
    /// </para>
    /// <para>
    /// The output is produced via <see cref="JsonElement.WriteTo(Utf8JsonWriter)"/>,
    /// which may normalize whitespace, property ordering within individual values, and escape
    /// sequences compared to the original source JSON.
    /// </para>
    /// </remarks>
    public bool EvaluateToString(
        string expression,
        JsonElement data,
        JsonWorkspace workspace,
        Span<byte> utf8Destination,
        out int bytesWritten,
        int maxDepth = Environment.DefaultMaxDepth,
        int timeLimitMs = 0)
    {
        JsonElement result = this.Evaluate(expression, data, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, maxDepth, timeLimitMs);
        return TrySerializeToUtf8(result, workspace, utf8Destination, out bytesWritten);
    }

    /// <summary>
    /// Evaluates a JSONata expression and writes the JSON result as UTF-16 characters
    /// into the caller-provided buffer.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <param name="destination">The buffer to write the UTF-16 JSON result into.</param>
    /// <param name="charsWritten">
    /// When this method returns <see langword="true"/>, the number of characters written.
    /// Zero when the expression produces an undefined result.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>
    /// <see langword="true"/> if the result was successfully written to the buffer;
    /// <see langword="false"/> if the buffer was too small.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method evaluates the expression, serializes the result to compact JSON as UTF-8,
    /// then transcodes to UTF-16 into <paramref name="destination"/>. If the expression produces
    /// an undefined result, <paramref name="charsWritten"/> is set to zero and the method returns
    /// <see langword="true"/>.
    /// </para>
    /// <para>
    /// The return value indicates only whether the final write to the destination buffer
    /// succeeded. The evaluation itself may throw exceptions (e.g. <see cref="JsonataException"/>
    /// for expression errors, <see cref="InvalidOperationException"/> for disposed documents).
    /// This is not a <c>TryEvaluateToString</c> method — it is not inexpensive when it
    /// &quot;fails&quot;, because the full evaluation runs before the buffer-size check.
    /// </para>
    /// <para>
    /// The output is produced via <see cref="JsonElement.WriteTo(Utf8JsonWriter)"/>,
    /// which may normalize whitespace, property ordering within individual values, and escape
    /// sequences compared to the original source JSON.
    /// </para>
    /// </remarks>
    public bool EvaluateToString(
        string expression,
        JsonElement data,
        JsonWorkspace workspace,
        Span<char> destination,
        out int charsWritten,
        int maxDepth = Environment.DefaultMaxDepth,
        int timeLimitMs = 0)
    {
        JsonElement result = this.Evaluate(expression, data, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, maxDepth, timeLimitMs);
        return TrySerializeToUtf16(result, workspace, destination, out charsWritten);
    }

    /// <summary>
    /// Evaluates a JSONata expression provided as UTF-8 bytes and writes the JSON result
    /// as UTF-8 bytes into the caller-provided buffer.
    /// </summary>
    /// <param name="utf8Expression">The JSONata expression as UTF-8 bytes.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <param name="utf8Destination">The buffer to write the UTF-8 JSON result into.</param>
    /// <param name="bytesWritten">
    /// When this method returns <see langword="true"/>, the number of bytes written.
    /// Zero when the expression produces an undefined result.
    /// </param>
    /// <param name="cacheKey">
    /// Optional cache key for the compiled expression. When non-null, the compiled expression
    /// is cached (and retrieved on subsequent calls) using this key.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>
    /// <see langword="true"/> if the result was successfully written to the buffer;
    /// <see langword="false"/> if the buffer was too small.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method evaluates the expression, serializes the result to compact JSON, and copies
    /// the bytes into <paramref name="utf8Destination"/>. If the expression produces an undefined
    /// result, <paramref name="bytesWritten"/> is set to zero and the method returns <see langword="true"/>
    /// (a valid JSON value always produces at least one byte, so zero length unambiguously indicates undefined).
    /// </para>
    /// <para>
    /// The return value indicates only whether the final write to the destination buffer
    /// succeeded. The evaluation itself may throw exceptions (e.g. <see cref="JsonataException"/>
    /// for expression errors, <see cref="InvalidOperationException"/> for disposed documents).
    /// This is not a <c>TryEvaluateToString</c> method — it is not inexpensive when it
    /// &quot;fails&quot;, because the full evaluation runs before the buffer-size check.
    /// </para>
    /// <para>
    /// The output is produced via <see cref="JsonElement.WriteTo(Utf8JsonWriter)"/>,
    /// which may normalize whitespace, property ordering within individual values, and escape
    /// sequences compared to the original source JSON.
    /// </para>
    /// </remarks>
    public bool EvaluateToString(
        byte[] utf8Expression,
        JsonElement data,
        JsonWorkspace workspace,
        Span<byte> utf8Destination,
        out int bytesWritten,
        string? cacheKey = null,
        int maxDepth = Environment.DefaultMaxDepth,
        int timeLimitMs = 0)
    {
        JsonElement result = this.Evaluate(utf8Expression, data, workspace, cacheKey, maxDepth, timeLimitMs);
        return TrySerializeToUtf8(result, workspace, utf8Destination, out bytesWritten);
    }

    /// <summary>
    /// Evaluates a JSONata expression provided as UTF-8 bytes and writes the JSON result
    /// as UTF-16 characters into the caller-provided buffer.
    /// </summary>
    /// <param name="utf8Expression">The JSONata expression as UTF-8 bytes.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <param name="destination">The buffer to write the UTF-16 JSON result into.</param>
    /// <param name="charsWritten">
    /// When this method returns <see langword="true"/>, the number of characters written.
    /// Zero when the expression produces an undefined result.
    /// </param>
    /// <param name="cacheKey">
    /// Optional cache key for the compiled expression. When non-null, the compiled expression
    /// is cached (and retrieved on subsequent calls) using this key.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>
    /// <see langword="true"/> if the result was successfully written to the buffer;
    /// <see langword="false"/> if the buffer was too small.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method evaluates the expression, serializes the result to compact JSON as UTF-8,
    /// then transcodes to UTF-16 into <paramref name="destination"/>. If the expression produces
    /// an undefined result, <paramref name="charsWritten"/> is set to zero and the method returns
    /// <see langword="true"/>.
    /// </para>
    /// <para>
    /// The return value indicates only whether the final write to the destination buffer
    /// succeeded. The evaluation itself may throw exceptions (e.g. <see cref="JsonataException"/>
    /// for expression errors, <see cref="InvalidOperationException"/> for disposed documents).
    /// This is not a <c>TryEvaluateToString</c> method — it is not inexpensive when it
    /// &quot;fails&quot;, because the full evaluation runs before the buffer-size check.
    /// </para>
    /// <para>
    /// The output is produced via <see cref="JsonElement.WriteTo(Utf8JsonWriter)"/>,
    /// which may normalize whitespace, property ordering within individual values, and escape
    /// sequences compared to the original source JSON.
    /// </para>
    /// </remarks>
    public bool EvaluateToString(
        byte[] utf8Expression,
        JsonElement data,
        JsonWorkspace workspace,
        Span<char> destination,
        out int charsWritten,
        string? cacheKey = null,
        int maxDepth = Environment.DefaultMaxDepth,
        int timeLimitMs = 0)
    {
        JsonElement result = this.Evaluate(utf8Expression, data, workspace, cacheKey, maxDepth, timeLimitMs);
        return TrySerializeToUtf16(result, workspace, destination, out charsWritten);
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
            (ReadOnlySpan<Sequence> args, in JsonElement input, Environment callEnv) =>
            {
                if (args.Length < paramCount)
                {
                    throw new JsonataException(
                        "T0410",
                        string.Format(SR.T0410_BindingFunctionExpectsNArguments, name, paramCount, args.Length),
                        0);
                }

                // Slice to paramCount — extra args from HOFs are silently ignored
                // (matches JSONata convention for user-defined functions).
                ReadOnlySpan<Sequence> effective = args.Length > paramCount
                    ? args.Slice(0, paramCount)
                    : args;

                if (signature is not null)
                {
                    SignatureValidator.ValidateArgs(signature, effective, -1);
                }

                return func(effective, workspace);
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
            (ReadOnlySpan<Sequence> args, in JsonElement input, Environment callEnv) =>
            {
                if (args.Length < paramCount)
                {
                    throw new JsonataException(
                        "T0410",
                        string.Format(SR.T0410_BindingFunctionExpectsNArguments, name, paramCount, args.Length),
                        0);
                }

                // Slice to paramCount — extra args from HOFs are silently ignored.
                ReadOnlySpan<Sequence> effective = args.Length > paramCount
                    ? args.Slice(0, paramCount)
                    : args;

                // Convert Sequence args to JsonElement[]
                JsonElement[] jsonArgs = new JsonElement[effective.Length];
                for (int i = 0; i < effective.Length; i++)
                {
                    jsonArgs[i] = effective[i].IsSingleton
                        ? effective[i].FirstOrDefault
                        : effective[i].IsUndefined
                            ? default
                            : JsonataHelpers.ArrayFromSequence(effective[i], workspace);
                }

                if (signature is not null)
                {
                    SignatureValidator.ValidateArgs(signature, effective, -1);
                }

                JsonElement result = func(jsonArgs, workspace);
                return new Sequence(result);
            },
            paramCount);

        env.Bind(name, new Sequence(lambda));
    }

    /// <summary>
    /// Clears the compiled expression cache, freeing memory used by cached delegate trees.
    /// </summary>
    /// <remarks>
    /// Subsequent evaluations will recompile their expressions on first use.
    /// </remarks>
    public void ClearCache()
    {
        this.cache.Clear();
    }

    /// <summary>
    /// Evaluates a JSONata expression provided as pre-encoded UTF-8 bytes.
    /// This overload bypasses the string-to-UTF-8 transcode. When a <paramref name="cacheKey"/>
    /// is provided, the compiled expression is cached under that key (same cache as the string
    /// overloads). When <paramref name="cacheKey"/> is <c>null</c>, the expression is compiled
    /// fresh each invocation — ideal for cold-start scenarios where each expression is evaluated once.
    /// </summary>
    /// <param name="utf8Expression">The JSONata expression as UTF-8 bytes.</param>
    /// <param name="data">The input JSON data element.</param>
    /// <param name="workspace">
    /// The workspace for intermediate document allocation. The returned <see cref="JsonElement"/>
    /// may reference documents owned by this workspace, so it remains valid only while the
    /// workspace is alive and has not been reset.
    /// </param>
    /// <param name="cacheKey">
    /// Optional cache key. When non-null, the compiled expression is stored (and retrieved on
    /// subsequent calls) using this key. The caller is responsible for ensuring the key uniquely
    /// identifies the expression.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth (default 500).</param>
    /// <param name="timeLimitMs">Maximum evaluation time in milliseconds (0 = no limit).</param>
    /// <returns>The evaluation result as a <see cref="JsonElement"/>, or <c>default</c> if the result is undefined.</returns>
    public JsonElement Evaluate(byte[] utf8Expression, JsonElement data, JsonWorkspace workspace, string? cacheKey = null, int maxDepth = Environment.DefaultMaxDepth, int timeLimitMs = 0)
    {
        ExpressionEvaluator compiled;
        if (cacheKey is not null)
        {
            compiled = this.GetOrCompile(cacheKey, utf8Expression);
        }
        else
        {
            var ast = Parser.Parse(utf8Expression);
            compiled = FunctionalCompiler.Compile(ast);
        }

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

            var result = compiled(data, env);

            if (result.IsUndefined)
            {
                return default;
            }

            JsonElement resultElement = result.IsSingleton
                ? result.FirstOrDefault
                : JsonataHelpers.ArrayFromSequence(result, workspace);

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

    private static bool TrySerializeToUtf8(JsonElement result, JsonWorkspace workspace, Span<byte> utf8Destination, out int bytesWritten)
    {
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            bytesWritten = 0;
            return true;
        }

        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter bufferWriter);
        try
        {
            result.WriteTo(writer);
            writer.Flush();

            ReadOnlySpan<byte> written = bufferWriter.WrittenSpan;
            if (written.Length <= utf8Destination.Length)
            {
                written.CopyTo(utf8Destination);
                bytesWritten = written.Length;
                return true;
            }

            bytesWritten = 0;
            return false;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    private static bool TrySerializeToUtf16(JsonElement result, JsonWorkspace workspace, Span<char> destination, out int charsWritten)
    {
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            charsWritten = 0;
            return true;
        }

        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter bufferWriter);
        try
        {
            result.WriteTo(writer);
            writer.Flush();

            ReadOnlySpan<byte> utf8 = bufferWriter.WrittenSpan;

#if NETSTANDARD2_0
            // netstandard2.0 lacks Encoding.GetCharCount(ReadOnlySpan<byte>).
            byte[] utf8Array = utf8.ToArray();
            int needed = Encoding.UTF8.GetCharCount(utf8Array, 0, utf8Array.Length);
            if (needed > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            char[] charArray = new char[needed];
            Encoding.UTF8.GetChars(utf8Array, 0, utf8Array.Length, charArray, 0);
            charArray.AsSpan(0, needed).CopyTo(destination);
            charsWritten = needed;
            return true;
#else
            int needed = Encoding.UTF8.GetCharCount(utf8);
            if (needed > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = Encoding.UTF8.GetChars(utf8, destination);
            return true;
#endif
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
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

    private ExpressionEvaluator GetOrCompile(string cacheKey, byte[] utf8Expression)
    {
        if (this.cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var ast = Parser.Parse(utf8Expression);
        var compiled = FunctionalCompiler.Compile(ast);

        this.cache.TryAdd(cacheKey, compiled);
        return compiled;
    }
}
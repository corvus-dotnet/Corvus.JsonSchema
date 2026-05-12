// <copyright file="Environment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A variable binding environment forming a scope chain. Each scope level
/// holds variable bindings and a reference to the enclosing (parent) scope.
/// The root scope contains built-in function bindings.
/// </summary>
internal sealed class Environment
{
    /// <summary>
    /// The prefix used for lambda sentinel values stored in JSON objects.
    /// </summary>
    internal const string LambdaSentinelPrefix = "__fn:";

    /// <summary>
    /// The default maximum call depth for recursive function evaluation.
    /// </summary>
    public const int DefaultMaxDepth = 500;

    [ThreadStatic]
    private static Environment? t_cachedRoot;

    [ThreadStatic]
    private static Environment? t_cachedChild;

    private Dictionary<string, Sequence>? bindings;
    private Environment? parent;
    private Environment root;
    private bool isCaptured;
    private int currentDepth;
    private int maxDepth;
    private int evalCount;
    private long startTicks;
    private long timeLimitTicks;
    private Dictionary<int, LambdaValue>? lambdaMap;
    private int lambdaCounter;

    // Side-channel for CVB-based object construction: when an ObjectBuilder callback
    // encounters a lambda value, it stores it here. The caller reads and clears after build.
    internal Dictionary<string, LambdaValue>? TempObjectLambdas;

    /// <summary>
    /// Initializes a new instance of the <see cref="Environment"/> class.
    /// </summary>
    /// <param name="parent">The enclosing scope, or <c>null</c> for the root.</param>
    public Environment(Environment? parent = null)
    {
        this.parent = parent;
        this.root = parent?.root ?? this;
        this.maxDepth = DefaultMaxDepth;
    }

    /// <summary>
    /// Rents a root <see cref="Environment"/> from the thread-local cache,
    /// or allocates a new one if the cache is empty.
    /// </summary>
    /// <returns>A root environment ready for use.</returns>
    public static Environment RentRoot()
    {
        var env = t_cachedRoot;
        if (env is not null)
        {
            t_cachedRoot = null;
            return env;
        }

        return new Environment();
    }

    /// <summary>
    /// Returns a root <see cref="Environment"/> to the thread-local cache.
    /// Resets all mutable state so the instance can be reused.
    /// </summary>
    /// <param name="env">The root environment to return.</param>
    public static void ReturnRoot(Environment env)
    {
        Debug.Assert(env.parent is null, "Only root environments should be returned to the cache.");
        env.bindings?.Clear();
        env.currentDepth = 0;
        env.evalCount = 0;
        env.startTicks = 0;
        env.timeLimitTicks = 0;
        env.lambdaMap?.Clear();
        env.lambdaCounter = 0;
        env.TempObjectLambdas = null;
        env.RootInput = default;
        env.WorkspaceDirect = default!;
        t_cachedRoot = env;
    }

    /// <summary>
    /// Rents a child <see cref="Environment"/> from the thread-local cache,
    /// or allocates a new one if the cache is empty.
    /// </summary>
    /// <param name="parent">The parent environment for scope chaining.</param>
    /// <returns>A child environment ready for use.</returns>
    public static Environment RentChild(Environment parent)
    {
        var child = t_cachedChild;
        if (child is not null)
        {
            t_cachedChild = null;
            child.parent = parent;
            child.root = parent.root ?? parent;
            child.RootInput = parent.GetRootInput();
            return child;
        }

        return new Environment(parent)
        {
            RootInput = parent.GetRootInput(),
        };
    }

    /// <summary>
    /// Returns a child <see cref="Environment"/> to the thread-local cache.
    /// If the environment was captured by a lambda closure, it is not pooled.
    /// </summary>
    /// <param name="child">The child environment to return.</param>
    public static void ReturnChild(Environment child)
    {
        if (child.isCaptured)
        {
            return;
        }

        child.bindings?.Clear();
        child.RootInput = default;
        t_cachedChild = child;
    }

    /// <summary>
    /// Gets a value indicating whether this environment has been captured
    /// by a lambda closure. A captured environment must not be reused
    /// by <see cref="LambdaValue.InvokeReusing"/>.
    /// </summary>
    internal bool IsCaptured => this.isCaptured;

    /// <summary>
    /// Marks this environment as captured by a lambda closure.
    /// A captured environment will not be returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkCaptured()
    {
        this.isCaptured = true;
    }

    /// <summary>
    /// Gets or sets the root input data element (<c>$$</c>).
    /// Stored on the root environment.
    /// </summary>
    public JsonElement RootInput { get; set; }

    /// <summary>
    /// Gets the <see cref="JsonWorkspace"/> for this evaluation, providing
    /// pooled memory for intermediate value creation.
    /// Stored on the root environment; child scopes delegate to the root.
    /// </summary>
    public JsonWorkspace Workspace
    {
        get => this.root.WorkspaceDirect;
        set => this.root.WorkspaceDirect = value;
    }

    /// <summary>
    /// Gets or sets the maximum call depth for this evaluation.
    /// Only meaningful on the root environment.
    /// </summary>
    public int MaxDepth
    {
        get => this.root.maxDepth;
        set => this.root.maxDepth = value;
    }

    /// <summary>
    /// Gets or sets the time limit in milliseconds for this evaluation.
    /// Zero means no time limit. Only meaningful on the root environment.
    /// </summary>
    public int TimeLimitMs
    {
        get => (int)(this.root.timeLimitTicks * 1000 / Stopwatch.Frequency);
        set => this.root.timeLimitTicks = value > 0 ? (long)value * Stopwatch.Frequency / 1000 : 0;
    }

    // Direct field-backed property for the root environment only.
    private JsonWorkspace WorkspaceDirect { get; set; } = default!;

    /// <summary>
    /// Looks up a variable by name, walking up the scope chain.
    /// </summary>
    /// <param name="name">The variable name (without <c>$</c> prefix).</param>
    /// <param name="value">The resolved value, or <see cref="Sequence.Undefined"/> if not found.</param>
    /// <returns><c>true</c> if the variable was found; otherwise <c>false</c>.</returns>
    public bool TryLookup(string name, out Sequence value)
    {
        if (this.bindings is not null && this.bindings.TryGetValue(name, out value))
        {
            return true;
        }

        if (this.parent is not null)
        {
            return this.parent.TryLookup(name, out value);
        }

        value = Sequence.Undefined;
        return false;
    }

    /// <summary>
    /// Binds a variable in this scope.
    /// </summary>
    /// <param name="name">The variable name (without <c>$</c> prefix).</param>
    /// <param name="value">The value to bind. If this is an owned multi-value
    /// sequence with a pooled backing array, the environment takes ownership and
    /// will return the array when the scope is cleaned up.</param>
    public void Bind(string name, Sequence value)
    {
        this.bindings ??= new();
        this.bindings[name] = value;
    }

    /// <summary>
    /// Creates a child scope inheriting from this environment.
    /// </summary>
    public Environment CreateChild()
    {
        return new Environment(this)
        {
            RootInput = this.GetRootInput(),
        };
    }

    /// <summary>
    /// Increments the evaluation depth and throws if the limit is exceeded.
    /// Also periodically checks the time limit.
    /// Called by the depth-tracking wrapper around every compiled expression.
    /// </summary>
    /// <exception cref="JsonataException">Thrown with code <c>U1001</c> when the depth or time limit is exceeded.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterEval()
    {
        var r = this.root;
        if (++r.currentDepth > r.maxDepth)
        {
            ThrowDepthExceeded();
        }

        // Check timeout every 1024 evaluations to amortize clock reads
        if (r.timeLimitTicks > 0 && (++r.evalCount & 0x3FF) == 0)
        {
            CheckTimeout(r);
        }
    }

    /// <summary>
    /// Decrements the evaluation depth after an expression evaluation completes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LeaveEval()
    {
        this.root.currentDepth--;
    }

    /// <summary>
    /// Starts the timer for time-limited evaluation. Call this once
    /// before beginning expression evaluation.
    /// </summary>
    public void StartTimer()
    {
        this.root.startTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Registers a lambda value in the root environment's lambda map and returns a unique ID.
    /// Used to preserve function references stored in JSON objects (where they would otherwise be lost).
    /// </summary>
    /// <param name="lambda">The lambda to register.</param>
    /// <returns>A unique integer ID that can be used to retrieve the lambda later.</returns>
    public int RegisterLambda(LambdaValue lambda)
    {
        var r = this.root;
        r.lambdaMap ??= new Dictionary<int, LambdaValue>();
        int id = r.lambdaCounter++;
        r.lambdaMap[id] = lambda;
        return id;
    }

    /// <summary>
    /// Attempts to retrieve a previously registered lambda by its ID.
    /// </summary>
    /// <param name="id">The lambda ID returned by <see cref="RegisterLambda"/>.</param>
    /// <param name="lambda">The retrieved lambda, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if the lambda was found; otherwise <c>false</c>.</returns>
    public bool TryGetLambda(int id, out LambdaValue? lambda)
    {
        var r = this.root;
        if (r.lambdaMap is not null && r.lambdaMap.TryGetValue(id, out lambda))
        {
            return true;
        }

        lambda = null;
        return false;
    }

    /// <summary>
    /// Checks if a JSON element is a lambda sentinel string and retrieves the associated lambda.
    /// </summary>
    /// <param name="element">The JSON element to check.</param>
    /// <param name="lambda">The lambda if the element is a sentinel; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if the element was a lambda sentinel with a valid registration.</returns>
    public bool TryGetStoredLambda(in JsonElement element, out LambdaValue? lambda)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string? s = element.GetString();
            if (s?.StartsWith(LambdaSentinelPrefix, StringComparison.Ordinal) == true
                && int.TryParse(s.Substring(LambdaSentinelPrefix.Length), out int id))
            {
                return this.TryGetLambda(id, out lambda);
            }
        }

        lambda = null;
        return false;
    }

    /// <summary>
    /// Gets the root input, walking up to the root if needed.
    /// </summary>
    public JsonElement GetRootInput()
    {
        return this.root.RootInput;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDepthExceeded()
    {
        throw new JsonataException(
            "U1001",
            SR.U1001_StackOverflowError,
            -1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckTimeout(Environment r)
    {
        long elapsed = Stopwatch.GetTimestamp() - r.startTicks;
        if (elapsed > r.timeLimitTicks)
        {
            throw new JsonataException("U1001", SR.U1001_ExpressionEvaluationTimeoutCheckForInfiniteLoop, -1);
        }
    }
}
// <copyright file="Environment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

    private readonly Dictionary<string, Sequence> bindings = new();
    private readonly Environment? parent;
    private int currentDepth;
    private int maxDepth;
    private Dictionary<int, LambdaValue>? lambdaMap;
    private int lambdaCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="Environment"/> class.
    /// </summary>
    /// <param name="parent">The enclosing scope, or <c>null</c> for the root.</param>
    public Environment(Environment? parent = null)
    {
        this.parent = parent;
        this.maxDepth = DefaultMaxDepth;
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
        get => this.GetRoot().WorkspaceDirect;
        set => this.GetRoot().WorkspaceDirect = value;
    }

    /// <summary>
    /// Gets or sets the maximum call depth for this evaluation.
    /// Only meaningful on the root environment.
    /// </summary>
    public int MaxDepth
    {
        get => this.GetRoot().maxDepth;
        set => this.GetRoot().maxDepth = value;
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
        if (this.bindings.TryGetValue(name, out value))
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
    /// <param name="value">The value to bind.</param>
    public void Bind(string name, Sequence value)
    {
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
    /// Increments the call depth and throws if the limit is exceeded.
    /// </summary>
    /// <exception cref="JsonataException">Thrown with code <c>U1001</c> when the depth limit is exceeded.</exception>
    public void EnterCall()
    {
        var root = this.GetRoot();
        if (++root.currentDepth > root.maxDepth)
        {
            throw new JsonataException("U1001", "Stack overflow error: call depth exceeded", -1);
        }
    }

    /// <summary>
    /// Decrements the call depth after a function call completes.
    /// </summary>
    public void LeaveCall()
    {
        this.GetRoot().currentDepth--;
    }

    /// <summary>
    /// Registers a lambda value in the root environment's lambda map and returns a unique ID.
    /// Used to preserve function references stored in JSON objects (where they would otherwise be lost).
    /// </summary>
    /// <param name="lambda">The lambda to register.</param>
    /// <returns>A unique integer ID that can be used to retrieve the lambda later.</returns>
    public int RegisterLambda(LambdaValue lambda)
    {
        var root = this.GetRoot();
        root.lambdaMap ??= new Dictionary<int, LambdaValue>();
        int id = root.lambdaCounter++;
        root.lambdaMap[id] = lambda;
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
        var root = this.GetRoot();
        if (root.lambdaMap is not null && root.lambdaMap.TryGetValue(id, out lambda))
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
        if (this.parent is null)
        {
            return this.RootInput;
        }

        return this.parent.GetRootInput();
    }

    private Environment GetRoot()
    {
        var current = this;
        while (current.parent is not null)
        {
            current = current.parent;
        }

        return current;
    }
}
// <copyright file="Environment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A variable binding environment forming a scope chain. Each scope level
/// holds variable bindings and a reference to the enclosing (parent) scope.
/// The root scope contains built-in function bindings.
/// </summary>
internal sealed class Environment
{
    private readonly Dictionary<string, Sequence> bindings = new();
    private readonly Environment? parent;

    /// <summary>
    /// Initializes a new instance of the <see cref="Environment"/> class.
    /// </summary>
    /// <param name="parent">The enclosing scope, or <c>null</c> for the root.</param>
    public Environment(Environment? parent = null)
    {
        this.parent = parent;
    }

    /// <summary>
    /// Gets or sets the root input data element (<c>$$</c>).
    /// Stored on the root environment.
    /// </summary>
    public JsonElement RootInput { get; set; }

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
}
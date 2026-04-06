// <copyright file="LambdaValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Represents a first-class function value in JSONata (a lambda or built-in function reference).
/// Lambda values capture their defining environment for closure semantics.
/// </summary>
internal sealed class LambdaValue
{
    private readonly ExpressionEvaluator body;
    private readonly string[] paramNames;
    private readonly Environment? definingEnv;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaValue"/> class for a user-defined lambda.
    /// </summary>
    /// <param name="body">The compiled body expression.</param>
    /// <param name="paramNames">The parameter names (without <c>$</c> prefix).</param>
    /// <param name="definingEnv">The environment where the lambda was defined (for closures).</param>
    public LambdaValue(ExpressionEvaluator body, string[] paramNames, Environment definingEnv)
    {
        this.body = body;
        this.paramNames = paramNames;
        this.definingEnv = definingEnv;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaValue"/> class for a built-in function wrapper.
    /// </summary>
    /// <param name="nativeFunc">A native function that takes evaluated arguments.</param>
    /// <param name="paramCount">The expected parameter count (for display/arity checking).</param>
    public LambdaValue(Func<Sequence[], JsonElement, Environment, Sequence> nativeFunc, int paramCount)
    {
        this.NativeFunc = nativeFunc;
        this.paramNames = new string[paramCount];
        this.body = null!;
    }

    /// <summary>
    /// Gets the native function implementation, if this is a built-in wrapper.
    /// </summary>
    public Func<Sequence[], JsonElement, Environment, Sequence>? NativeFunc { get; }

    /// <summary>
    /// Gets the parameter names.
    /// </summary>
    public string[] ParamNames => this.paramNames;

    /// <summary>
    /// Gets the arity (number of parameters).
    /// </summary>
    public int Arity => this.paramNames.Length;

    /// <summary>
    /// Invokes the lambda with the given arguments.
    /// </summary>
    /// <param name="args">The evaluated argument sequences.</param>
    /// <param name="input">The current context element.</param>
    /// <param name="callerEnv">The caller's environment.</param>
    /// <returns>The result of invoking the lambda body.</returns>
    public Sequence Invoke(Sequence[] args, in JsonElement input, Environment callerEnv)
    {
        if (this.NativeFunc is not null)
        {
            return this.NativeFunc(args, input, callerEnv);
        }

        // User-defined lambda: create a child of the DEFINING env (closure semantics)
        var invokeEnv = (this.definingEnv ?? callerEnv).CreateChild();

        // Bind parameters
        for (int i = 0; i < this.paramNames.Length; i++)
        {
            invokeEnv.Bind(
                this.paramNames[i],
                i < args.Length ? args[i] : Sequence.Undefined);
        }

        // Track call depth to prevent stack overflow
        invokeEnv.EnterCall();
        try
        {
            return this.body(input, invokeEnv);
        }
        finally
        {
            invokeEnv.LeaveCall();
        }
    }
}
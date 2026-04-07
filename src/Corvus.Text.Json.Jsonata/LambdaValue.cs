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
    private readonly JsonElement definingInput;
    private readonly int contextArgCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaValue"/> class for a user-defined lambda.
    /// </summary>
    /// <param name="body">The compiled body expression.</param>
    /// <param name="paramNames">The parameter names (without <c>$</c> prefix).</param>
    /// <param name="definingEnv">The environment where the lambda was defined (for closures).</param>
    /// <param name="definingInput">The input context (<c>$</c>) at the point where the lambda was defined.</param>
    /// <param name="isThunk">Whether this is a thunk for tail-call optimization.</param>
    /// <param name="contextArgCount">Number of context-bound parameters from a function signature (before the <c>-</c> separator).</param>
    public LambdaValue(ExpressionEvaluator body, string[] paramNames, Environment definingEnv, in JsonElement definingInput, bool isThunk = false, int contextArgCount = 0)
    {
        this.body = body;
        this.paramNames = paramNames;
        this.definingEnv = definingEnv;
        this.definingInput = definingInput;
        this.IsThunk = isThunk;
        this.contextArgCount = contextArgCount;
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
    /// Gets a value indicating whether this lambda is a thunk for tail-call optimization.
    /// </summary>
    public bool IsThunk { get; }

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

        // If the lambda has context parameters (from a function signature with '-'),
        // and the caller provided fewer args than params, prepend the input as context args.
        Sequence[] effectiveArgs = args;
        if (this.contextArgCount > 0 && args.Length < this.paramNames.Length)
        {
            int missingCount = this.paramNames.Length - args.Length;
            int contextToInsert = Math.Min(this.contextArgCount, missingCount);
            effectiveArgs = new Sequence[args.Length + contextToInsert];
            for (int i = 0; i < contextToInsert; i++)
            {
                effectiveArgs[i] = new Sequence(input);
            }

            Array.Copy(args, 0, effectiveArgs, contextToInsert, args.Length);
        }

        // Bind parameters
        for (int i = 0; i < this.paramNames.Length; i++)
        {
            invokeEnv.Bind(
                this.paramNames[i],
                i < effectiveArgs.Length ? effectiveArgs[i] : Sequence.Undefined);
        }

        // Track call depth to prevent stack overflow
        invokeEnv.EnterCall();
        try
        {
            // Use the defining input (the context at lambda creation time) so that
            // $ inside the body refers to the closure context, not the call-site context.
            var result = this.body(this.definingInput, invokeEnv);

            // Trampoline: when the body returns a thunk (a tail-call-optimized
            // lambda with no parameters), execute it immediately instead of
            // returning the thunk as a value. This ensures that tail-position
            // function calls produce the actual result rather than a deferred
            // lambda wrapper.
            while (result.IsLambda && result.Lambda!.IsThunk)
            {
                result = result.Lambda!.Invoke([], input, callerEnv);
            }

            return result;
        }
        finally
        {
            invokeEnv.LeaveCall();
        }
    }
}
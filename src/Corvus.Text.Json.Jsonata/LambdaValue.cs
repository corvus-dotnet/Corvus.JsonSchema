// <copyright file="LambdaValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

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
    private readonly int regularArgCount;
    private readonly string? signature;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaValue"/> class for a user-defined lambda.
    /// </summary>
    /// <param name="body">The compiled body expression.</param>
    /// <param name="paramNames">The parameter names (without <c>$</c> prefix).</param>
    /// <param name="definingEnv">The environment where the lambda was defined (for closures).</param>
    /// <param name="definingInput">The input context (<c>$</c>) at the point where the lambda was defined.</param>
    /// <param name="contextArgCount">Number of context-bound parameters from a function signature (before the <c>-</c> separator).</param>
    /// <param name="regularArgCount">Number of regular parameters (after the <c>-</c> separator).</param>
    /// <param name="signature">The raw function signature string, if any.</param>
    public LambdaValue(ExpressionEvaluator body, string[] paramNames, Environment definingEnv, in JsonElement definingInput, int contextArgCount = 0, int regularArgCount = 0, string? signature = null)
    {
        this.body = body;
        this.paramNames = paramNames;
        this.definingEnv = definingEnv;
        definingEnv.MarkCaptured();
        this.definingInput = definingInput;
        this.contextArgCount = contextArgCount;
        this.regularArgCount = regularArgCount;
        this.signature = signature;
    }

    /// <summary>
    /// Represents a native function implementation that receives its arguments
    /// as a <see cref="ReadOnlySpan{T}"/> of <see cref="Sequence"/> values,
    /// sliced to the actual argument count.
    /// </summary>
    internal delegate Sequence NativeFuncDelegate(ReadOnlySpan<Sequence> args, in JsonElement input, Environment callerEnv);

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaValue"/> class for a built-in function wrapper.
    /// </summary>
    /// <param name="nativeFunc">A native function that takes evaluated arguments.</param>
    /// <param name="paramCount">The expected parameter count (for display/arity checking).</param>
    public LambdaValue(NativeFuncDelegate nativeFunc, int paramCount)
    {
        this.NativeFunc = nativeFunc;
        this.paramNames = new string[paramCount];
        this.body = null!;
    }

    /// <summary>
    /// Gets the native function implementation, if this is a built-in wrapper.
    /// </summary>
    public NativeFuncDelegate? NativeFunc { get; }

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
    /// <param name="argsCount">The number of valid arguments in <paramref name="args"/>.</param>
    /// <param name="input">The current context element.</param>
    /// <param name="callerEnv">The caller's environment.</param>
    /// <returns>The result of invoking the lambda body.</returns>
    public Sequence Invoke(Sequence[] args, int argsCount, in JsonElement input, Environment callerEnv)
    {
        var result = this.InvokeBody(args, argsCount, input, callerEnv);

        // Trampoline: when the body returns a tail-call continuation,
        // call the target's InvokeBody directly instead of recursing through Invoke.
        // This keeps the .NET call stack flat for both direct and mutual recursion.
        while (result.IsTailCall)
        {
            var tc = result.TailCall!;
            result = tc.Target.InvokeBody(tc.Args, tc.ArgsCount, tc.Input, tc.CallerEnv);
        }

        return result;
    }

    /// <summary>
    /// Evaluates this lambda's body with the given arguments, without trampolining.
    /// Called by the trampoline in <see cref="Invoke"/> and also for the initial invocation.
    /// </summary>
    /// <param name="args">The evaluated argument sequences.</param>
    /// <param name="argsCount">The number of valid arguments in <paramref name="args"/>.</param>
    /// <param name="input">The current context element.</param>
    /// <param name="callerEnv">The caller's environment.</param>
    /// <returns>
    /// The result of evaluating the body. May be a <see cref="TailCallContinuation"/>
    /// if the body ends with a tail-position function call.
    /// </returns>
    internal Sequence InvokeBody(Sequence[] args, int argsCount, in JsonElement input, Environment callerEnv)
    {
        if (this.NativeFunc is not null)
        {
            return this.NativeFunc(args.AsSpan(0, argsCount), input, callerEnv);
        }

        // User-defined lambda: create a child of the DEFINING env (closure semantics)
        var invokeEnv = (this.definingEnv ?? callerEnv).CreateChild();

        // If the lambda has context parameters (from a function signature with '-'),
        // and the caller provided fewer args than params, fill unfilled params with
        // the input context. Regular params (after '-') are filled first by explicit
        // args from the end, context params (before '-') are filled by remaining args
        // from the start, and any still-unfilled context params get the input.
        Sequence[] effectiveArgs = args;
        Sequence[]? rentedArgs = null;
        if (this.contextArgCount > 0 && argsCount < this.paramNames.Length)
        {
            rentedArgs = ArrayPool<Sequence>.Shared.Rent(this.paramNames.Length);
            effectiveArgs = rentedArgs;

            // Regular params (after '-') are filled from the end of explicit args
            int regularToFill = Math.Min(this.regularArgCount, argsCount);
            int argsForRegular = regularToFill;
            int regularStart = this.paramNames.Length - this.regularArgCount;
            for (int i = 0; i < regularToFill; i++)
            {
                effectiveArgs[regularStart + i] = args[argsCount - argsForRegular + i];
            }

            // Remaining explicit args fill context params from the left
            int argsForContext = argsCount - argsForRegular;
            for (int i = 0; i < argsForContext; i++)
            {
                effectiveArgs[i] = args[i];
            }

            // Unfilled context params get the input
            for (int i = argsForContext; i < regularStart; i++)
            {
                effectiveArgs[i] = new Sequence(input);
            }
        }

        try
        {
            // Bind parameters
            int effectiveCount = rentedArgs is not null ? this.paramNames.Length : argsCount;
            for (int i = 0; i < this.paramNames.Length; i++)
            {
                invokeEnv.Bind(
                    this.paramNames[i],
                    i < effectiveCount ? effectiveArgs[i] : Sequence.Undefined);
            }

            // Validate argument types against the signature, if present
            if (this.signature is not null)
            {
                SignatureValidator.ValidateArgs(this.signature, effectiveArgs.AsSpan(0, effectiveCount), -1);
            }

            return this.body(this.definingInput, invokeEnv);
        }
        finally
        {
            if (rentedArgs is not null)
            {
                rentedArgs.AsSpan(0, this.paramNames.Length).Clear();
                ArrayPool<Sequence>.Shared.Return(rentedArgs);
            }
        }
    }

    /// <summary>
    /// Creates a child environment suitable for reuse across multiple invocations
    /// of this lambda. The environment is a child of the defining environment
    /// (for closure semantics).
    /// </summary>
    /// <param name="callerEnv">Fallback environment if no defining environment exists.</param>
    /// <returns>A child environment to pass to <see cref="InvokeReusing"/>.</returns>
    internal Environment CreateInvokeEnv(Environment callerEnv)
    {
        return Environment.RentChild(this.definingEnv ?? callerEnv);
    }

    /// <summary>
    /// Invokes this lambda reusing a pre-created child environment, avoiding
    /// per-call Environment and Dictionary allocation. The caller must have
    /// obtained <paramref name="reuseEnv"/> from <see cref="CreateInvokeEnv"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is safe for built-in higher-order functions ($map, $filter, $reduce, $sort)
    /// where each iteration's result is consumed immediately and does not capture the
    /// iteration environment in a long-lived closure.
    /// </para>
    /// <para>
    /// For native functions, this delegates to <see cref="Invoke"/> since native
    /// functions do not use the environment for parameter binding.
    /// </para>
    /// </remarks>
    internal Sequence InvokeReusing(ReadOnlySpan<Sequence> args, in JsonElement input, Environment reuseEnv, Environment callerEnv)
    {
        if (this.NativeFunc is not null)
        {
            // Native functions don't use the environment for parameter binding.
            var result = this.NativeFunc(args, input, callerEnv);

            // Trampoline for tail calls
            while (result.IsTailCall)
            {
                var tc = result.TailCall!;
                result = tc.Target.InvokeBody(tc.Args, tc.ArgsCount, tc.Input, tc.CallerEnv);
            }

            return result;
        }

        // Rebind parameters in the reused environment
        for (int i = 0; i < this.paramNames.Length; i++)
        {
            reuseEnv.Bind(
                this.paramNames[i],
                i < args.Length ? args[i] : Sequence.Undefined);
        }

        if (this.signature is not null)
        {
            SignatureValidator.ValidateArgs(this.signature, args, -1);
        }

        var bodyResult = this.body(this.definingInput, reuseEnv);

        // Trampoline for tail calls
        while (bodyResult.IsTailCall)
        {
            var tc = bodyResult.TailCall!;
            bodyResult = tc.Target.InvokeBody(tc.Args, tc.ArgsCount, tc.Input, tc.CallerEnv);
        }

        return bodyResult;
    }
}
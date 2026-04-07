// <copyright file="TailCallContinuation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Represents a pending tail-call that the trampoline can process without
/// growing the .NET call stack. Instead of calling <see cref="LambdaValue.Invoke"/>
/// immediately, a tail-position function call returns this continuation so the
/// caller's trampoline loop can bind parameters and evaluate the target body directly.
/// </summary>
internal sealed class TailCallContinuation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TailCallContinuation"/> class.
    /// </summary>
    /// <param name="target">The function to invoke.</param>
    /// <param name="args">The evaluated argument sequences.</param>
    /// <param name="input">The current input context element.</param>
    /// <param name="callerEnv">The caller's environment.</param>
    public TailCallContinuation(LambdaValue target, Sequence[] args, in JsonElement input, Environment callerEnv)
    {
        this.Target = target;
        this.Args = args;
        this.Input = input;
        this.CallerEnv = callerEnv;
    }

    /// <summary>Gets the target function to invoke.</summary>
    public LambdaValue Target { get; }

    /// <summary>Gets the evaluated arguments.</summary>
    public Sequence[] Args { get; }

    /// <summary>Gets the current input context.</summary>
    public JsonElement Input { get; }

    /// <summary>Gets the caller's environment.</summary>
    public Environment CallerEnv { get; }
}
// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless;

/// <summary>
/// Centralized exception-throwing helpers for the serverless (AOT) function-side durability.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; the helper used where a local assigned via an <c>out</c> parameter must stay definitely assigned is a <c>Get*Exception</c> factory the caller throws (which <see cref="DoesNotReturnAttribute"/> does not satisfy). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when the invocation is missing the required <c>runId</c>.</summary>
    /// <param name="paramName">The offending parameter's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMissingRunId(string paramName)
        => throw new ArgumentException(SR.MissingRunId, paramName);

    /// <summary>Throws when the invocation's <c>runId</c> is outside the run-id grammar (ADR 0065 §9).</summary>
    /// <param name="paramName">The offending parameter's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMalformedRunId(string paramName)
        => throw new ArgumentException(SR.MalformedRunId, paramName);

    /// <summary>Creates the exception for an invocation missing a valid absolute <c>checkpointUrl</c>, for the caller to throw.</summary>
    /// <param name="paramName">The offending parameter's name.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetMissingCheckpointUrlException(string paramName)
        => new(SR.MissingCheckpointUrl, paramName);

    /// <summary>Throws when the terminal checkpoint did not commit to the dispatching runner.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTerminalCheckpointNotCommitted()
        => throw new InvalidOperationException(SR.TerminalCheckpointNotCommitted);
}
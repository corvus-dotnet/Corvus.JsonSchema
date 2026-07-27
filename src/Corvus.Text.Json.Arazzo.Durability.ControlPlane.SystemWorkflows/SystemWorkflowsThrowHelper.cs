// <copyright file="SystemWorkflowsThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;

/// <summary>
/// Centralized exception-throwing helpers for the control-plane system workflows.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from an expression position (a <c>??</c>) or where a pattern-declared local stays definitely assigned after the guard are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class SystemWorkflowsThrowHelper
{
    /// <summary>Creates the exception for a missing embedded system-workflow spec, for the caller to throw.</summary>
    /// <param name="fileName">The spec file name.</param>
    /// <param name="assemblyName">The assembly searched.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetSpecNotFoundException(string fileName, object? assemblyName)
        => new(SR.Format(SR.SpecNotFound, fileName, assemblyName));

    /// <summary>Creates the exception for an embedded system-workflow spec resource that could not be opened, for the caller to throw.</summary>
    /// <param name="resourceName">The manifest resource name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetSpecResourceNotOpenedException(object? resourceName)
        => new(SR.Format(SR.SpecResourceNotOpened, resourceName));

    /// <summary>Throws when a critical system workflow fails to bake at install time.</summary>
    /// <param name="baseWorkflowId">The base workflow id that failed to bake.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSystemWorkflowBakeFailed(object? baseWorkflowId)
        => throw new InvalidOperationException(SR.Format(SR.SystemWorkflowBakeFailed, baseWorkflowId));

    /// <summary>Creates the exception for a broker server URL set without its token reference, for the caller to throw.</summary>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetBrokerTokenRefRequiredException()
        => new(SR.BrokerTokenRefRequired, "options");
}
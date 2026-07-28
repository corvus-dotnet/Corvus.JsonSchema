// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// Centralized exception-throwing helpers for the SQL Server durability backend.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a source with the same name and security tags already exists.</summary>
    /// <param name="name">The conflicting source name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SourceAlreadyExists, name));

    /// <summary>Throws when an environment with the same name and security tags already exists.</summary>
    /// <param name="name">The conflicting environment name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.EnvironmentAlreadyExists, name));

    /// <summary>Throws when a source credential binding with the same source, environment, and security tags already exists.</summary>
    /// <param name="sourceName">The source name the binding is for.</param>
    /// <param name="environment">The environment the binding is for.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceCredentialAlreadyExists(string sourceName, string environment)
        => throw new InvalidOperationException(SR.Format(SR.SourceCredentialAlreadyExists, sourceName, environment));

    /// <summary>Throws when a security rule with the same name already exists.</summary>
    /// <param name="name">The conflicting rule name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityRuleAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SecurityRuleAlreadyExists, name));

    /// <summary>Throws when an environment administration record is written with no administrator identities.</summary>
    /// <param name="paramName">The name of the offending argument.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAdministratorsRequired(string paramName)
        => throw new ArgumentException(SR.EnvironmentAdministratorsRequired, paramName);

    /// <summary>Throws when a workflow administration record is written with no administrator identities.</summary>
    /// <param name="paramName">The name of the offending argument.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowAdministratorsRequired(string paramName)
        => throw new ArgumentException(SR.WorkflowAdministratorsRequired, paramName);

    /// <summary>Throws when a native build job cannot be completed because it is not building.</summary>
    /// <param name="id">The native build job id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobNotBuildingForCompletion(string id)
        => throw new NativeBuildJobStateException(id, SR.Format(SR.NativeBuildJobNotBuildingForCompletion, id));

    /// <summary>Throws when a native build job cannot have its lease renewed because it is not building.</summary>
    /// <param name="id">The native build job id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobNotBuildingForLeaseRenewal(string id)
        => throw new NativeBuildJobStateException(id, SR.Format(SR.NativeBuildJobNotBuildingForLeaseRenewal, id));

    /// <summary>Throws when a workflow deployment cannot be completed because it is not deploying.</summary>
    /// <param name="id">The workflow deployment id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentNotDeployingForCompletion(string id)
        => throw new WorkflowDeploymentStateException(id, SR.Format(SR.WorkflowDeploymentNotDeployingForCompletion, id));

    /// <summary>Throws when a workflow deployment cannot have its lease renewed because it is not deploying.</summary>
    /// <param name="id">The workflow deployment id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentNotDeployingForLeaseRenewal(string id)
        => throw new WorkflowDeploymentStateException(id, SR.Format(SR.WorkflowDeploymentNotDeployingForLeaseRenewal, id));
}
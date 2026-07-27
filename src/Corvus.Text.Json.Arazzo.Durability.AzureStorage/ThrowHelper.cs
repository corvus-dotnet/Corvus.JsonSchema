// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// Centralized exception-throwing helpers for the Azure Storage durability store.
/// </summary>
/// <remarks>
/// <para>
/// These are guard-position <c>Throw*</c> helpers marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize the call-site code after the throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when an environment administration record is created with no administrator identity.</summary>
    /// <param name="paramName">The offending parameter's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAdministratorRequired(string paramName)
        => throw new ArgumentException(SR.EnvironmentAdministratorRequired, paramName);

    /// <summary>Throws when a workflow administration record is created with no administrator identity.</summary>
    /// <param name="paramName">The offending parameter's name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowAdministratorRequired(string paramName)
        => throw new ArgumentException(SR.WorkflowAdministratorRequired, paramName);

    /// <summary>Throws when a source with the same name and security tags already exists.</summary>
    /// <param name="name">The source name that collides.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SourceAlreadyExists, name));

    /// <summary>Throws when a source credential binding with the same identity and security tags already exists.</summary>
    /// <param name="sourceName">The source name of the colliding binding.</param>
    /// <param name="environment">The environment of the colliding binding.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceCredentialAlreadyExists(string sourceName, string environment)
        => throw new InvalidOperationException(SR.Format(SR.SourceCredentialAlreadyExists, sourceName, environment));

    /// <summary>Throws when an environment with the same name and security tags already exists.</summary>
    /// <param name="name">The environment name that collides.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.EnvironmentAlreadyExists, name));

    /// <summary>Throws when a security rule with the same name already exists.</summary>
    /// <param name="name">The security rule name that collides.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityRuleAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SecurityRuleAlreadyExists, name));

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
}
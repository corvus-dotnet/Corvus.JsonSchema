// <copyright file="TenancyAgreement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The one shape every surface uses to refuse a version whose owner group is not its target environment's (ADR 0065):
/// promotion, promotion requests, schedules and run start all refuse the same problem type with the same audit outcome,
/// so a client and an auditor read one thing.
/// </summary>
internal static class TenancyAgreement
{
    /// <summary>The problem type.</summary>
    public const string ProblemType = "tenancy-mismatch";

    /// <summary>The problem title.</summary>
    public const string Title = "Owner group mismatch";

    /// <summary>The governance-audit outcome.</summary>
    public const string RefusedOutcome = "refused-tenancy-mismatch";

    /// <summary>Builds the problem detail naming both owner groups.</summary>
    /// <param name="baseWorkflowId">The version's base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="versionTags">The version's security tags.</param>
    /// <param name="environment">The target environment's name.</param>
    /// <param name="target">The target environment.</param>
    /// <param name="ownerGroupKeyUtf8">The deployment's owner-group tag key.</param>
    /// <returns>The detail.</returns>
    /// <remarks>Materializes both owner groups, on the refusal path only; the agreement check itself is string-free.</remarks>
    public static string Detail(string baseWorkflowId, int versionNumber, in SecurityTagSet versionTags, string environment, in Environment target, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        string versionGroup = OwnerGroupTag.Read(versionTags, ownerGroupKeyUtf8) is { } v ? $"owner group '{v}'" : "no owner group";
        string environmentGroup = OwnerGroupTag.Read(target, ownerGroupKeyUtf8) is { } e ? $"owner group '{e}'" : "no owner group";
        return $"Version {versionNumber} of '{baseWorkflowId}' carries {versionGroup} and environment '{environment}' carries {environmentGroup}; a version is made available, scheduled and run only in an environment its own owner group holds (ADR 0065).";
    }
}
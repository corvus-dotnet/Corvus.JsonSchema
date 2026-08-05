// <copyright file="TenantEnvironmentSealing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Answers the half of ADR 0065's tenancy invariant that the <see cref="TenancyLedger"/> cannot: whether any
/// tenant-owned environment holds no ACTIVE checkpoint key generation, and so is carrying its runs in the clear.
/// </summary>
/// <remarks>
/// <para>The owner-group half of the invariant is a single ledger row, so it is read rather than scanned. This half is
/// not derivable from a row: sealing state changes with every key registration, retirement, and environment create, and
/// a counter maintained by four separate writers would drift into a wrong answer that no read could detect. It is
/// therefore a scan, run only on the rare path that introduces an owner group, and only in the reach-isolating modes
/// whose rule depends on it.</para>
/// <para>The platform environment is excluded. Decision 10 makes it permanently unsealed, so counting it would refuse
/// every second-tenant onboarding forever. It is identified by <see cref="Environment.Platform"/> and never by name,
/// because a name is chosen by the party the exclusion defends against.</para>
/// <para>An environment carrying no owner group is not tenant-owned and is not counted either. It belongs to nobody the
/// gate can name, so requiring it to be sealed would block onboarding on a row no tenant claims.</para>
/// </remarks>
public static class TenantEnvironmentSealing
{
    /// <summary>Whether any tenant-owned environment in this batch holds no ACTIVE key generation.</summary>
    /// <param name="environments">One batch of environments, as a store page hands them back.</param>
    /// <param name="ownerGroupKeyUtf8">The deployment's internal owner-group tag key (for example <c>sys:tenant</c>).</param>
    /// <returns><see langword="true"/> once an unsealed tenant-owned environment is seen, so the caller stops paging.</returns>
    /// <remarks>Takes a batch rather than the whole population because a store hands back pages as pooled batches whose
    /// buffers are returned on dispose. Nothing is retained from a row, so no page outlives its scan.</remarks>
    public static bool AnyUnsealed(IReadOnlyList<Environment> environments, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        ArgumentNullException.ThrowIfNull(environments);

        // Indexed rather than foreach: enumerating an IReadOnlyList<T> goes through the interface and allocates an
        // enumerator per page, which is one allocation per page for a scan whose whole point is to stay cheap.
        for (int i = 0; i < environments.Count; ++i)
        {
            Environment environment = environments[i];
            if (!IsPlatform(environment) && OwnerGroupTag.IsTenantOwned(environment, ownerGroupKeyUtf8) && !HasActiveGeneration(environment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="environment"/> is the control plane's own platform environment, and so outside
    /// the tenancy invariant entirely.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> if the platform marker is present and set.</returns>
    public static bool IsPlatform(in Environment environment)
        => environment.Platform.IsNotUndefined() && (bool)environment.Platform;

    private static ReadOnlySpan<byte> ActiveUtf8 => "Active"u8;

    // Whether the environment holds at least one ACTIVE generation. Retired generations protect nothing, so an
    // environment whose only generations are retired counts as unsealed.
    private static bool HasActiveGeneration(in Environment environment)
    {
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(environment.KeyGenerations))
        {
            if (generation.State.ValueEquals(ActiveUtf8))
            {
                return true;
            }
        }

        return false;
    }
}
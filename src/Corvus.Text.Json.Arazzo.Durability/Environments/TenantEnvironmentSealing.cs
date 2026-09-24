// <copyright file="TenantEnvironmentSealing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Tells the control plane's own platform environment from a tenant-owned one for ADR 0065's tenancy invariant.
/// </summary>
/// <remarks>
/// <para>The platform environment is outside the invariant. Decision 10 makes it permanently unsealed, so counting it
/// would refuse every second-tenant onboarding forever. It is identified by <see cref="Environment.Platform"/> and
/// never by name, because a name is chosen by the party the exclusion defends against.</para>
/// <para>Until 2026-09-22 this class also answered whether any tenant-owned environment held no active checkpoint key
/// generation, and the gate admitted a second owner group once none did. That credited a barrier that did not exist,
/// since nothing encrypted under a registered key (V-38 of the 2026-08-07 audit), and the gate now refuses a second
/// owner group outright until phase B builds the encryption. The sealing predicate returns with it.</para>
/// </remarks>
public static class TenantEnvironmentSealing
{
    /// <summary>Whether <paramref name="environment"/> is the control plane's own platform environment, and so outside
    /// the tenancy invariant entirely.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> if the platform marker is present and set.</returns>
    public static bool IsPlatform(in Environment environment)
        => environment.Platform.IsNotUndefined() && (bool)environment.Platform;

    /// <summary>
    /// The ids of an environment's active key generations (ADR 0065 decision 12): the generations a checkpoint may be
    /// sealed under. A tenant environment with at least one is sealed (decision 10).
    /// </summary>
    /// <param name="environment">The environment record.</param>
    /// <returns>The active generation ids, empty when there are none.</returns>
    public static IReadOnlySet<string> ActiveGenerations(in Environment environment)
    {
        HashSet<string>? active = null;
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(environment.KeyGenerations))
        {
            if (generation.State.ValueEquals("Active"u8))
            {
                (active ??= new HashSet<string>(StringComparer.Ordinal)).Add((string)generation.KeyId);
            }
        }

        return active ?? (IReadOnlySet<string>)System.Collections.Frozen.FrozenSet<string>.Empty;
    }
}
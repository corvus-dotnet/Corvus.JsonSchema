// <copyright file="OwnerGroupProbe.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Decides whether a deployment's environments belong to more than one owner group (ADR 0065, phase A). The tenancy
/// invariant asks this of two write paths — may this write introduce another owner group, and may the last active key
/// generation be retired — and both must read it the same way, so it lives here rather than at either call site.
/// </summary>
/// <remarks>
/// <para>The question is a boolean, so this remembers one owner group rather than building a distinct set: the first
/// group seen, and whether any later row differs from it. Nothing is counted beyond that, because the moment a second
/// group appears the answer is settled and no caller asks how many there are.</para>
/// <para>The platform environment is excluded. Decision 10 makes it permanently unsealed, so counting it would leave
/// every deployment permanently at "more than one owner group" and refuse every second-tenant onboarding forever. It
/// is identified by <see cref="Environment.Platform"/> and never by name, because a name is chosen by the party the
/// exclusion defends against.</para>
/// <para>An environment carrying no owner group is not a group of its own. A deployment that authenticates but
/// declares no owner-group claim stamps nothing, and treating each unstamped row as its own tenancy would report a
/// single-tenant deployment as multi-tenant and refuse it work it is entitled to do.</para>
/// </remarks>
public struct OwnerGroupProbe : IDisposable
{
    private byte[]? first;
    private int firstLength;
    private bool seenFirst;
    private bool foundSecond;

    /// <summary>Gets a value indicating whether environments belonging to two different owner groups have been seen.</summary>
    public readonly bool MoreThanOne => this.foundSecond;

    /// <summary>
    /// Observes one batch of environments, returning whether a second distinct owner group is now known.
    /// </summary>
    /// <param name="environments">This batch of environments.</param>
    /// <param name="ownerGroupKeyUtf8">The deployment's internal owner-group tag key (e.g. <c>sys:tenant</c>).</param>
    /// <returns><see langword="true"/> once two different owner groups have been seen, so the caller stops paging.</returns>
    /// <remarks>
    /// Takes a batch rather than the whole population because a store hands back pages as pooled batches whose buffers
    /// are returned on dispose. An <see cref="Environment"/> retained past its page reads memory another request may
    /// already own, so the remembered group is copied into this probe's own pooled buffer and the rows are not kept.
    /// </remarks>
    public bool Observe(IReadOnlyList<Environment> environments, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        ArgumentNullException.ThrowIfNull(environments);

        // Indexed rather than foreach: enumerating an IReadOnlyList<T> goes through the interface and allocates an
        // enumerator per page, which is one allocation per page for a scan whose whole point is to stay cheap.
        for (int i = 0; i < environments.Count && !this.foundSecond; ++i)
        {
            Environment environment = environments[i];
            if (!IsPlatform(environment))
            {
                this.Observe(environment, ownerGroupKeyUtf8);
            }
        }

        return this.foundSecond;
    }

    /// <summary>Returns the pooled buffer holding the remembered owner group, if one was rented.</summary>
    public void Dispose()
    {
        if (this.first is { } rented)
        {
            this.first = null;
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Whether <paramref name="environment"/> is the control plane's own platform environment, and so outside
    /// the owner-group question entirely.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> if the platform marker is present and set.</returns>
    public static bool IsPlatform(in Environment environment)
        => environment.Platform.IsNotUndefined() && (bool)environment.Platform;

    // Folds one environment's owner group into the probe. The tag value is read, compared, and copied entirely inside
    // the enumeration: CurrentValue may point into the enumerator's pooled unescape scratch, which Dispose returns.
    private void Observe(in Environment environment, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        SecurityTagSet.Utf8Enumerator e = environment.ManagementTagsValue.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (!e.CurrentKey.SequenceEqual(ownerGroupKeyUtf8) || e.CurrentValue.IsEmpty)
                {
                    continue;
                }

                if (!this.seenFirst)
                {
                    this.Remember(e.CurrentValue);
                }
                else if (!e.CurrentValue.SequenceEqual(this.first.AsSpan(0, this.firstLength)))
                {
                    this.foundSecond = true;
                }

                return;
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private void Remember(ReadOnlySpan<byte> ownerGroup)
    {
        this.first = ArrayPool<byte>.Shared.Rent(ownerGroup.Length);
        ownerGroup.CopyTo(this.first);
        this.firstLength = ownerGroup.Length;
        this.seenFirst = true;
    }
}
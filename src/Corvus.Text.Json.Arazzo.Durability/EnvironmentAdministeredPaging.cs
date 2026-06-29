// <copyright file="EnvironmentAdministeredPaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Shared helpers for the reverse environment-administration index (design §7.8) that every
/// <see cref="IEnvironmentAdministratorStore"/> implementation uses, so the digest keys and the keyset page boundary are
/// computed identically across all backends. Mirrors <see cref="WorkflowAdministeredPaging"/>.
/// </summary>
public static class EnvironmentAdministeredPaging
{
    /// <summary>Computes the distinct collision-probe digests of an administrator set — the reverse-index keys for an
    /// environment. The empty identity has no digest and is skipped. Two identities are set-equal iff their digests are equal
    /// (<see cref="SecurityIdentityDigest"/>), so these are exactly the digests the forward
    /// <see cref="EnvironmentAdministrators.IsAdministeredBy"/> compares — the index can never over-grant or miss a match.</summary>
    /// <param name="administrators">The administrator identities written for an environment.</param>
    /// <returns>The distinct administrator digests (order as first seen), for the index write path.</returns>
    public static IReadOnlyList<string> DistinctDigests(IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators)
    {
        ArgumentNullException.ThrowIfNull(administrators);
        var digests = new List<string>(administrators.Count);
        foreach (EnvironmentAdministrators.AdministratorIdentity administrator in administrators)
        {
            if (SecurityIdentityDigest.Compute(SecurityTagSet.CopyFrom(administrator.Tags)) is { } digest && !digests.Contains(digest))
            {
                digests.Add(digest);
            }
        }

        return digests;
    }

    /// <summary>Computes the distinct collision-probe digests of a persisted administration record's current administrator
    /// set — used by stores that must retract an environment's <em>previous</em> index entries before writing its new ones
    /// (the old digests are read back from the record on disk, since no atomic server-side script holds them).</summary>
    /// <param name="record">The current persisted administration record for an environment.</param>
    /// <returns>The distinct administrator digests currently indexed for the environment.</returns>
    public static IReadOnlyList<string> DistinctDigests(EnvironmentAdministrators record)
    {
        var digests = new List<string>(record.AdministratorCount);
        if (record.Administrators.IsNotUndefined())
        {
            foreach (EnvironmentAdministrators.AdministratorIdentity administrator in record.Administrators.EnumerateArray())
            {
                if (SecurityIdentityDigest.Compute(SecurityTagSet.CopyFrom(administrator.Tags)) is { } digest && !digests.Contains(digest))
                {
                    digests.Add(digest);
                }
            }
        }

        return digests;
    }

    /// <summary>Builds an <see cref="EnvironmentAdministeredPage"/> from the keyset rows a store read for one page, where a
    /// store reads up to <paramref name="pageSize"/> + 1 rows (the +1 lookahead detects "more remain"). When more rows than
    /// the page were read, the lookahead is trimmed and a continuation token is encoded from the last returned name's UTF-8.</summary>
    /// <param name="rows">The environment names read for the page, ordered by name; at most <paramref name="pageSize"/> + 1.</param>
    /// <param name="pageSize">The requested page size (the number of rows to return at most).</param>
    /// <returns>The page (owning its pooled token buffer when a next page exists).</returns>
    public static EnvironmentAdministeredPage ToPage(IReadOnlyList<string> rows, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count <= pageSize)
        {
            return EnvironmentAdministeredPage.Create(rows);
        }

        var page = new List<string>(pageSize);
        for (int i = 0; i < pageSize; i++)
        {
            page.Add(rows[i]);
        }

        // Encode the continuation token from the last returned name's UTF-8 (bytes-native; scratch sized by the
        // worst-case UTF-8 length and sliced by the actual bytes written).
        string last = page[^1];
        byte[] scratch = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(last.Length));
        try
        {
            int written = Encoding.UTF8.GetBytes(last, scratch);
            return EnvironmentAdministeredPage.Create(page, scratch.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }
}
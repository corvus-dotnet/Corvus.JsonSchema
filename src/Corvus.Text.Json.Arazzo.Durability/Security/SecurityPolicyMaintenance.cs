// <copyright file="SecurityPolicyMaintenance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Housekeeping for the row-authorization policy. The time-bound (§16.5.2) grant model is <strong>fail-safe at
/// resolution</strong> — a resolver excludes any binding past its <see cref="SecurityBindingDocument.ExpiresAtValue"/>,
/// so an expired grant stops working whether or not it has been reaped. Reaping is therefore pure housekeeping: it
/// deletes expired binding rows to keep the policy snapshot small. A host runs it on a timer; correctness never
/// depends on it. It is store-agnostic — it drives only the public <see cref="ISecurityPolicyStore"/> surface, so it
/// works against every backend without per-backend code.
/// </summary>
public static class SecurityPolicyMaintenance
{
    /// <summary>Deletes every binding whose time-bound grant has expired (now ≥ its expiry).</summary>
    /// <param name="store">The policy store to reap.</param>
    /// <param name="timeProvider">The time source defining "now".</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of expired bindings deleted.</returns>
    public static async ValueTask<int> ReapExpiredBindingsAsync(ISecurityPolicyStore store, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset now = timeProvider.GetUtcNow();

        // Collect the expired (id, etag) pairs from a single snapshot, then delete them — the etag string outlives the
        // pooled snapshot, so the snapshot is disposed before the deletes (no document held across the await loop).
        var expired = new List<(string Id, WorkflowEtag Etag)>();
        using (SecurityPolicySnapshot snapshot = await store.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (SecurityBindingDocument binding in snapshot.Bindings)
            {
                if (binding.ExpiresAtValue is { } expiresAt && now >= expiresAt)
                {
                    expired.Add((binding.IdValue, binding.EtagValue));
                }
            }
        }

        int reaped = 0;
        foreach ((string id, WorkflowEtag etag) in expired)
        {
            try
            {
                if (await store.DeleteBindingAsync(id, etag, cancellationToken).ConfigureAwait(false))
                {
                    reaped++;
                }
            }
            catch (SecurityPolicyConflictException)
            {
                // Concurrently modified (e.g. re-granted with a fresh expiry) since the snapshot — leave it; the next
                // pass re-evaluates. Reaping is best-effort housekeeping, not correctness.
            }
        }

        return reaped;
    }
}
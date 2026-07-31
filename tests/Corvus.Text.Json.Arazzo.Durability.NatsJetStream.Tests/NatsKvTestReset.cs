// <copyright file="NatsKvTestReset.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Resets NATS JetStream key/value buckets to empty for conformance tests, then re-provisions them.
/// </summary>
/// <remarks>
/// <para>
/// The per-test reset drops each bucket and immediately re-provisions it. Deleting a JetStream stream and
/// recreating it under the same name races the server's asynchronous stream teardown: on a loaded or slower
/// host (CI), the recreate can arrive before the delete has settled and the server rejects it with
/// <c>NatsJSApiException: error creating store for stream</c> (the server-side "stream name already in use").
/// A fast, idle host almost never hits the window, so the failure surfaces only intermittently in CI.
/// </para>
/// <para>
/// This helper rides over the teardown window: on a rejected provision it waits briefly, re-deletes (the old
/// stream has since finished tearing down), and retries — so the reset always ends with a fresh, empty bucket
/// rather than either a flake or a stale one.
/// </para>
/// </remarks>
internal static class NatsKvTestReset
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Drops the named buckets (ignoring any that do not exist), then re-provisions them, retrying over the
    /// stream-teardown window if the provision is transiently rejected.
    /// </summary>
    /// <param name="kv">The KV context bound to the test connection.</param>
    /// <param name="buckets">The bucket names to drop before provisioning.</param>
    /// <param name="provisionAsync">Provisions the store's bucket(s) (typically the store's <c>PrepareAsync</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the buckets are empty and provisioned.</returns>
    public static async ValueTask ResetAndProvisionAsync(
        NatsKVContext kv,
        IReadOnlyList<string> buckets,
        Func<ValueTask> provisionAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kv);
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(provisionAsync);

        for (int attempt = 1; ; attempt++)
        {
            foreach (string bucket in buckets)
            {
                try
                {
                    await kv.DeleteStoreAsync(bucket, cancellationToken).ConfigureAwait(false);
                }
                catch (NatsJSApiException)
                {
                    // The bucket (JetStream stream) does not exist yet — nothing to reset.
                }
            }

            try
            {
                await provisionAsync().ConfigureAwait(false);
                return;
            }
            catch (NatsJSApiException) when (attempt < MaxAttempts)
            {
                // The prior stream teardown had not settled server-side; wait, then re-delete and retry.
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

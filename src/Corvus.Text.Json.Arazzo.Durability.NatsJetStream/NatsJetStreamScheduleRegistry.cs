// <copyright file="NatsJetStreamScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IScheduleRegistry"/>: one entry per <c>scheduleId</c> whose
/// create-if-absent conflict enforces the schedules contract's deployment-global uniqueness. Under the
/// composite <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of
/// that uniqueness — a run-key collision can no longer observe a schedule created in another environment — so
/// a NATS deployment needs this durable registry, not the in-memory one, or every control-plane restart
/// forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// The entry's value is the registration's own delimited composite <c>{environment}/{runId}</c> (the
/// environment-name grammar admits no slash, so the first slash splits the halves). Provision the bucket once
/// with <see cref="PrepareAsync(string, CancellationToken)"/>, then open the registry with
/// <see cref="ConnectAsync(string, CancellationToken)"/> (or the connection overloads).
/// </remarks>
public sealed class NatsJetStreamScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private const string Bucket = "arazzo_schedules";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore registrations;

    private NatsJetStreamScheduleRegistry(NatsConnection? ownedConnection, INatsKVStore registrations)
    {
        this.ownedConnection = ownedConnection;
        this.registrations = registrations;
    }

    /// <summary>Provisions the registry's key/value bucket.</summary>
    /// <remarks>
    /// Creating a KV bucket creates a JetStream stream, which requires stream-management permissions, so run
    /// this once at deploy time, separately from the least-privileged account used to operate the registry.
    /// </remarks>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        await PrepareAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the registry's key/value bucket over a caller-supplied connection (the caller retains ownership).</summary>
    /// <param name="connection">A NATS connection whose account is permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry over the given NATS URL.</summary>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamScheduleRegistry> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore registrations = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamScheduleRegistry(connection, registrations);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the registry over a caller-supplied connection (the caller retains ownership).</summary>
    /// <param name="connection">A configured NATS connection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamScheduleRegistry> ConnectAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore registrations = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamScheduleRegistry(ownedConnection: null, registrations);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        // The create-if-absent conflict IS the uniqueness gate; the read below sees the committed occupant.
        byte[] value = Encoding.UTF8.GetBytes(registration.Environment + "/" + registration.RunId.Value);
        try
        {
            await this.registrations.CreateAsync(scheduleId, value, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (NatsKVException)
        {
            // Occupied: fall through to compare with the occupant.
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        if (await this.ReadAsync(scheduleId, cancellationToken).ConfigureAwait(false) is { } existing && existing == registration)
        {
            return;
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        return await this.ReadAsync(scheduleId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        try
        {
            await this.registrations.PurgeAsync(scheduleId, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<ScheduleRegistration?> ReadAsync(string scheduleId, CancellationToken cancellationToken)
    {
        try
        {
            NatsKVEntry<byte[]> entry = await this.registrations.GetEntryAsync<byte[]>(scheduleId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value is not { } value)
            {
                return null;
            }

            string composite = Encoding.UTF8.GetString(value);
            int separator = composite.IndexOf('/', StringComparison.Ordinal);
            return new ScheduleRegistration(composite[..separator], new WorkflowRunId(composite[(separator + 1)..]));
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }
}
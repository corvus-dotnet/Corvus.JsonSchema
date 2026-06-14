// <copyright file="InMemoryRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Threading;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IRunnerRegistry"/>. Each runner's
/// <see cref="RunnerRegistration"/> is held as its UTF-8 JSON document — exactly the "push JSON to the store"
/// shape every backend persists — keyed by runner id, with reads detaching the value via
/// <see cref="RunnerRegistration.FromJson"/> so it outlives the parse. It is the reference against which the
/// shared registry-conformance suite runs, and is usable for a single-process host that does not need the
/// registry to survive a restart.
/// </summary>
public sealed class InMemoryRunnerRegistry : IRunnerRegistry
{
    private readonly Dictionary<string, byte[]> entries = [];
    private readonly Lock gate = new();

    /// <inheritdoc/>
    public ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string runnerId = registration.RunnerIdValue;
        var buffer = new ArrayBufferWriter<byte>();
        registration.WriteTo(buffer);
        byte[] document = buffer.WrittenSpan.ToArray();

        lock (this.gate)
        {
            this.entries[runnerId] = document;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            if (!this.entries.TryGetValue(runnerId, out byte[]? bytes))
            {
                return ValueTask.FromResult(false);
            }

            var buffer = new ArrayBufferWriter<byte>();
            RunnerRegistration.FromJson(bytes).WriteWithLastSeenAt(buffer, at);
            this.entries[runnerId] = buffer.WrittenSpan.ToArray();
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            var result = new List<RunnerRegistration>(this.entries.Count);
            foreach (byte[] bytes in this.entries.Values)
            {
                result.Add(RunnerRegistration.FromJson(bytes));
            }

            return ValueTask.FromResult<IReadOnlyList<RunnerRegistration>>(result);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            foreach (byte[] bytes in this.entries.Values)
            {
                if (RunnerRegistration.FromJson(bytes).HostsVersion(baseWorkflowId, versionNumber))
                {
                    return ValueTask.FromResult(true);
                }
            }

            return ValueTask.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            List<string>? dead = null;
            foreach (KeyValuePair<string, byte[]> entry in this.entries)
            {
                if (RunnerRegistration.FromJson(entry.Value).LastSeenAtValue < deadBefore)
                {
                    (dead ??= []).Add(entry.Key);
                }
            }

            if (dead is null)
            {
                return ValueTask.FromResult(0);
            }

            foreach (string id in dead)
            {
                this.entries.Remove(id);
            }

            return ValueTask.FromResult(dead.Count);
        }
    }
}
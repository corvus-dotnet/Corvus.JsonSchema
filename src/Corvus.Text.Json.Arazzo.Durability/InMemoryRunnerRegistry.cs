// <copyright file="InMemoryRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IRunnerRegistry"/>. Each runner's
/// <see cref="RunnerRegistration"/> is held as its UTF-8 JSON document — exactly the "push JSON to the store"
/// shape every backend persists — keyed by runner id, with reads detaching the value via
/// <see cref="RunnerRegistration.Clone"/> so it outlives the parse. It is the reference against which the
/// shared registry-conformance suite runs, and is usable for a single-process host that does not need the
/// registry to survive a restart.
/// </summary>
public sealed class InMemoryRunnerRegistry : IRunnerRegistry
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private readonly Dictionary<string, byte[]> entries = [];
    private readonly Lock gate = new();

    /// <inheritdoc/>
    public ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string runnerId = (string)registration.RunnerId;
        byte[] document = Serialize(registration);

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

            using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(bytes);
            RunnerRegistration existing = doc.RootElement;
            this.entries[runnerId] = Rebuild(existing, at);
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
                using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(bytes);
                result.Add(doc.RootElement.Clone());
            }

            return ValueTask.FromResult<IReadOnlyList<RunnerRegistration>>(result);
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
                using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(entry.Value);
                if (ReadLastSeen(doc.RootElement) < deadBefore)
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

    private static byte[] Serialize(in RunnerRegistration registration)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            registration.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] Rebuild(in RunnerRegistration existing, DateTimeOffset lastSeenAt)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RunnerRegistration.Mutable> builder = RunnerRegistration.CreateBuilder(
            workspace,
            hostedVersions: existing.HostedVersions,
            lastSeenAt: (JsonDateTime.Source)lastSeenAt.ToString("O", CultureInfo.InvariantCulture),
            maxConcurrency: existing.MaxConcurrency,
            runnerId: existing.RunnerId,
            startedAt: existing.StartedAt,
            transports: existing.Transports,
            address: existing.Address.ValueKind == JsonValueKind.Undefined ? default : (JsonString.Source)existing.Address);

        return Serialize(builder.RootElement);
    }

    private static DateTimeOffset ReadLastSeen(in RunnerRegistration registration)
        => DateTimeOffset.Parse((string)registration.LastSeenAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
// <copyright file="RunnerRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A registration record for a workflow runner process: its identity, liveness
/// timestamps, capacity, supported transports, and the catalog versions it hosts.
/// </summary>
/// <remarks>
/// This is the persisted registry entity. Every backend stores it as its JSON document verbatim
/// (see <see cref="ToJsonBytes"/> / <see cref="FromJson"/>), keyed by <see cref="RunnerId"/>, with a
/// queryable <see cref="LastSeenAtValue"/> for pruning.
/// </remarks>
[JsonSchemaTypeGenerator("Schemas/RunnerRegistration.json")]
public readonly partial struct RunnerRegistration
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the runner id as a string.</summary>
    public string RunnerIdValue => (string)this.RunnerId;

    /// <summary>Gets the instant of the runner's most recent heartbeat.</summary>
    public DateTimeOffset LastSeenAtValue
        => DateTimeOffset.Parse((string)this.LastSeenAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>Parses a <see cref="RunnerRegistration"/> from its persisted JSON document, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The runner registration.</returns>
    public static RunnerRegistration FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this registration to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Returns a copy of this registration with its <see cref="LastSeenAt"/> advanced.</summary>
    /// <param name="at">The instant of the heartbeat.</param>
    /// <returns>The updated registration, detached and ready to persist.</returns>
    public RunnerRegistration WithLastSeenAt(DateTimeOffset at)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            hostedVersions: this.HostedVersions,
            lastSeenAt: (JsonDateTime.Source)at.ToString("O", CultureInfo.InvariantCulture),
            maxConcurrency: this.MaxConcurrency,
            runnerId: this.RunnerId,
            startedAt: this.StartedAt,
            transports: this.Transports,
            address: this.Address.ValueKind == JsonValueKind.Undefined ? default : (JsonString.Source)this.Address);

        return builder.RootElement.Clone();
    }
}
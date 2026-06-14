// <copyright file="RunnerRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A registration record for a workflow runner process: its identity, liveness
/// timestamps, capacity, supported transports, and the catalog versions it hosts.
/// </summary>
/// <remarks>
/// This is the persisted registry entity. Every backend stores it as its JSON document verbatim, keyed by
/// <see cref="RunnerId"/>, with a queryable <see cref="LastSeenAtValue"/> for pruning. A store writes it straight
/// into the buffer (or stream) it owns (<see cref="WriteTo(IBufferWriter{byte})"/> on register,
/// <see cref="WriteWithLastSeenAt"/> on heartbeat) and reads it back with <see cref="FromJson"/> — no detached
/// clone, no second serialization.
/// </remarks>
[JsonSchemaTypeGenerator("Schemas/RunnerRegistration.json")]
public readonly partial struct RunnerRegistration
{
    /// <summary>Gets the runner id as a string.</summary>
    public string RunnerIdValue => (string)this.RunnerId;

    /// <summary>Gets the instant of the runner's most recent heartbeat.</summary>
    public DateTimeOffset LastSeenAtValue => ((NodaTime.OffsetDateTime)this.LastSeenAt).ToDateTimeOffset();

    /// <summary>Determines whether this runner hosts the given catalog version with it loaded and ready to run.</summary>
    /// <param name="baseWorkflowId">The base workflow id of the version.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <returns><see langword="true"/> if a hosted version matches and is loaded.</returns>
    public bool HostsVersion(string baseWorkflowId, int versionNumber)
    {
        foreach (RunnerHostedVersion hosted in this.HostedVersions.EnumerateArray())
        {
            if ((bool)hosted.Loaded
                && hosted.VersionNumber == versionNumber
                && (string)hosted.BaseWorkflowId == baseWorkflowId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the (baseWorkflowId, versionNumber) pairs this runner hosts with the version loaded — the rows a backend projects into its hosting index.</summary>
    /// <returns>The loaded hosted versions.</returns>
    public IReadOnlyList<(string BaseWorkflowId, int VersionNumber)> LoadedHostedVersions()
    {
        var list = new List<(string, int)>();
        foreach (RunnerHostedVersion hosted in this.HostedVersions.EnumerateArray())
        {
            if ((bool)hosted.Loaded)
            {
                list.Add(((string)hosted.BaseWorkflowId, hosted.VersionNumber));
            }
        }

        return list;
    }

    /// <summary>Parses a <see cref="RunnerRegistration"/> from its persisted JSON document as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The runner registration.</returns>
    public static RunnerRegistration FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Writes this registration's JSON straight into the caller's buffer (or stream) in a single pass.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    public void WriteTo(IBufferWriter<byte> buffer)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            this.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Writes a heartbeated copy of this registration (its <see cref="LastSeenAt"/> advanced) into the caller's
    /// buffer, modifying only that field — no field-by-field rebuild.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    /// <param name="at">The instant of the heartbeat.</param>
    public void WriteWithLastSeenAt(IBufferWriter<byte> buffer, DateTimeOffset at)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.HeartbeatBuilder(workspace, at);
        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Returns a heartbeated copy of this registration (its <see cref="LastSeenAt"/> advanced) as a detached
    /// value — for a store that must reshape it (e.g. re-embed it in a backend envelope). Modifies only that field and
    /// detaches once; no scratch buffer, no re-parse. Byte-oriented stores use <see cref="WriteWithLastSeenAt"/>.</summary>
    /// <param name="at">The instant of the heartbeat.</param>
    /// <returns>The detached, heartbeated registration.</returns>
    public RunnerRegistration WithLastSeenAt(DateTimeOffset at)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.HeartbeatBuilder(workspace, at);
        return builder.RootElement.Clone();
    }

    // Realises a mutable builder over this registration with its LastSeenAt advanced (modifies only that field).
    private JsonDocumentBuilder<Mutable> HeartbeatBuilder(JsonWorkspace workspace, DateTimeOffset at)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetLastSeenAt(at);
        return builder;
    }
}
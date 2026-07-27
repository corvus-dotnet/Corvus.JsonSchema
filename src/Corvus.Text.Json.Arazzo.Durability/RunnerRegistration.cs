// <copyright file="RunnerRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A registration record for a workflow runner process: its identity, liveness
/// timestamps, capacity, supported transports, and the catalog versions it hosts.
/// </summary>
/// <remarks>
/// This is the persisted registry entity. Every backend stores it as its JSON document verbatim, keyed by
/// <see cref="RunnerId"/>, with a queryable <see cref="LastSeenAtValue"/> for pruning. A store serializes it through
/// a pooled writer (the generated <see cref="WriteTo(Utf8JsonWriter)"/> on register; <see cref="WriteWithLastSeenAt"/>
/// on heartbeat) and reads it back with <see cref="FromJson"/> — no detached clone, no second serialization, no
/// unpooled scratch buffer.
/// </remarks>
[JsonSchemaTypeGenerator("Schemas/RunnerRegistration.json")]
public readonly partial struct RunnerRegistration
{
    /// <summary>Gets the runner id as a string.</summary>
    public string RunnerIdValue => (string)this.RunnerId;

    /// <summary>Gets the instant of the runner's most recent heartbeat.</summary>
    public DateTimeOffset LastSeenAtValue => ((NodaTime.OffsetDateTime)this.LastSeenAt).ToDateTimeOffset();

    /// <summary>Gets whether this runner claims §18 draft runs for its environment (absent means <see langword="false"/>).</summary>
    public bool HostsDraftRunsValue => ((JsonElement)this.HostsDraftRuns).ValueKind == JsonValueKind.True;

    /// <summary>Gets whether this runner claims durable schedule runs (#896) for its environment (absent means <see langword="false"/>).</summary>
    public bool ServesSchedulesValue => ((JsonElement)this.ServesSchedules).ValueKind == JsonValueKind.True;

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
                && ((JsonElement)hosted.BaseWorkflowId).EqualsString(baseWorkflowId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the run isolation this runner's execution backend provides (ADR 0058); an absent <c>isolationModel</c> means <see cref="RunIsolationModel.InProcess"/>.</summary>
    public RunIsolationModel IsolationModelValue
        => ((JsonElement)this.IsolationModel).ValueKind == JsonValueKind.String && this.IsolationModel.ValueEquals("Isolated"u8)
            ? RunIsolationModel.Isolated : RunIsolationModel.InProcess;

    /// <summary>Determines whether this runner satisfies a required isolation (ADR 0058): its advertised model is at least as
    /// strong as the requirement (<see cref="RunIsolationModel.Isolated"/> is stronger than <see cref="RunIsolationModel.InProcess"/>),
    /// string-free. An <see cref="RunIsolationModel.InProcess"/> requirement is met by any runner; an
    /// <see cref="RunIsolationModel.Isolated"/> requirement only by a runner advertising <c>Isolated</c>.</summary>
    /// <param name="requiredIsolation">The isolation the target environment requires.</param>
    /// <returns><see langword="true"/> if this runner provides at least the required isolation.</returns>
    public bool ProvidesIsolation(RunIsolationModel requiredIsolation)
        => requiredIsolation == RunIsolationModel.InProcess || this.IsolationModelValue == RunIsolationModel.Isolated;

    /// <summary>Determines whether this runner hosts the given loaded version AND provides the required isolation (ADR 0058) —
    /// the start-gate match. The isolation check runs first, so a runner of the wrong isolation is rejected without scanning
    /// its hosted versions.</summary>
    /// <param name="baseWorkflowId">The base workflow id of the version.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="requiredIsolation">The isolation the target environment requires.</param>
    /// <returns><see langword="true"/> if this runner hosts the loaded version and provides the required isolation.</returns>
    public bool HostsVersion(string baseWorkflowId, int versionNumber, RunIsolationModel requiredIsolation)
        => this.ProvidesIsolation(requiredIsolation) && this.HostsVersion(baseWorkflowId, versionNumber);

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

    /// <summary>Writes a heartbeated copy of this registration (its <see cref="LastSeenAt"/> advanced) to the writer,
    /// modifying only that field — no field-by-field rebuild. The caller owns (and flushes) the writer.</summary>
    /// <param name="writer">The writer to serialize into (typically rented from a workspace).</param>
    /// <param name="at">The instant of the heartbeat.</param>
    public void WriteWithLastSeenAt(Utf8JsonWriter writer, DateTimeOffset at)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetLastSeenAt(at);
        builder.RootElement.WriteTo(writer);
    }
}
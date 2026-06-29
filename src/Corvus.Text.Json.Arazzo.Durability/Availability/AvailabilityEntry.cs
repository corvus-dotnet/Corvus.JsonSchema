// <copyright file="AvailabilityEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The persisted form of one cell of the availability matrix (design §7.8): a workflow version made available in a
/// deployment environment. Generated from <c>Schemas/Availability.json</c> and used as the domain value <em>and</em> the
/// persisted form.
/// </summary>
/// <remarks>
/// <para>
/// AvailabilityEntry is additive and many-to-many: the record's presence means the version is available in the environment,
/// and deleting it withdraws availability — there is no mutable state (so no update / carried-forward write, only a
/// <see cref="WriteNew"/>). The record is keyed by (<see cref="BaseWorkflowIdValue"/>, <see cref="VersionNumberValue"/>,
/// <see cref="EnvironmentValue"/>); it carries no security tags — authorization (the target environment's administrators)
/// and readiness (§7.7) are enforced at the control-plane surface, not by the store.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/Availability.json")]
public readonly partial struct AvailabilityEntry
{
    /// <summary>Gets the base workflow id the made-available version belongs to.</summary>
    public string BaseWorkflowIdValue => (string)this.BaseWorkflowId;

    /// <summary>Gets the 1-based version number made available.</summary>
    public int VersionNumberValue => (int)this.VersionNumber;

    /// <summary>Gets the deployment environment the version is available in.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the actor that made the version available.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Parses an availability entry from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The availability entry.</returns>
    public static AvailabilityEntry FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this availability entry to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in AvailabilityEntry v) => v.WriteTo(writer));

    /// <summary>Builds a draft availability entry from its key values for a store to complete with the server-stamped
    /// createdBy/createdAt/etag. No intermediate record — the key is written straight into the draft document.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <returns>A pooled, disposable draft document; the store reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<AvailabilityEntry> Draft(string baseWorkflowId, int versionNumber, string environment)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        return PersistedJson.ToPooledDocument<AvailabilityEntry, (string BaseWorkflowId, int VersionNumber, string Environment)>(
            (baseWorkflowId, versionNumber, environment),
            static (Utf8JsonWriter writer, in (string BaseWorkflowId, int VersionNumber, string Environment) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, s.BaseWorkflowId);
                writer.WriteNumber(JsonPropertyNames.VersionNumberUtf8, s.VersionNumber);
                writer.WriteString(JsonPropertyNames.EnvironmentUtf8, s.Environment);
                writer.WriteEndObject();
            });
    }

    /// <summary>Writes a brand-new availability entry's JSON into the caller's (pooled) writer in one pass — the draft's
    /// key is carried bytes-to-bytes and the server fields (createdBy/createdAt/etag) are stamped here.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the key as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor making the version available (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in AvailabilityEntry draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireKey(draft);
        writer.WriteStartObject();
        WriteValue(writer, JsonPropertyNames.BaseWorkflowIdUtf8, (JsonElement)draft.BaseWorkflowId);
        WriteValue(writer, JsonPropertyNames.VersionNumberUtf8, (JsonElement)draft.VersionNumber);
        WriteValue(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)draft.Environment);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    // Copies a draft property to the writer bytes-to-bytes.
    private static void WriteValue(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        writer.WritePropertyName(name);
        value.WriteTo(writer);
    }

    // A create draft must carry the full key (baseWorkflowId, versionNumber, environment).
    private static void RequireKey(in AvailabilityEntry draft)
    {
        if (!draft.BaseWorkflowId.IsNotUndefined() || !draft.VersionNumber.IsNotUndefined() || !draft.Environment.IsNotUndefined())
        {
            throw new ArgumentException("An availability entry requires a 'baseWorkflowId', 'versionNumber', and 'environment'.", nameof(draft));
        }
    }
}
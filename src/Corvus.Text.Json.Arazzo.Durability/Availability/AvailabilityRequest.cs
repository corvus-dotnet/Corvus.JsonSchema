// <copyright file="AvailabilityRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A persisted availability request (design §7.8): a principal's request to make a workflow version available in an
/// environment, plus the decision state and audit/concurrency metadata. Governed by the target environment's
/// administrators (mirroring the §16.5 access-request inbox, parameterised by environment). Generated from
/// <c>Schemas/AvailabilityRequest.json</c> and used as the domain value <em>and</em> the persisted form; it is congruent
/// with the API's <c>AvailabilityRequestView</c>.
/// </summary>
/// <remarks>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteDecision"/>): a store passes the
/// buffer it owns and the request is realised and written in one pass — no interim detached clone. The leaf accessors
/// realise a <see cref="string"/> only where one is required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/AvailabilityRequest.json")]
public readonly partial struct AvailabilityRequest
{
    /// <summary>Gets the request's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the base workflow id the request targets.</summary>
    public string BaseWorkflowIdValue => (string)this.BaseWorkflowId;

    /// <summary>Gets the 1-based version number the request would make available.</summary>
    public int VersionNumberValue => (int)this.VersionNumber;

    /// <summary>Gets the target environment.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the optional justification, or <see langword="null"/>.</summary>
    public string? ReasonOrNull => this.Reason.IsNotUndefined() ? (string)this.Reason : null;

    /// <summary>Gets the request's lifecycle state.</summary>
    public string StatusValue => (string)this.Status;

    /// <summary>Gets the actor (requester) that created the request.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the request was created.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets the actor that decided the request, or <see langword="null"/> if undecided.</summary>
    public string? DecidedByOrNull => this.DecidedBy.IsNotUndefined() ? (string)this.DecidedBy : null;

    /// <summary>Gets when the request was decided, or <see langword="null"/> if undecided.</summary>
    public DateTimeOffset? DecidedAtValue => this.DecidedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.DecidedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optional decision note, or <see langword="null"/>.</summary>
    public string? DecisionReasonOrNull => this.DecisionReason.IsNotUndefined() ? (string)this.DecisionReason : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Parses a request from its persisted JSON as a detached value. Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The request.</returns>
    public static AvailabilityRequest FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Builds a draft Pending request from the create-content (target version + environment + optional reason) for a
    /// store to complete with the server-stamped id/etag/created metadata and the initial Pending status. The store reads
    /// only these content fields (bytes-to-bytes) and stamps the rest.</summary>
    /// <param name="baseWorkflowId">The base workflow id the request targets.</param>
    /// <param name="versionNumber">The 1-based version number to make available.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="reason">An optional justification.</param>
    /// <returns>A pooled, disposable draft document the caller must dispose once the store/pipeline has read it.</returns>
    public static ParsedJsonDocument<AvailabilityRequest> Draft(string baseWorkflowId, int versionNumber, string environment, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        var state = (baseWorkflowId, versionNumber, environment, reason);
        return PersistedJson.ToPooledDocument<AvailabilityRequest, (string BaseWorkflowId, int VersionNumber, string Environment, string? Reason)>(
            in state,
            static (Utf8JsonWriter writer, in (string BaseWorkflowId, int VersionNumber, string Environment, string? Reason) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, c.BaseWorkflowId);
                writer.WriteNumber(JsonPropertyNames.VersionNumberUtf8, c.VersionNumber);
                writer.WriteString(JsonPropertyNames.EnvironmentUtf8, c.Environment);
                if (c.Reason is { } reason)
                {
                    writer.WriteString(JsonPropertyNames.ReasonUtf8, reason);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Realises a new (Pending) request into the caller's (pooled) writer in one pass — the draft's create-content
    /// is carried bytes-to-bytes and the id/status/server audit fields are stamped here.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="id">The request id.</param>
    /// <param name="draft">The draft carrying the create-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor (requester) creating the request (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, in AvailabilityRequest draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireContent(draft);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        WriteValue(writer, JsonPropertyNames.BaseWorkflowIdUtf8, (JsonElement)draft.BaseWorkflowId);
        WriteValue(writer, JsonPropertyNames.VersionNumberUtf8, (JsonElement)draft.VersionNumber);
        WriteValue(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)draft.Environment);
        if (draft.Reason.IsNotUndefined())
        {
            WriteValue(writer, JsonPropertyNames.ReasonUtf8, (JsonElement)draft.Reason);
        }

        writer.WriteString(JsonPropertyNames.StatusUtf8, AvailabilityRequestStatusNames.Pending);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Realises a decided copy of this request (status + decision fields set; everything else carried through) and
    /// writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="decision">The decision to apply (terminal status + optional reason).</param>
    /// <param name="actor">The actor deciding the request (audit).</param>
    /// <param name="decidedAt">The decision instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteDecision(Utf8JsonWriter writer, AvailabilityRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyDecision(workspace, decision, actor, decidedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    // Copies a draft property to the writer bytes-to-bytes.
    private static void WriteValue(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        writer.WritePropertyName(name);
        value.WriteTo(writer);
    }

    // A create draft must carry the target content (baseWorkflowId, versionNumber, environment).
    private static void RequireContent(in AvailabilityRequest draft)
    {
        if (!draft.BaseWorkflowId.IsNotUndefined() || !draft.VersionNumber.IsNotUndefined() || !draft.Environment.IsNotUndefined())
        {
            throw new ArgumentException("An availability request requires a 'baseWorkflowId', 'versionNumber', and 'environment'.", nameof(draft));
        }
    }

    // Realises a mutable builder over this document and applies the decision; id/created/content fields carry through.
    private JsonDocumentBuilder<Mutable> ApplyDecision(JsonWorkspace workspace, AvailabilityRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetStatus(AvailabilityRequestStatusNames.ToWire(decision.Status));
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetDecidedAt(decidedAt);
        builder.RootElement.SetDecidedBy(actor);
        if (decision.DecisionReason is { } reason)
        {
            builder.RootElement.SetDecisionReason(reason);
        }
        else
        {
            builder.RootElement.RemoveDecisionReason();
        }

        return builder;
    }
}
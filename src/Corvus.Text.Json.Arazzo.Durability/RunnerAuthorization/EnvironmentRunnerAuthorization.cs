// <copyright file="EnvironmentRunnerAuthorization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A persisted environment-runner authorization (design §5.5): a runner's authorization to serve a deployment environment,
/// plus the decision state and audit/concurrency metadata. Because receiving an environment's runs means receiving its
/// credentials, a runner cannot self-assert into an environment — it enters the <c>Pending</c> state and is not dispatchable
/// until an administrator of that environment (§15.1) authorizes it. Keyed by <c>(environment, runnerId)</c> and governed by
/// the target environment's administrators, exactly like Availability (§7.8). Generated from
/// <c>Schemas/EnvironmentRunnerAuthorization.json</c> and used as the domain value <em>and</em> the persisted form; it is
/// congruent with the API's <c>EnvironmentRunnerAuthorizationView</c>.
/// </summary>
/// <remarks>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteDecision"/>): a store passes the
/// buffer it owns and the record is realised and written in one pass — no interim detached clone. The leaf accessors realise
/// a <see cref="string"/> only where one is required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/EnvironmentRunnerAuthorization.json")]
public readonly partial struct EnvironmentRunnerAuthorization
{
    /// <summary>Gets the environment the runner asks to serve.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the runner the authorization applies to.</summary>
    public string RunnerIdValue => (string)this.RunnerId;

    /// <summary>Gets the authorization's lifecycle state as a realised <see cref="string"/> — for a display/log/serialize
    /// sink only. To <em>branch</em> on the status, use the string-free <see cref="IsAuthorized"/>/<see cref="IsPending"/>/
    /// <see cref="IsRevoked"/> predicates, which compare the JSON value's bytes and never allocate.</summary>
    public string StatusValue => (string)this.Status;

    /// <summary>Gets a value indicating whether the runner is authorized to serve the environment (dispatchable), compared
    /// string-free against the wire value — no string is realised. The u8 literals mirror the schema's status enum
    /// (see <see cref="RunnerAuthorization.RunnerAuthorizationStatusNames"/>).</summary>
    public bool IsAuthorized => this.Status.ValueEquals("Authorized"u8);

    /// <summary>Gets a value indicating whether the runner is awaiting an administrator's decision, compared string-free.</summary>
    public bool IsPending => this.Status.ValueEquals("Pending"u8);

    /// <summary>Gets a value indicating whether the runner's authorization has been revoked, compared string-free.</summary>
    public bool IsRevoked => this.Status.ValueEquals("Revoked"u8);

    /// <summary>Tests whether the authorization is in the given lifecycle state, string-free (no status string is realised) —
    /// the per-row status filter the in-memory and KV/table stores apply when scanning their keyset index.</summary>
    /// <param name="status">The status to test for.</param>
    /// <returns><see langword="true"/> if the authorization's status equals <paramref name="status"/>.</returns>
    public bool HasStatus(RunnerAuthorization.RunnerAuthorizationStatus status) => status switch
    {
        RunnerAuthorization.RunnerAuthorizationStatus.Pending => this.IsPending,
        RunnerAuthorization.RunnerAuthorizationStatus.Authorized => this.IsAuthorized,
        RunnerAuthorization.RunnerAuthorizationStatus.Revoked => this.IsRevoked,
        _ => false,
    };

    /// <summary>Tests whether this authorization's environment is the given one, string-free (no environment string is
    /// realised from the document — the candidate string's bytes are compared against the JSON value).</summary>
    /// <param name="environment">The environment to test for.</param>
    /// <returns><see langword="true"/> if the environments match.</returns>
    public bool EnvironmentEquals(string environment) => this.Environment.ValueEquals(environment);

    /// <summary>Gets the optional note recorded with the most recent decision, or <see langword="null"/>.</summary>
    public string? ReasonOrNull => this.Reason.IsNotUndefined() ? (string)this.Reason : null;

    /// <summary>Gets the actor that created the record (the runner self-registering enters Pending).</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the record was created.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets the environment administrator that last authorized or revoked the runner, or <see langword="null"/> if undecided.</summary>
    public string? DecidedByOrNull => this.DecidedBy.IsNotUndefined() ? (string)this.DecidedBy : null;

    /// <summary>Gets when the runner was last authorized or revoked, or <see langword="null"/> if undecided.</summary>
    public DateTimeOffset? DecidedAtValue => this.DecidedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.DecidedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Parses an authorization from its persisted JSON as a detached value. Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The authorization.</returns>
    public static EnvironmentRunnerAuthorization FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Builds a draft authorization from the create-content (environment + runnerId) for a store to complete with the
    /// server-stamped actor/etag/created metadata and the initial Pending status. The store reads only these content fields
    /// (bytes-to-bytes) and stamps the rest.</summary>
    /// <param name="environment">The environment the runner asks to serve.</param>
    /// <param name="runnerId">The runner the authorization applies to.</param>
    /// <returns>A pooled, disposable draft document the caller must dispose once the store/pipeline has read it.</returns>
    public static ParsedJsonDocument<EnvironmentRunnerAuthorization> Draft(string environment, string runnerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        var state = (environment, runnerId);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization, (string Environment, string RunnerId)>(
            in state,
            static (Utf8JsonWriter writer, in (string Environment, string RunnerId) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.EnvironmentUtf8, c.Environment);
                writer.WriteString(JsonPropertyNames.RunnerIdUtf8, c.RunnerId);
                writer.WriteEndObject();
            });
    }

    /// <summary>Realises a new (Pending) authorization into the caller's (pooled) writer in one pass — the draft's
    /// create-content is carried bytes-to-bytes and the status/server audit fields are stamped here.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="draft">The draft carrying the create-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor creating the record (the runner self-registering; audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in EnvironmentRunnerAuthorization draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireContent(draft);
        writer.WriteStartObject();
        WriteValue(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)draft.Environment);
        WriteValue(writer, JsonPropertyNames.RunnerIdUtf8, (JsonElement)draft.RunnerId);
        writer.WriteString(JsonPropertyNames.StatusUtf8, RunnerAuthorizationStatusNames.Pending);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Realises a decided copy of this authorization (status + decision fields set; everything else carried through)
    /// and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="decision">The decision to apply (terminal status + optional reason).</param>
    /// <param name="actor">The environment administrator deciding the authorization (audit).</param>
    /// <param name="decidedAt">The decision instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteDecision(Utf8JsonWriter writer, RunnerAuthorizationDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
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

    // A create draft must carry the target content (environment, runnerId).
    private static void RequireContent(in EnvironmentRunnerAuthorization draft)
    {
        if (!draft.Environment.IsNotUndefined() || !draft.RunnerId.IsNotUndefined())
        {
            throw new ArgumentException("An environment-runner authorization requires an 'environment' and 'runnerId'.", nameof(draft));
        }
    }

    // Realises a mutable builder over this document and applies the decision; environment/runnerId/created fields carry through.
    private JsonDocumentBuilder<Mutable> ApplyDecision(JsonWorkspace workspace, RunnerAuthorizationDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetStatus(RunnerAuthorizationStatusNames.ToWire(decision.Status));
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetDecidedAt(decidedAt);
        builder.RootElement.SetDecidedBy(actor);
        if (decision.Reason is { } reason)
        {
            builder.RootElement.SetReason(reason);
        }
        else
        {
            builder.RootElement.RemoveReason();
        }

        return builder;
    }
}
// <copyright file="NativeBuildJob.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A persisted Native-AOT build job (ADR 0055): the state of an asynchronous build of a workflow version's serverless
/// binary for one runtime target in one environment, plus the build's lifecycle state and audit/concurrency metadata. Its
/// identity is the target tuple (<c>baseWorkflowId</c>, <c>versionNumber</c>, <c>environment</c>, <c>runtimeIdentifier</c>),
/// from which the id is derived deterministically (<see cref="DeriveId"/>), so enqueuing is idempotent per target. Generated
/// from <c>Schemas/NativeBuildJob.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteClaimed"/>/<see cref="WriteCompletion"/>):
/// a store passes the buffer it owns and the job is realised and written in one pass, no interim detached clone. The leaf
/// accessors realise a <see cref="string"/> only where one is required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/NativeBuildJob.json")]
public readonly partial struct NativeBuildJob
{
    /// <summary>Gets the job's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the base workflow id whose version is being built.</summary>
    public string BaseWorkflowIdValue => (string)this.BaseWorkflowId;

    /// <summary>Gets the 1-based version number being built.</summary>
    public int VersionNumberValue => (int)this.VersionNumber;

    /// <summary>Gets the target environment.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the runtime identifier (RID) the Native-AOT binary targets.</summary>
    public string RuntimeIdentifierValue => (string)this.RuntimeIdentifier;

    /// <summary>Gets the job's lifecycle state as a realised <see cref="string"/> — for a display/log/serialize sink only.
    /// To <em>filter</em> on the status, use the string-free <see cref="HasStatus"/> predicate, which compares the JSON
    /// value's bytes and never allocates.</summary>
    public string StatusValue => (string)this.Status;

    /// <summary>Gets when the job was enqueued.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets when a worker started building the job, or <see langword="null"/> if not started.</summary>
    public DateTimeOffset? StartedAtValue => this.StartedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.StartedAt).ToDateTimeOffset() : null;

    /// <summary>Gets when the build completed, or <see langword="null"/> if not completed.</summary>
    public DateTimeOffset? CompletedAtValue => this.CompletedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.CompletedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the failure reason, or <see langword="null"/> if the build has not failed.</summary>
    public string? FailureReasonOrNull => this.FailureReason.IsNotUndefined() ? (string)this.FailureReason : null;

    /// <summary>Gets the optional human-friendly build label, or <see langword="null"/>.</summary>
    public string? BuildLabelOrNull => this.BuildLabel.IsNotUndefined() ? (string)this.BuildLabel : null;

    /// <summary>Gets the worker that claimed the job, or <see langword="null"/> if unclaimed.</summary>
    public string? ClaimedByOrNull => this.ClaimedBy.IsNotUndefined() ? (string)this.ClaimedBy : null;

    /// <summary>Gets the instant the claiming worker's advisory lease on this Building job expires, or
    /// <see langword="null"/> if no lease is held (ADR 0056).</summary>
    public DateTimeOffset? LeaseExpiresAtValue => this.LeaseExpiresAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.LeaseExpiresAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Tests whether the job is in the given lifecycle state, string-free (no status string is realised) — the
    /// per-row status filter the in-memory and KV/table stores apply when scanning their keyset index. The u8 literals
    /// mirror the schema's status enum (see <see cref="NativeBuildJobStatusNames"/>).</summary>
    /// <param name="status">The status to test for.</param>
    /// <returns><see langword="true"/> if the job's status equals <paramref name="status"/>.</returns>
    public bool HasStatus(NativeBuildJobStatus status) => status switch
    {
        NativeBuildJobStatus.Queued => this.Status.ValueEquals("Queued"u8),
        NativeBuildJobStatus.Building => this.Status.ValueEquals("Building"u8),
        NativeBuildJobStatus.Ready => this.Status.ValueEquals("Ready"u8),
        NativeBuildJobStatus.Failed => this.Status.ValueEquals("Failed"u8),
        _ => false,
    };

    /// <summary>Tests whether this job holds a live (unexpired) advisory lease as of <paramref name="now"/> — the claiming
    /// worker is presumed alive (ADR 0056). A Building job with no <c>leaseExpiresAt</c> holds no live lease.</summary>
    /// <param name="now">The current instant to test the lease against.</param>
    /// <returns><see langword="true"/> if a lease is held and has not expired.</returns>
    public bool HasLiveLease(DateTimeOffset now) => this.LeaseExpiresAtValue is { } expires && expires > now;

    /// <summary>Tests whether this job may be claimed as of <paramref name="now"/>: it is Queued, or it is Building but holds
    /// no live lease (an orphan left by a crashed worker, reclaimable exactly as a run with no live lease is — ADR 0056),
    /// string-free on the status.</summary>
    /// <param name="now">The current instant to test the lease against.</param>
    /// <returns><see langword="true"/> if the job is claimable.</returns>
    public bool IsClaimable(DateTimeOffset now)
        => this.HasStatus(NativeBuildJobStatus.Queued)
        || (this.HasStatus(NativeBuildJobStatus.Building) && !this.HasLiveLease(now));

    /// <summary>Tests whether this job targets the given base workflow id, string-free (no id string is realised from the
    /// document — the candidate string's bytes are compared against the JSON value).</summary>
    /// <param name="baseWorkflowId">The base workflow id to test for.</param>
    /// <returns><see langword="true"/> if the base workflow ids match.</returns>
    public bool BaseWorkflowIdEquals(string baseWorkflowId) => this.BaseWorkflowId.ValueEquals(baseWorkflowId);

    /// <summary>Tests whether this job targets the given version number.</summary>
    /// <param name="versionNumber">The version number to test for.</param>
    /// <returns><see langword="true"/> if the version numbers match.</returns>
    public bool VersionNumberEquals(int versionNumber) => this.VersionNumberValue == versionNumber;

    /// <summary>Tests whether this job's target environment is the given one, string-free (no environment string is realised
    /// from the document — the candidate string's bytes are compared against the JSON value).</summary>
    /// <param name="environment">The environment to test for.</param>
    /// <returns><see langword="true"/> if the environments match.</returns>
    public bool EnvironmentEquals(string environment) => this.Environment.ValueEquals(environment);

    /// <summary>Tests whether this job targets the given runtime identifier, string-free (no RID string is realised from the
    /// document — the candidate string's bytes are compared against the JSON value).</summary>
    /// <param name="runtimeIdentifier">The runtime identifier to test for.</param>
    /// <returns><see langword="true"/> if the runtime identifiers match.</returns>
    public bool RuntimeIdentifierEquals(string runtimeIdentifier) => this.RuntimeIdentifier.ValueEquals(runtimeIdentifier);

    /// <summary>Tests whether this job's target is the given tuple, string-free for the string parts (no field is realised
    /// from the document per compare; the version number compares as an integer value) — the per-row target filter.</summary>
    /// <param name="baseWorkflowId">The base workflow id to test for.</param>
    /// <param name="versionNumber">The version number to test for.</param>
    /// <param name="environment">The environment to test for.</param>
    /// <param name="runtimeIdentifier">The runtime identifier to test for.</param>
    /// <returns><see langword="true"/> if the job targets the given tuple.</returns>
    public bool MatchesTarget(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
        => this.BaseWorkflowId.ValueEquals(baseWorkflowId)
        && this.VersionNumberValue == versionNumber
        && this.Environment.ValueEquals(environment)
        && this.RuntimeIdentifier.ValueEquals(runtimeIdentifier);

    /// <summary>Derives a job's deterministic id from its target tuple: the same target always maps to the same id, so
    /// enqueuing is idempotent per target (a rebuild resets the same job). The shape is
    /// <c>{baseWorkflowId}-v{versionNumber}-{environment}-{runtimeIdentifier}</c>.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="runtimeIdentifier">The runtime identifier.</param>
    /// <returns>The deterministic job id for the target.</returns>
    public static string DeriveId(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        return string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}-v{versionNumber}-{environment}-{runtimeIdentifier}");
    }

    /// <summary>Parses a job from its persisted JSON as a detached value. Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The job.</returns>
    public static NativeBuildJob FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Builds a draft Queued job from the target-content (the tuple + optional build label) for a store to complete
    /// with the server-stamped id/etag/created metadata and the initial Queued status. The store reads only these content
    /// fields (bytes-to-bytes) and stamps the rest.</summary>
    /// <param name="baseWorkflowId">The base workflow id whose version is being built.</param>
    /// <param name="versionNumber">The 1-based version number being built.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="runtimeIdentifier">The runtime identifier the binary targets.</param>
    /// <param name="buildLabel">An optional human-friendly build label.</param>
    /// <returns>A pooled, disposable draft document the caller must dispose once the store/pipeline has read it.</returns>
    public static ParsedJsonDocument<NativeBuildJob> Draft(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, string? buildLabel = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // The generated Create() writes the document text and its parse metadata in one pass — no serialize-then-reparse
        // round trip. The draft carries only the target-content: a default Source is omitted from the document, so the
        // server-stamped fields (id/status/createdAt/etag) stay absent for WriteNew to stamp.
        return Create(
            baseWorkflowId: baseWorkflowId,
            createdAt: default,
            environment: environment,
            etag: default,
            id: default,
            runtimeIdentifier: runtimeIdentifier,
            status: default,
            versionNumber: versionNumber,
            buildLabel: buildLabel is { } l ? (JsonString.Source)l : default);
    }

    /// <summary>Realises a brand-new Queued job as a self-contained pooled document in one pass — the
    /// <see cref="ParsedJsonDocument{T}"/>-producing counterpart of <see cref="WriteNew"/> for drivers that consume the
    /// parsed document. Same content requirement and field mapping as the writer path below; keep the two in step.</summary>
    /// <param name="id">The assigned job id.</param>
    /// <param name="draft">The draft job carrying the target content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<NativeBuildJob> CreateNew(string id, in NativeBuildJob draft, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireContent(draft);
        return Create(
            baseWorkflowId: draft.BaseWorkflowId,
            createdAt: createdAt,
            environment: draft.Environment,
            etag: etag.Value ?? string.Empty,
            id: id,
            runtimeIdentifier: draft.RuntimeIdentifier,
            status: NativeBuildJobStatusNames.Queued,
            versionNumber: draft.VersionNumber,
            buildLabel: draft.BuildLabel.IsNotUndefined() ? (JsonString.Source)draft.BuildLabel : default);
    }

    /// <summary>Realises a new (Queued) job into the caller's (pooled) writer in one pass — the draft's target-content is
    /// carried bytes-to-bytes and the id/status/created fields are stamped here.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="id">The job id.</param>
    /// <param name="draft">The draft carrying the target-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, in NativeBuildJob draft, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireContent(draft);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        WriteValue(writer, JsonPropertyNames.BaseWorkflowIdUtf8, (JsonElement)draft.BaseWorkflowId);
        WriteValue(writer, JsonPropertyNames.VersionNumberUtf8, (JsonElement)draft.VersionNumber);
        WriteValue(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)draft.Environment);
        WriteValue(writer, JsonPropertyNames.RuntimeIdentifierUtf8, (JsonElement)draft.RuntimeIdentifier);
        if (draft.BuildLabel.IsNotUndefined())
        {
            WriteValue(writer, JsonPropertyNames.BuildLabelUtf8, (JsonElement)draft.BuildLabel);
        }

        writer.WriteString(JsonPropertyNames.StatusUtf8, NativeBuildJobStatusNames.Queued);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Realises a claimed (Building) copy of this job (status + claimedBy + startedAt + leaseExpiresAt set;
    /// everything else carried through) and writes its JSON to the caller's (pooled) writer — the Queued -> Building
    /// transition (also the reclaim of an orphaned Building job, which resets startedAt and the lease).</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="claimedBy">The worker claiming the job.</param>
    /// <param name="startedAt">The instant the build started.</param>
    /// <param name="leaseExpiresAt">The instant the claiming worker's advisory lease expires (ADR 0056).</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteClaimed(Utf8JsonWriter writer, string claimedBy, DateTimeOffset startedAt, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyClaimed(workspace, claimedBy, startedAt, leaseExpiresAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Realises a lease-renewed copy of this Building job (leaseExpiresAt + etag set; status/claimedBy/startedAt and
    /// everything else carried through) and writes its JSON to the caller's (pooled) writer — the heartbeat that keeps a
    /// running build's lease live so no other worker reclaims it (ADR 0056).</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="leaseExpiresAt">The new instant the lease expires.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteRenewedLease(Utf8JsonWriter writer, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyRenewedLease(workspace, leaseExpiresAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Realises a completed copy of this job (status + completedAt set, failureReason set or removed; everything
    /// else carried through) and writes its JSON to the caller's (pooled) writer — the Building -> Ready | Failed
    /// transition.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="completion">The completion to apply (terminal status + optional failure reason).</param>
    /// <param name="completedAt">The completion instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteCompletion(Utf8JsonWriter writer, NativeBuildJobCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyCompletion(workspace, completion, completedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    // Copies a draft property to the writer bytes-to-bytes.
    private static void WriteValue(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        writer.WritePropertyName(name);
        value.WriteTo(writer);
    }

    // A create draft must carry the target content (baseWorkflowId, versionNumber, environment, runtimeIdentifier).
    private static void RequireContent(in NativeBuildJob draft)
    {
        if (!draft.BaseWorkflowId.IsNotUndefined() || !draft.VersionNumber.IsNotUndefined() || !draft.Environment.IsNotUndefined() || !draft.RuntimeIdentifier.IsNotUndefined())
        {
            ThrowHelper.ThrowNativeBuildJobRequiresContent();
        }
    }

    // Realises a mutable builder over this document and applies the claim; id/created/target fields carry through.
    private JsonDocumentBuilder<Mutable> ApplyClaimed(JsonWorkspace workspace, string claimedBy, DateTimeOffset startedAt, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetStatus(NativeBuildJobStatusNames.Building);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetClaimedBy(claimedBy);
        builder.RootElement.SetStartedAt(startedAt);
        builder.RootElement.SetLeaseExpiresAt(leaseExpiresAt);
        return builder;
    }

    // Realises a mutable builder over this document and extends the lease; status/claimedBy/startedAt and everything else carry through.
    private JsonDocumentBuilder<Mutable> ApplyRenewedLease(JsonWorkspace workspace, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetLeaseExpiresAt(leaseExpiresAt);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        return builder;
    }

    // Realises a mutable builder over this document and applies the completion; id/created/target fields carry through.
    private JsonDocumentBuilder<Mutable> ApplyCompletion(JsonWorkspace workspace, NativeBuildJobCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetStatus(NativeBuildJobStatusNames.ToWire(completion.Status));
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetCompletedAt(completedAt);
        if (completion.FailureReason is { } reason)
        {
            builder.RootElement.SetFailureReason(reason);
        }
        else
        {
            builder.RootElement.RemoveFailureReason();
        }

        return builder;
    }
}
// <copyright file="AccessRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted access request (design §16.5): a principal's request for elevated capability (e.g. <c>runs:write</c>)
/// on a workflow, the requesting subject the eventual grant keys on, plus the decision state and audit/concurrency
/// metadata. Generated from <c>Schemas/AccessRequest.json</c> and used as the domain value <em>and</em> the persisted
/// form.
/// </summary>
/// <remarks>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteDecision"/>): a store passes
/// the buffer it owns and the request is realised and written in one pass — no interim detached clone. The leaf
/// accessors realise a <see cref="string"/> only where one is required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/AccessRequest.json")]
public readonly partial struct AccessRequest
{
    /// <summary>Gets the request's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the base workflow id the request targets.</summary>
    public string BaseWorkflowIdValue => (string)this.BaseWorkflowId;

    /// <summary>Gets the principal claim type the granted entitlement keys on (e.g. <c>sub</c>).</summary>
    public string SubjectClaimTypeValue => (string)this.SubjectClaimType;

    /// <summary>Gets the requester's value for <see cref="SubjectClaimTypeValue"/>.</summary>
    public string SubjectClaimValueValue => (string)this.SubjectClaimValue;

    /// <summary>Gets the optional human-friendly requester label, or <see langword="null"/>.</summary>
    public string? RequesterLabelOrNull => this.RequesterLabel.IsNotUndefined() ? (string)this.RequesterLabel : null;

    /// <summary>Gets the optional justification, or <see langword="null"/>.</summary>
    public string? ReasonOrNull => this.Reason.IsNotUndefined() ? (string)this.Reason : null;

    /// <summary>Gets the proposed time-bound (PIM) duration in seconds, or <see langword="null"/> (default to the deployment max TTL).</summary>
    public long? RequestedDurationSecondsOrNull => this.RequestedDurationSeconds.IsNotUndefined() ? (long)this.RequestedDurationSeconds : null;

    /// <summary>Gets when the granted entitlement expires, or <see langword="null"/> (undecided, or a standing grant).</summary>
    public DateTimeOffset? GrantedUntilValue => this.GrantedUntil.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.GrantedUntil).ToDateTimeOffset() : null;

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

    /// <summary>Gets the id of the security-policy binding written on approval, or <see langword="null"/>.</summary>
    public string? GrantedBindingIdOrNull => this.GrantedBindingId.IsNotUndefined() ? (string)this.GrantedBindingId : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Materialises the requested capability scopes as an array.</summary>
    /// <returns>The requested scopes.</returns>
    public string[] RequestedScopesArray()
    {
        int count = this.RequestedScopes.GetArrayLength();
        if (count == 0)
        {
            return [];
        }

        var result = new string[count];
        int i = 0;
        foreach (JsonString scope in this.RequestedScopes.EnumerateArray())
        {
            result[i++] = (string)scope;
        }

        return result;
    }

    /// <summary>Parses a request from its persisted JSON as a detached value. Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The request.</returns>
    public static AccessRequest FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Realises a new (Pending) request into a workspace and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="id">The request id.</param>
    /// <param name="draft">The draft request carrying the create-content (request + subject) as JSON values — read bytes-to-bytes.</param>
    /// <param name="actor">The actor (requester) creating the request (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, in AccessRequest draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = BuildNew(workspace, id, draft, actor, createdAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>
    /// Builds a draft request from the create-content (the requested workflow/scopes/duration/reason and the resolved
    /// subject) for a store to complete with the server-stamped id/etag/created metadata and the initial Pending status.
    /// The store reads only these content fields (bytes-to-bytes) and stamps the rest. A handler supplies the body
    /// fields and the principal-derived subject; the eligibility predicate + approval pipeline read the draft directly.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id the request targets.</param>
    /// <param name="requestedScopes">The capability scopes requested (at least one).</param>
    /// <param name="subjectClaimType">The principal claim type the eventual grant keys on.</param>
    /// <param name="subjectClaimValue">The requester's value for <paramref name="subjectClaimType"/>.</param>
    /// <param name="requesterLabel">An optional human-friendly requester label.</param>
    /// <param name="reason">An optional justification.</param>
    /// <param name="requestedDurationSeconds">The optional proposed time-bound (PIM) duration in seconds.</param>
    /// <returns>A pooled, disposable draft document the caller must dispose once the store/pipeline has read it.</returns>
    public static ParsedJsonDocument<AccessRequest> Draft(
        string baseWorkflowId,
        IReadOnlyList<string> requestedScopes,
        string subjectClaimType,
        string subjectClaimValue,
        string? requesterLabel = null,
        string? reason = null,
        long? requestedDurationSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(requestedScopes);
        var state = new DraftState(baseWorkflowId, requestedScopes, subjectClaimType, subjectClaimValue, requesterLabel, reason, requestedDurationSeconds);
        return PersistedJson.ToPooledDocument<AccessRequest, DraftState>(
            in state,
            static (Utf8JsonWriter writer, in DraftState c) =>
            {
                writer.WriteStartObject();
                writer.WriteString("baseWorkflowId"u8, c.BaseWorkflowId);
                writer.WriteStartArray("requestedScopes"u8);
                foreach (string scope in c.RequestedScopes)
                {
                    writer.WriteStringValue(scope);
                }

                writer.WriteEndArray();
                writer.WriteString("subjectClaimType"u8, c.SubjectClaimType);
                writer.WriteString("subjectClaimValue"u8, c.SubjectClaimValue);
                if (c.RequesterLabel is { } requesterLabel)
                {
                    writer.WriteString("requesterLabel"u8, requesterLabel);
                }

                if (c.Reason is { } reason)
                {
                    writer.WriteString("reason"u8, reason);
                }

                if (c.RequestedDurationSeconds is { } duration)
                {
                    writer.WriteNumber("requestedDurationSeconds"u8, duration);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Builds a draft Pending request carrying the request body's already-parsed JSON values bytes-to-bytes —
    /// the warm HTTP path: <paramref name="baseWorkflowId"/>, <paramref name="requestedScopes"/> (the whole array, no
    /// intermediate <see cref="System.Collections.Generic.List{T}"/> of managed strings) and <paramref name="reason"/>
    /// are copied straight from the body, while the principal-derived subject/label are carried as the strings they
    /// already are. The store stamps id/etag/createdAt; the eligibility predicate and <c>SubmitAsync</c> read the draft.</summary>
    /// <param name="baseWorkflowId">The target workflow id, as the body's JSON value.</param>
    /// <param name="requestedScopes">The requested scopes array, as the body's JSON value (copied verbatim).</param>
    /// <param name="subjectClaimType">The principal's subject claim type (the grant is scoped to it).</param>
    /// <param name="subjectClaimValue">The principal's subject claim value.</param>
    /// <param name="requesterLabel">An optional human-facing requester label (the authentication name).</param>
    /// <param name="reason">An optional reason, as the body's JSON value (skipped when undefined).</param>
    /// <param name="requestedDurationSeconds">An optional requested grant duration.</param>
    /// <returns>The pooled draft document (the caller disposes it).</returns>
    public static ParsedJsonDocument<AccessRequest> Draft(
        in JsonElement baseWorkflowId,
        in JsonElement requestedScopes,
        string subjectClaimType,
        string subjectClaimValue,
        string? requesterLabel,
        in JsonElement reason,
        long? requestedDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(subjectClaimType);
        ArgumentNullException.ThrowIfNull(subjectClaimValue);
        var state = new DraftElementState(baseWorkflowId, requestedScopes, subjectClaimType, subjectClaimValue, requesterLabel, reason, requestedDurationSeconds);
        return PersistedJson.ToPooledDocument<AccessRequest, DraftElementState>(
            in state,
            static (Utf8JsonWriter writer, in DraftElementState c) =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("baseWorkflowId"u8);
                c.BaseWorkflowId.WriteTo(writer);
                writer.WritePropertyName("requestedScopes"u8);
                c.RequestedScopes.WriteTo(writer);
                writer.WriteString("subjectClaimType"u8, c.SubjectClaimType);
                writer.WriteString("subjectClaimValue"u8, c.SubjectClaimValue);
                if (c.RequesterLabel is { } requesterLabel)
                {
                    writer.WriteString("requesterLabel"u8, requesterLabel);
                }

                if (c.Reason.ValueKind != JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("reason"u8);
                    c.Reason.WriteTo(writer);
                }

                if (c.RequestedDurationSeconds is { } duration)
                {
                    writer.WriteNumber("requestedDurationSeconds"u8, duration);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Realises a decided copy of this request (status + decision fields set; everything else carried through)
    /// and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="decision">The decision to apply (terminal status + optional reason/grant/expiry).</param>
    /// <param name="actor">The actor deciding the request (audit).</param>
    /// <param name="decidedAt">The decision instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteDecision(Utf8JsonWriter writer, AccessRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyDecision(workspace, decision, actor, decidedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    // Realises a new Pending request into the pooled workspace arena: the draft's create-content is carried
    // bytes-to-bytes (its JSON values — including the requestedScopes array — flow straight into the builder); id, the
    // initial Pending status, and the server-stamped audit/concurrency fields are added here.
    private static JsonDocumentBuilder<Mutable> BuildNew(JsonWorkspace workspace, string id, in AccessRequest draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => CreateBuilder(
            workspace,
            baseWorkflowId: draft.BaseWorkflowId,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            id: id,
            requestedScopes: draft.RequestedScopes,
            status: AccessRequestStatusNames.Pending,
            subjectClaimType: draft.SubjectClaimType,
            subjectClaimValue: draft.SubjectClaimValue,
            reason: draft.Reason.IsNotUndefined() ? (JsonString.Source)draft.Reason : default,
            requesterLabel: draft.RequesterLabel.IsNotUndefined() ? (JsonString.Source)draft.RequesterLabel : default,
            requestedDurationSeconds: draft.RequestedDurationSeconds.IsNotUndefined() ? (RequestedDurationSecondsEntity.Source)draft.RequestedDurationSeconds : default);

    // The create-content of a draft request, threaded through the pooled-writer callback (a static lambda, no closure).
    private readonly struct DraftState(
        string baseWorkflowId,
        IReadOnlyList<string> requestedScopes,
        string subjectClaimType,
        string subjectClaimValue,
        string? requesterLabel,
        string? reason,
        long? requestedDurationSeconds)
    {
        public string BaseWorkflowId { get; } = baseWorkflowId;

        public IReadOnlyList<string> RequestedScopes { get; } = requestedScopes;

        public string SubjectClaimType { get; } = subjectClaimType;

        public string SubjectClaimValue { get; } = subjectClaimValue;

        public string? RequesterLabel { get; } = requesterLabel;

        public string? Reason { get; } = reason;

        public long? RequestedDurationSeconds { get; } = requestedDurationSeconds;
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values (baseWorkflowId/requestedScopes/
    // reason) plus the principal-derived subject/label strings.
    private readonly struct DraftElementState(
        JsonElement baseWorkflowId,
        JsonElement requestedScopes,
        string subjectClaimType,
        string subjectClaimValue,
        string? requesterLabel,
        JsonElement reason,
        long? requestedDurationSeconds)
    {
        public JsonElement BaseWorkflowId { get; } = baseWorkflowId;

        public JsonElement RequestedScopes { get; } = requestedScopes;

        public string SubjectClaimType { get; } = subjectClaimType;

        public string SubjectClaimValue { get; } = subjectClaimValue;

        public string? RequesterLabel { get; } = requesterLabel;

        public JsonElement Reason { get; } = reason;

        public long? RequestedDurationSeconds { get; } = requestedDurationSeconds;
    }

    // Realises a mutable builder over this document and applies the decision; id/created/request fields carry through.
    private JsonDocumentBuilder<Mutable> ApplyDecision(JsonWorkspace workspace, AccessRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetStatus(AccessRequestStatusNames.ToWire(decision.Status));
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetDecidedAt(decidedAt);
        builder.RootElement.SetDecidedBy(actor);
        SetOrRemove(builder, decision);
        return builder;
    }

    // The decision's optional fields, set when present and removed when absent (so a re-decision clears stale values).
    private static void SetOrRemove(JsonDocumentBuilder<Mutable> builder, AccessRequestDecision decision)
    {
        if (decision.DecisionReason is { } reason)
        {
            builder.RootElement.SetDecisionReason(reason);
        }
        else
        {
            builder.RootElement.RemoveDecisionReason();
        }

        if (decision.GrantedBindingId is { } bindingId)
        {
            builder.RootElement.SetGrantedBindingId(bindingId);
        }
        else
        {
            builder.RootElement.RemoveGrantedBindingId();
        }

        if (decision.GrantedUntil is { } grantedUntil)
        {
            builder.RootElement.SetGrantedUntil(grantedUntil);
        }
        else
        {
            builder.RootElement.RemoveGrantedUntil();
        }
    }
}
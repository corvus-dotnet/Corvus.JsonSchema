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
    /// <param name="definition">The request content.</param>
    /// <param name="actor">The actor (requester) creating the request (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, AccessRequestDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = BuildNew(workspace, id, definition, actor, createdAt, etag);
        builder.RootElement.WriteTo(writer);
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

    // Realises a new Pending request into the pooled workspace arena. requestedScopes is the only required array; it is
    // built through a small capturing callback (this is the cold write path — CreateAsync — not the warm read path).
    private static JsonDocumentBuilder<Mutable> BuildNew(JsonWorkspace workspace, string id, AccessRequestDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => CreateBuilder(
            workspace,
            baseWorkflowId: definition.BaseWorkflowId,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            id: id,
            requestedScopes: JsonStringArray.Build((ref JsonStringArray.Builder array) =>
            {
                foreach (string scope in definition.RequestedScopes)
                {
                    array.AddItem(scope);
                }
            }),
            status: AccessRequestStatusNames.Pending,
            subjectClaimType: definition.SubjectClaimType,
            subjectClaimValue: definition.SubjectClaimValue,
            reason: definition.Reason is { } reason ? (JsonString.Source)reason : default,
            requesterLabel: definition.RequesterLabel is { } label ? (JsonString.Source)label : default,
            requestedDurationSeconds: definition.RequestedDurationSeconds is { } duration ? (RequestedDurationSecondsEntity.Source)duration : default);

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
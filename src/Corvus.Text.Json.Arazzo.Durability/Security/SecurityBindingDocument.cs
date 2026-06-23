// <copyright file="SecurityBindingDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted claim→rule binding (design §14.2): the per-verb grants (read/write/purge) that a principal carrying
/// a matching claim resolves to, plus audit/concurrency metadata. This is the single binding type — generated from
/// <c>Schemas/SecurityBindingDocument.json</c> — used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// Construction threads the destination through: a store passes the buffer (or stream) it already owns and the
/// binding is realised and written into it in one pass (<see cref="WriteNewBinding"/>/<see cref="WriteUpdatedBinding"/>)
/// — no interim detached clone, no second serialization, and no array copied out of a hidden buffer. The backend
/// consumes that span directly, and where the value is needed back it is parsed once (<see cref="FromJson"/>). The
/// leaf accessors realise a <see cref="string"/> only where one is actually required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/SecurityBindingDocument.json")]
public readonly partial struct SecurityBindingDocument
{
    /// <summary>Gets the binding's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the principal claim type this binding keys on (<c>"*"</c> = any authenticated).</summary>
    public string ClaimTypeValue => (string)this.ClaimType;

    /// <summary>Gets the required claim value, or <see langword="null"/> (matches any value).</summary>
    public string? ClaimValueOrNull => this.ClaimValue.IsNotUndefined() ? (string)this.ClaimValue : null;

    /// <summary>Gets the resolution order (ascending).</summary>
    public int OrderValue => this.Order;

    /// <summary>Gets the optional human description, or <see langword="null"/>.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the actor that created the binding.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the binding was created.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets the actor that last updated the binding, or <see langword="null"/>.</summary>
    public string? UpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets when the binding was last updated, or <see langword="null"/>.</summary>
    public DateTimeOffset? UpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.LastUpdatedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets when this grant expires (design §16.5.2), or <see langword="null"/> for a standing grant.</summary>
    public DateTimeOffset? ExpiresAtValue => this.ExpiresAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.ExpiresAt).ToDateTimeOffset() : null;

    /// <summary>Gets a value indicating whether this is an eligibility assignment (design §16.5.3/§16.5.4) — ignored by the resolver, read by the self-elevation strategy — rather than an active grant.</summary>
    public bool EligibleOnlyValue => this.EligibleOnly.IsNotUndefined() && (bool)this.EligibleOnly;

    /// <summary>Materialises the granted capability scopes (design §14.1) as an array — empty when the binding grants
    /// no capability (a reach-only binding). Used at snapshot-compile time (per generation), off the request hot path.</summary>
    /// <returns>The granted scopes, or an empty array.</returns>
    public string[] ScopesArray()
    {
        if (!this.Scopes.IsNotUndefined())
        {
            return [];
        }

        int count = this.Scopes.GetArrayLength();
        if (count == 0)
        {
            return [];
        }

        var result = new string[count];
        int i = 0;
        foreach (JsonString scope in this.Scopes.EnumerateArray())
        {
            result[i++] = (string)scope;
        }

        return result;
    }

    /// <summary>Realises a new binding into a workspace and writes its JSON to the caller's (pooled) writer in one pass —
    /// a store serializes the result to its driver bytes via <see cref="PersistedJson.ToArray"/> and returns a pooled
    /// document over those bytes.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="id">The binding id.</param>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values — read bytes-to-bytes.</param>
    /// <param name="actor">The actor creating the binding (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, in SecurityBindingDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = BuildNew(workspace, id, draft, actor, createdAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Realises an updated copy of this binding (modifying only the fields the update touches; id/created
    /// metadata carried through) and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft binding carrying the new operator-supplied content as JSON values — read bytes-to-bytes.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in SecurityBindingDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyUpdate(workspace, draft, actor, updatedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Parses a binding from its persisted JSON as a detached value (one owned copy). Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The binding.</returns>
    public static SecurityBindingDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>
    /// Builds a draft binding from operator-supplied content for a store to complete with the server-stamped
    /// id/etag/created metadata — the programmatic counterpart of carrying an HTTP request body as the draft. The store
    /// reads only the content fields (bytes-to-bytes) and stamps the rest.
    /// </summary>
    /// <param name="claimType">The principal claim type the binding keys on (<c>"*"</c> = any authenticated).</param>
    /// <param name="claimValue">The required claim value, or <see langword="null"/> (matches any value).</param>
    /// <param name="read">The read-verb grant.</param>
    /// <param name="write">The write-verb grant.</param>
    /// <param name="purge">The purge-verb grant.</param>
    /// <param name="order">The resolution order (ascending).</param>
    /// <param name="description">An optional human description.</param>
    /// <param name="scopes">The capability scopes granted (design §14.1); <see langword="null"/>/empty is reach-only.</param>
    /// <param name="expiresAt">When the grant expires (design §16.5.2); <see langword="null"/> is a standing grant.</param>
    /// <param name="eligibleOnly">When <see langword="true"/>, an eligibility assignment (design §16.5.3/§16.5.4), not an active grant.</param>
    /// <returns>A pooled, disposable draft document the caller must dispose once the store has read it.</returns>
    public static ParsedJsonDocument<SecurityBindingDocument> Draft(
        string claimType,
        string? claimValue,
        VerbGrantInfo read,
        VerbGrantInfo write,
        VerbGrantInfo purge,
        int order = 0,
        string? description = null,
        IReadOnlyList<string>? scopes = null,
        DateTimeOffset? expiresAt = null,
        bool eligibleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(claimType);
        var state = new DraftState(claimType, claimValue, read, write, purge, order, description, scopes, expiresAt, eligibleOnly);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument, DraftState>(
            in state,
            static (Utf8JsonWriter writer, in DraftState c) =>
            {
                writer.WriteStartObject();
                writer.WriteString("claimType"u8, c.ClaimType);
                if (c.ClaimValue is { } claimValue)
                {
                    writer.WriteString("claimValue"u8, claimValue);
                }

                writer.WritePropertyName("read"u8);
                c.Read.WriteTo(writer);
                writer.WritePropertyName("write"u8);
                c.Write.WriteTo(writer);
                writer.WritePropertyName("purge"u8);
                c.Purge.WriteTo(writer);
                writer.WriteNumber("order"u8, c.Order);
                if (c.Description is { } description)
                {
                    writer.WriteString("description"u8, description);
                }

                if (c.Scopes is { Count: > 0 } scopes)
                {
                    writer.WriteStartArray("scopes"u8);
                    foreach (string scope in scopes)
                    {
                        writer.WriteStringValue(scope);
                    }

                    writer.WriteEndArray();
                }

                if (c.ExpiresAt is { } expiresAt)
                {
                    writer.WriteString("expiresAt"u8, expiresAt);
                }

                if (c.EligibleOnly)
                {
                    writer.WriteBoolean("eligibleOnly"u8, true);
                }

                writer.WriteEndObject();
            });
    }

    // Realises a new binding into the pooled workspace arena: the draft's operator content is carried bytes-to-bytes (its
    // JSON values flow straight into the builder); id and the server-stamped audit/concurrency fields are added here.
    private static JsonDocumentBuilder<Mutable> BuildNew(JsonWorkspace workspace, string id, in SecurityBindingDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        // The draft may be a request body that omits verb grants / order (both optional on the write body but required on
        // the stored document); default an omitted grant to None and an omitted order to 0 — the semantics the handler's
        // Models.VerbGrant -> VerbGrantInfo conversion used to apply before the body was carried bytes-to-bytes.
        VerbGrantInfo read = draft.Read.IsNotUndefined() ? draft.Read : VerbGrantInfo.None;
        VerbGrantInfo write = draft.Write.IsNotUndefined() ? draft.Write : VerbGrantInfo.None;
        VerbGrantInfo purge = draft.Purge.IsNotUndefined() ? draft.Purge : VerbGrantInfo.None;
        int order = draft.Order.IsNotUndefined() ? (int)draft.Order : 0;
        return CreateBuilder(
            workspace,
            claimType: draft.ClaimType,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            id: id,
            order: order,
            purge: purge,
            read: read,
            write: write,
            claimValue: draft.ClaimValue.IsNotUndefined() ? (JsonString.Source)draft.ClaimValue : default,
            description: draft.Description.IsNotUndefined() ? (JsonString.Source)draft.Description : default,
            expiresAt: draft.ExpiresAt.IsNotUndefined() ? (JsonDateTime.Source)draft.ExpiresAt : default,
            eligibleOnly: draft.EligibleOnly.IsNotUndefined() ? (JsonBoolean.Source)draft.EligibleOnly : default,
            scopes: draft.Scopes.IsNotUndefined() ? (JsonStringArray.Source)draft.Scopes : default);
    }

    // Realises a mutable builder over this document and modifies only the fields an update touches, carrying the draft's
    // content bytes-to-bytes; id and the created-* metadata are carried through unchanged (no field-by-field rebuild).
    private JsonDocumentBuilder<Mutable> ApplyUpdate(JsonWorkspace workspace, in SecurityBindingDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetClaimType(draft.ClaimType);

        // As BuildNew: a draft carried from the (optional-field) write body may omit verb grants / order — default an
        // omitted grant to None and an omitted order to 0 so the replaced binding stays valid (the PUT-replaces semantics).
        builder.RootElement.SetOrder(draft.Order.IsNotUndefined() ? (int)draft.Order : 0);
        builder.RootElement.SetRead(draft.Read.IsNotUndefined() ? draft.Read : VerbGrantInfo.None);
        builder.RootElement.SetWrite(draft.Write.IsNotUndefined() ? draft.Write : VerbGrantInfo.None);
        builder.RootElement.SetPurge(draft.Purge.IsNotUndefined() ? draft.Purge : VerbGrantInfo.None);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetLastUpdatedAt(updatedAt);
        builder.RootElement.SetLastUpdatedBy(actor);
        if (draft.ClaimValue.IsNotUndefined())
        {
            builder.RootElement.SetClaimValue(draft.ClaimValue);
        }
        else
        {
            builder.RootElement.RemoveClaimValue();
        }

        if (draft.Description.IsNotUndefined())
        {
            builder.RootElement.SetDescription(draft.Description);
        }
        else
        {
            builder.RootElement.RemoveDescription();
        }

        if (draft.ExpiresAt.IsNotUndefined())
        {
            builder.RootElement.SetExpiresAt(draft.ExpiresAt);
        }
        else
        {
            builder.RootElement.RemoveExpiresAt();
        }

        if (draft.EligibleOnly.IsNotUndefined())
        {
            builder.RootElement.SetEligibleOnly(draft.EligibleOnly);
        }
        else
        {
            builder.RootElement.RemoveEligibleOnly();
        }

        if (draft.Scopes.IsNotUndefined())
        {
            builder.RootElement.SetScopes(draft.Scopes);
        }
        else
        {
            builder.RootElement.RemoveScopes();
        }

        return builder;
    }

    // The operator-supplied content of a draft binding, threaded through the pooled-writer callback (a static lambda, no
    // closure). Holds the verb grants by value (each a thin Corvus.Text.Json handle) plus the scalar/optional fields.
    private readonly struct DraftState(
        string claimType,
        string? claimValue,
        VerbGrantInfo read,
        VerbGrantInfo write,
        VerbGrantInfo purge,
        int order,
        string? description,
        IReadOnlyList<string>? scopes,
        DateTimeOffset? expiresAt,
        bool eligibleOnly)
    {
        public string ClaimType { get; } = claimType;

        public string? ClaimValue { get; } = claimValue;

        public VerbGrantInfo Read { get; } = read;

        public VerbGrantInfo Write { get; } = write;

        public VerbGrantInfo Purge { get; } = purge;

        public int Order { get; } = order;

        public string? Description { get; } = description;

        public IReadOnlyList<string>? Scopes { get; } = scopes;

        public DateTimeOffset? ExpiresAt { get; } = expiresAt;

        public bool EligibleOnly { get; } = eligibleOnly;
    }

    /// <summary>
    /// A per-verb grant: either <see cref="IsUnrestrictedValue"/> access (a <see langword="null"/> reach — the
    /// operator escape) or a set of rule names ANDed together. The generated grant type; the constant
    /// <see cref="None"/>/<see cref="Full"/> grants are realised once and cached, and the rule names are read by
    /// enumerating <see cref="VerbGrantInfo.RuleNames"/> directly (no intermediate list).
    /// </summary>
    public readonly partial struct VerbGrantInfo
    {
        // The two constant grants are parsed once from their canonical JSON literal — no builder, no clone.
        private static readonly VerbGrantInfo NoneGrant = ParseValue("{\"unrestricted\":false}"u8);
        private static readonly VerbGrantInfo FullGrant = ParseValue("{\"unrestricted\":true}"u8);

        /// <summary>Gets a grant that confers nothing (the verb is not granted by this binding).</summary>
        public static VerbGrantInfo None => NoneGrant;

        /// <summary>Gets a grant of unrestricted (full-reach) access for the verb.</summary>
        public static VerbGrantInfo Full => FullGrant;

        /// <summary>Gets a value indicating whether the verb is unrestricted (full reach).</summary>
        public bool IsUnrestrictedValue => this.Unrestricted.IsNotUndefined() && (bool)this.Unrestricted;

        /// <summary>Gets the number of rule names ANDed for the verb (zero when unrestricted or ungranted).</summary>
        public int RuleNameCount => this.RuleNames.IsNotUndefined() ? this.RuleNames.GetArrayLength() : 0;

        /// <summary>Gets a value indicating whether this grant names one or more rules.</summary>
        public bool HasRuleNames => this.RuleNameCount > 0;

        /// <summary>Gets a value indicating whether this grant confers nothing.</summary>
        public bool IsEmptyValue => !this.IsUnrestrictedValue && !this.HasRuleNames;

        /// <summary>Builds a grant of the conjunction of the named rules, detached and ready to use.</summary>
        /// <param name="ruleNames">The rule names (ANDed).</param>
        /// <returns>The grant.</returns>
        public static VerbGrantInfo Rules(params string[] ruleNames)
        {
            // Thread the names through as the build context so the array callback can be static — no closure allocation.
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
                workspace,
                ruleNames,
                ruleNames: JsonStringArray.Build(
                    ruleNames,
                    static (in string[] names, ref JsonStringArray.Builder array) =>
                    {
                        foreach (string ruleName in names)
                        {
                            array.AddItem(ruleName);
                        }
                    }),
                unrestricted: false);
            return builder.RootElement.Clone();
        }
    }
}
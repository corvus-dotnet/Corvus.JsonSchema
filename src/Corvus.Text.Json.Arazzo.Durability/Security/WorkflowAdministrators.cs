// <copyright file="WorkflowAdministrators.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The explicit, mutable administration record for a base workflow id (design §13/§14.2): the set of <em>administrator
/// identities</em> entitled to publish further versions and to manage administration. Generated from
/// <c>Schemas/WorkflowAdministrators.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>An administrator identity is a set of unforgeable, deployment-stamped tags (e.g. <c>sys:tenant=acme</c>) — never
/// author-supplied. A submitter whose stamped administrator tags equal one of these sets (compared as a set, via
/// <see cref="WorkflowIdentity.SameAdministrator"/>) is an administrator; see <see cref="IsAdministeredBy"/>.</para>
/// <para>The record is materialized lazily: a workflow whose administration has never been mutated has <em>no</em> record,
/// and its administration defaults to the administrator identity that stamped version 1. The first transfer / add-administrator writes the
/// explicit record. The administrator set is never empty — the last administrator cannot be removed.</para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/WorkflowAdministrators.json")]
public readonly partial struct WorkflowAdministrators
{
    /// <summary>Gets the base workflow id this record governs.</summary>
    public string BaseWorkflowIdValue => (string)this.BaseWorkflowId;

    /// <summary>Gets the actor that first materialized the explicit administration record.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets the actor that last changed the administrator set, or <see langword="null"/>.</summary>
    public string? LastUpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets the instant the record was first materialized.</summary>
    public DateTimeOffset CreatedAtValue => ParseDate(this.CreatedAt);

    /// <summary>Gets the instant the administrator set was last changed, or <see langword="null"/>.</summary>
    public DateTimeOffset? LastUpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ParseDate(this.LastUpdatedAt) : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the number of administrator identities in the set.</summary>
    public int AdministratorCount => this.Administrators.IsNotUndefined() ? this.Administrators.GetArrayLength() : 0;

    /// <summary>Materializes the administrator identities as detached, owned tag sets (the holder may outlive this document).</summary>
    /// <returns>The administrator identities; never empty for a well-formed record.</returns>
    public List<SecurityTagSet> AdministratorIdentitiesValue
    {
        get
        {
            var administrators = new List<SecurityTagSet>(this.AdministratorCount);
            if (this.Administrators.IsNotUndefined())
            {
                foreach (AdministratorIdentity administrator in this.Administrators.EnumerateArray())
                {
                    administrators.Add(SecurityTagSet.CopyFrom(administrator.Tags));
                }
            }

            return administrators;
        }
    }

    /// <summary>Whether <paramref name="candidate"/> is one of this workflow's administrator identities (design §14.2): true
    /// iff <paramref name="candidate"/> equals one of the administrator sets exactly (order-independent set equality), so a
    /// caller presenting a superset or a partial match is <em>not</em> an administrator.</summary>
    /// <param name="candidate">The candidate administrator identity (the caller's stamped administrator tags).</param>
    /// <returns><see langword="true"/> if the candidate is an administrator.</returns>
    public bool IsAdministeredBy(SecurityTagSet candidate)
    {
        if (this.Administrators.IsUndefined())
        {
            return false;
        }

        foreach (AdministratorIdentity administrator in this.Administrators.EnumerateArray())
        {
            if (WorkflowIdentity.SameAdministrator(SecurityTagSet.CopyFrom(administrator.Tags), candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses an administration record from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The record.</returns>
    public static WorkflowAdministrators FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Builds an <see cref="AdministratorIdentity"/> from a resolved identity (its tags) plus the optional resolved
    /// grantee <paramref name="kind"/> / <paramref name="label"/>, materialized in <paramref name="workspace"/>. The tags
    /// are written straight from the set's UTF-8 (<see cref="SecurityTagSet.EnumerateUtf8"/>) — no per-tag string, no
    /// <c>ParseValue</c>; kind/label are CTJ values written bytes-to-bytes (omitted when absent).</summary>
    /// <param name="workspace">The workspace that owns the materialized identity (keep it alive until persisted/read).</param>
    /// <param name="tags">The resolved <c>sys:</c> identity tags.</param>
    /// <param name="kind">The resolved grantee kind (written when <paramref name="hasKind"/>).</param>
    /// <param name="hasKind">Whether <paramref name="kind"/> is present.</param>
    /// <param name="label">The display label (written when <paramref name="hasLabel"/>).</param>
    /// <param name="hasLabel">Whether <paramref name="label"/> is present.</param>
    /// <returns>The materialized administrator identity (workspace-backed).</returns>
    public static AdministratorIdentity BuildIdentity(JsonWorkspace workspace, SecurityTagSet tags, in AdministratorIdentity.KindEntity kind, bool hasKind, in JsonString label, bool hasLabel)
    {
        var state = new IdentityBuildState(tags, kind, hasKind, label, hasLabel);
        return AdministratorIdentity.CreateBuilder(
            workspace,
            AdministratorIdentity.Build(
                in state,
                tags: AdministratorIdentity.SecurityTagInfoArray.Build(
                    in state,
                    static (in IdentityBuildState s, ref AdministratorIdentity.SecurityTagInfoArray.Builder array) =>
                    {
                        SecurityTagSet.Utf8Enumerator e = s.Tags.EnumerateUtf8();
                        try
                        {
                            while (e.MoveNext())
                            {
                                var spans = new TagInfoSpans(e.CurrentKey, e.CurrentValue);
                                array.AddItem(SecurityTagInfo.Build(in spans, BuildTagInfo));
                            }
                        }
                        finally
                        {
                            e.Dispose();
                        }
                    }),
                kind: hasKind ? kind : default,
                label: hasLabel ? label : default))
            .RootElement;
    }

    // Writes one {key, value} security tag from its unescaped UTF-8 spans into the tag-info array, threading the spans
    // through a ref-struct context so the builder consumes them in place (the span→JsonString.Source conversion copies).
    private static void BuildTagInfo(in TagInfoSpans spans, ref SecurityTagInfo.Builder builder)
        => builder.Create((JsonString.Source)spans.Key, (JsonString.Source)spans.Value);

    // Carries the resolved identity tags + optional kind/label through the closure-free AdministratorIdentity build.
    private readonly struct IdentityBuildState(SecurityTagSet tags, AdministratorIdentity.KindEntity kind, bool hasKind, JsonString label, bool hasLabel)
    {
        public SecurityTagSet Tags { get; } = tags;

        public AdministratorIdentity.KindEntity Kind { get; } = kind;

        public bool HasKind { get; } = hasKind;

        public JsonString Label { get; } = label;

        public bool HasLabel { get; } = hasLabel;
    }

    // Carries one tag's (key, value) as unescaped UTF-8 spans through the closure-free SecurityTagInfo build.
    private readonly ref struct TagInfoSpans(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        public ReadOnlySpan<byte> Key { get; } = key;

        public ReadOnlySpan<byte> Value { get; } = value;
    }

    /// <summary>Serializes this record to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in WorkflowAdministrators v) => v.WriteTo(writer));

    /// <summary>Writes a brand-new administration record into the caller's (pooled) writer in one pass.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="administrators">The administrator identities (at least one); each carries its own tags and optional kind/label.</param>
    /// <param name="actor">The actor materializing the record (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string baseWorkflowId, IReadOnlyList<AdministratorIdentity> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, baseWorkflowId);
        WriteAdministrators(writer, administrators);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes an updated administration record, preserving the original creation audit and bumping the etag.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="administrators">The new administrator identities (at least one); each carries its own tags and optional kind/label.</param>
    /// <param name="actor">The actor changing the administrator set (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token.</param>
    public void WriteUpdated(Utf8JsonWriter writer, IReadOnlyList<AdministratorIdentity> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, this.BaseWorkflowIdValue);
        WriteAdministrators(writer, administrators);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, this.CreatedByValue);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, this.CreatedAtValue);
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    // Each administrator is written verbatim bytes-to-bytes (tags + optional kind/label): carried-forward existing
    // identities copy through, and a newly built identity (BuildIdentity) serializes its parts. No per-admin reshape.
    private static void WriteAdministrators(Utf8JsonWriter writer, IReadOnlyList<AdministratorIdentity> administrators)
    {
        writer.WritePropertyName(JsonPropertyNames.AdministratorsUtf8);
        writer.WriteStartArray();
        foreach (AdministratorIdentity administrator in administrators)
        {
            administrator.WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    // Read the instant from the strongly-typed date-time element via its native NodaTime value — no managed-string
    // realization and no JsonElement hop (the house idiom, e.g. RunnerRegistration, SecurityRuleDocument).
    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => ((NodaTime.OffsetDateTime)value).ToDateTimeOffset();
}
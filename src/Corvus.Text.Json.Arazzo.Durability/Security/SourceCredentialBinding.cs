// <copyright file="SourceCredentialBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The persisted form of a source credential (design §13): an operator-managed <em>reference</em> to secret
/// material held in an external secret store, plus non-sensitive auth metadata. Generated from
/// <c>Schemas/SourceCredentialBinding.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>
/// This document <strong>never contains secret material</strong>. Each <see cref="SecretReference"/> is a
/// <see cref="SecretRef"/> (<c>scheme://locator[#version]</c>) the runner dereferences at transport-bind time via
/// <c>ISecretResolver</c>; rotation is by changing the reference, so the control plane never reads the secret.
/// </para>
/// <para>
/// The binding is a small reference document, read rarely and cached by the runner keyed by
/// (<see cref="SourceNameValue"/>, <see cref="EnvironmentValue"/>) — see the §13.4 performance posture.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/SourceCredentialBinding.json")]
public readonly partial struct SourceCredentialBinding
{
    /// <summary>Gets the binding's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the Arazzo source description name this credential authenticates calls to.</summary>
    public string SourceNameValue => (string)this.SourceName;

    /// <summary>Gets the deployment environment the binding applies to.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the auth scheme the resolved secret(s) build into a provider.</summary>
    public SourceCredentialKind AuthKindValue => SourceCredentialKindExtensions.Parse((string)this.AuthKind);

    /// <summary>Gets the optional human description, or <see langword="null"/>.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the actor that created the binding.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets the actor that last updated the binding, or <see langword="null"/>.</summary>
    public string? LastUpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedAtValue => ParseDate(this.CreatedAt);

    /// <summary>Gets the last-update instant, or <see langword="null"/> if the binding has never been updated.</summary>
    public DateTimeOffset? LastUpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ParseDate(this.LastUpdatedAt) : null;

    /// <summary>Gets the instant the referenced secret expires (§13.2 lifecycle metadata), or <see langword="null"/>
    /// when the binding declares no expiry (treated as non-expiring).</summary>
    public DateTimeOffset? ExpiresAtOrNull => this.ExpiresAt.IsNotUndefined() ? ParseDate(this.ExpiresAt) : null;

    /// <summary>Gets the instant the referenced secret was last rotated (§13.2 lifecycle metadata), or
    /// <see langword="null"/> when unknown.</summary>
    public DateTimeOffset? RotatedAtOrNull => this.RotatedAt.IsNotUndefined() ? ParseDate(this.RotatedAt) : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the security tags (KVP labels) scoping who may <strong>manage</strong> this binding (§14.2) as a
    /// deferred holder over the persisted bytes — empty on an unscoped binding. Drives the management reach check;
    /// independent of <see cref="UsageTagsValue"/>.</summary>
    public SecurityTagSet ManagementTagsValue => SecurityTagSet.CopyFrom(this.ManagementTags);

    /// <summary>Gets the security tags (KVP labels) scoping which runs may <strong>use</strong> this binding (§13) as a
    /// deferred holder over the persisted bytes — empty (shared) on an unscoped binding. Drives the usage entitlement
    /// check (<see cref="IsUsableBy"/>); independent of <see cref="ManagementTagsValue"/>.</summary>
    public SecurityTagSet UsageTagsValue => SecurityTagSet.CopyFrom(this.UsageTags);

    /// <summary>Whether this binding is scoped to a specific run identity (it carries <c>usageTags</c>), as opposed to a
    /// shared binding usable by any run. The access overview (§6.1) lists only the usage-scoped bindings a grantee's
    /// identity satisfies — a shared binding is deployment-wide, not a grantee-specific grant.</summary>
    public bool IsUsageScoped => this.UsageTags.IsNotUndefined() && this.UsageTags.GetArrayLength() > 0;

    /// <summary>Whether a run carrying <paramref name="runTags"/> is entitled to use this binding (design §13/§14.2):
    /// it is, iff the run satisfies <em>every</em> <c>usageTags</c> entry the binding carries (label-superset). An
    /// untagged-for-usage binding is shared and usable by any run; a usage-tagged binding is usable only by a run that
    /// carries all of its usage tags.</summary>
    /// <param name="runTags">The run's own security tags.</param>
    /// <returns><see langword="true"/> if the run is entitled to use this binding.</returns>
    public bool IsUsableBy(SecurityTagSet runTags)
    {
        if (this.UsageTags.IsUndefined() || this.UsageTags.GetArrayLength() == 0)
        {
            return true;
        }

        // A usage-tagged binding is usable only by a run carrying all of its usage tags. The whole membership scan runs
        // on UTF-8: the run's tags are parsed once into a pooled scratch buffer (no managed strings, no list), and each
        // required tag is matched on its unescaped UTF-8 (GetUtf8String) against those slices — so it allocates nothing.
        if (runTags.IsEmpty)
        {
            return false;
        }

        ReadOnlySpan<byte> json = runTags.RawJson;
        int runCount = runTags.Count;
        byte[] scratch = ArrayPool<byte>.Shared.Rent(json.Length);
        SecurityTagSpanSort.TagSlice[]? rentedSlices = runCount > SecurityTagSpanSort.StackTagCapacity ? ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Rent(runCount) : null;
        try
        {
            scoped Span<SecurityTagSpanSort.TagSlice> table;
            if (rentedSlices is not null)
            {
                table = rentedSlices;
            }
            else
            {
                table = stackalloc SecurityTagSpanSort.TagSlice[SecurityTagSpanSort.StackTagCapacity];
            }

            Span<SecurityTagSpanSort.TagSlice> slices = table[..runCount];
            SecurityTagSpanSort.Parse(json, scratch, slices);

            foreach (SecurityTagInfo required in this.UsageTags.EnumerateArray())
            {
                if (!RunCarries(slices, scratch, required))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
            if (rentedSlices is not null)
            {
                ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Return(rentedSlices);
            }
        }
    }

    // Whether the run's tags (parsed as UTF-8 slices into scratch) carry the required tag, matched on its unescaped
    // UTF-8 (a pooled GetUtf8String buffer per side) — no managed string for either side.
    private static bool RunCarries(ReadOnlySpan<SecurityTagSpanSort.TagSlice> slices, ReadOnlySpan<byte> scratch, SecurityTagInfo required)
    {
        using UnescapedUtf8JsonString key = required.Key.GetUtf8String();
        using UnescapedUtf8JsonString value = required.Value.GetUtf8String();
        foreach (SecurityTagSpanSort.TagSlice slice in slices)
        {
            if (scratch.Slice(slice.KeyOffset, slice.KeyLength).SequenceEqual(key.Span)
                && scratch.Slice(slice.ValueOffset, slice.ValueLength).SequenceEqual(value.Span))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Derives the binding's <see cref="CredentialStatus"/> (§13.2) from its <c>expiresAt</c> against
    /// <paramref name="now"/> — pure, allocation-free, and never persisted (so it cannot go stale). A binding with no
    /// declared expiry is <see cref="CredentialStatus.Valid"/>; one whose expiry has passed is
    /// <see cref="CredentialStatus.Expired"/>; one expiring within <paramref name="expiringWindow"/> is
    /// <see cref="CredentialStatus.ExpiringSoon"/>. The expiry instant itself is available via
    /// <see cref="ExpiresAtOrNull"/>.</summary>
    /// <param name="now">The current instant (supply the deployment's <c>TimeProvider</c> time).</param>
    /// <param name="expiringWindow">How far ahead of expiry a still-valid credential is reported as expiring-soon.</param>
    /// <returns>The derived status.</returns>
    public CredentialStatus DeriveStatus(DateTimeOffset now, TimeSpan expiringWindow)
    {
        if (this.ExpiresAtOrNull is not { } expiresAt)
        {
            return CredentialStatus.Valid;
        }

        if (now >= expiresAt)
        {
            return CredentialStatus.Expired;
        }

        return expiresAt - now <= expiringWindow ? CredentialStatus.ExpiringSoon : CredentialStatus.Valid;
    }

    /// <summary>Gets the number of secret references on the binding.</summary>
    public int SecretRefCount => this.SecretRefs.IsNotUndefined() ? this.SecretRefs.GetArrayLength() : 0;

    /// <summary>Looks up a secret reference by its role, returning the parsed <see cref="SecretRef"/> pointer.</summary>
    /// <param name="role">The role (e.g. <c>value</c>, <c>password</c>, <c>clientSecret</c>).</param>
    /// <param name="secretRef">The parsed reference on success.</param>
    /// <returns><see langword="true"/> if a reference with that role exists.</returns>
    public bool TryGetSecretRef(string role, out SecretRef secretRef)
    {
        foreach (SecretReference reference in this.SecretRefs.EnumerateArray())
        {
            if (((JsonElement)reference.Name).EqualsString(role))
            {
                secretRef = SecretRef.Parse((string)reference.Ref);
                return true;
            }
        }

        secretRef = default;
        return false;
    }

    /// <summary>Looks up a non-secret configuration value by key.</summary>
    /// <param name="key">The configuration key (e.g. <c>headerName</c>, <c>tokenUrl</c>).</param>
    /// <param name="value">The value on success.</param>
    /// <returns><see langword="true"/> if a configuration entry with that key exists.</returns>
    public bool TryGetConfigValue(string key, out string? value)
    {
        if (this.Config.IsNotUndefined())
        {
            foreach (CredentialConfigEntry entry in this.Config.EnumerateArray())
            {
                if (((JsonElement)entry.Key).EqualsString(key))
                {
                    value = (string)entry.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    /// <summary>Gets the per-environment server base URL override (the non-secret <c>baseUrl</c> config), or
    /// <see langword="null"/> when the binding carries none or it is not a well-formed absolute URI. A source's base URL
    /// is commonly environment-specific (design §8); when set, the run's transport sends to this endpoint instead of the
    /// source document's declared server. (The AsyncAPI <c>serverHost</c> equivalent targets the message transport and
    /// is not applied by the HTTP transport.)</summary>
    public Uri? BaseUrlOverrideOrNull
        => this.TryGetConfigValue("baseUrl", out string? value) && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;

    /// <summary>Parses a binding from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The binding.</returns>
    public static SourceCredentialBinding FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this binding to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in SourceCredentialBinding v) => v.WriteTo(writer));

    /// <summary>Builds a draft binding from programmatic content (references + non-secret metadata + the resolved
    /// security tags) for a store to complete with the server-stamped id/createdBy/createdAt/etag — the cold counterpart
    /// of carrying an HTTP request body as the draft. The store reads only the content fields (bytes-to-bytes) and stamps
    /// the rest.</summary>
    /// <param name="definition">The binding content (references + non-secret metadata + security tags).</param>
    /// <returns>A pooled, disposable draft document carrying only the supplied content; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<SourceCredentialBinding> Draft(SourceCredentialDefinition definition)
        => PersistedJson.ToPooledDocument<SourceCredentialBinding, SourceCredentialDefinition>(
            definition,
            static (Utf8JsonWriter writer, in SourceCredentialDefinition d) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.SourceNameUtf8, d.SourceName);
                writer.WriteString(JsonPropertyNames.EnvironmentUtf8, d.Environment);
                writer.WriteString(JsonPropertyNames.AuthKindUtf8, d.AuthKind.ToJsonToken());
                WriteSecretRefs(writer, d.SecretRefs);
                WriteConfig(writer, d.Config);
                if (d.Description is { } description)
                {
                    writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
                }

                WriteLifecycle(writer, d);

                if (!d.ManagementTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    d.ManagementTags.WriteTo(writer);
                }

                if (!d.UsageTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.UsageTagsUtf8);
                    d.UsageTags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });

    /// <summary>Builds a draft binding directly from an HTTP request body's already-parsed JSON values (carried
    /// bytes-to-bytes — no per-field strings, no list) plus the resolved management/usage tags, for a store to complete
    /// with the server-stamped id/createdBy/createdAt/etag. Pass <see langword="default"/> (an undefined element) for any
    /// field the body omits; for an update, pass <see langword="default"/> for the immutable identity (sourceName,
    /// environment) and tags — the store carries those forward from the stored binding.</summary>
    /// <param name="sourceName">The source-name value (or undefined for an update).</param>
    /// <param name="environment">The environment value (or undefined for an update).</param>
    /// <param name="authKind">The auth-kind token value.</param>
    /// <param name="secretRefs">The secret-references array value.</param>
    /// <param name="config">The config array value (or undefined).</param>
    /// <param name="description">The description value (or undefined).</param>
    /// <param name="expiresAt">The expires-at value (or undefined).</param>
    /// <param name="rotatedAt">The rotated-at value (or undefined).</param>
    /// <param name="managementTags">The resolved management tags (empty for an update).</param>
    /// <param name="usageTags">The resolved usage tags (empty for an update).</param>
    /// <returns>A pooled, disposable draft document; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<SourceCredentialBinding> Draft(
        in JsonElement sourceName,
        in JsonElement environment,
        in JsonElement authKind,
        in JsonElement secretRefs,
        in JsonElement config,
        in JsonElement description,
        in JsonElement expiresAt,
        in JsonElement rotatedAt,
        in SecurityTagSet managementTags,
        in SecurityTagSet usageTags)
    {
        DraftElements state = new(sourceName, environment, authKind, secretRefs, config, description, expiresAt, rotatedAt, managementTags, usageTags);
        return PersistedJson.ToPooledDocument<SourceCredentialBinding, DraftElements>(
            state,
            static (Utf8JsonWriter writer, in DraftElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.SourceNameUtf8, s.SourceName);
                WriteValueIfPresent(writer, JsonPropertyNames.EnvironmentUtf8, s.Environment);
                WriteValueIfPresent(writer, JsonPropertyNames.AuthKindUtf8, s.AuthKind);
                WriteValueIfPresent(writer, JsonPropertyNames.SecretRefsUtf8, s.SecretRefs);
                WriteValueIfPresent(writer, JsonPropertyNames.ConfigUtf8, s.Config);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, s.Description);
                WriteValueIfPresent(writer, JsonPropertyNames.ExpiresAtUtf8, s.ExpiresAt);
                WriteValueIfPresent(writer, JsonPropertyNames.RotatedAtUtf8, s.RotatedAt);
                if (!s.ManagementTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.ManagementTags.WriteTo(writer);
                }

                if (!s.UsageTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.UsageTagsUtf8);
                    s.UsageTags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>The bytes-to-bytes create overload: the usage tags are written straight into the draft array via
    /// <paramref name="writeManagementTags"/> / <paramref name="writeUsageTags"/> (<see cref="IdentityBuilder"/>
    /// write-actions — no intermediate <see cref="SecurityTagSet"/> materialization for either, since the draft document
    /// is the genuine leaf; the create handler reads the management tags back from the draft as a non-owning view for the
    /// <c>Admits</c> write-reach check). Each tag property is omitted entirely when its <c>has…</c> flag is
    /// <see langword="false"/>.</summary>
    /// <typeparam name="TManagement">The state threaded to <paramref name="writeManagementTags"/> (a <see langword="static"/> action — no closure).</typeparam>
    /// <typeparam name="TUsage">The state threaded to <paramref name="writeUsageTags"/> (a <see langword="static"/> action — no closure).</typeparam>
    /// <param name="sourceName">The source name JSON value (or undefined to omit).</param>
    /// <param name="environment">The environment JSON value (or undefined).</param>
    /// <param name="authKind">The auth-kind JSON value (or undefined).</param>
    /// <param name="secretRefs">The secret-references JSON value (or undefined).</param>
    /// <param name="config">The config JSON value (or undefined).</param>
    /// <param name="description">The description JSON value (or undefined).</param>
    /// <param name="expiresAt">The expiry JSON value (or undefined).</param>
    /// <param name="rotatedAt">The rotation JSON value (or undefined).</param>
    /// <param name="managementState">The state passed to <paramref name="writeManagementTags"/>.</param>
    /// <param name="hasManagementTags">Whether to write the management-tags property at all.</param>
    /// <param name="writeManagementTags">Adds the resolved management tags to the draft's management array via the <see cref="IdentityBuilder"/>.</param>
    /// <param name="usageState">The state passed to <paramref name="writeUsageTags"/>.</param>
    /// <param name="hasUsageTags">Whether to write the usage-tags property at all (omitted when no usage tags resolve).</param>
    /// <param name="writeUsageTags">Adds the resolved usage tags to the draft's usage array via the <see cref="IdentityBuilder"/>.</param>
    /// <returns>The pooled, disposable draft document.</returns>
    public static ParsedJsonDocument<SourceCredentialBinding> Draft<TManagement, TUsage>(
        in JsonElement sourceName,
        in JsonElement environment,
        in JsonElement authKind,
        in JsonElement secretRefs,
        in JsonElement config,
        in JsonElement description,
        in JsonElement expiresAt,
        in JsonElement rotatedAt,
        in TManagement managementState,
        bool hasManagementTags,
        SecurityTagBuildAction<TManagement> writeManagementTags,
        in TUsage usageState,
        bool hasUsageTags,
        SecurityTagBuildAction<TUsage> writeUsageTags,
        in JsonElement usageKind,
        in JsonElement usageLabel)
    {
        DraftElements<TManagement, TUsage> state = new(sourceName, environment, authKind, secretRefs, config, description, expiresAt, rotatedAt, managementState, hasManagementTags, writeManagementTags, usageState, hasUsageTags, writeUsageTags, usageKind, usageLabel);
        return PersistedJson.ToPooledDocument<SourceCredentialBinding, DraftElements<TManagement, TUsage>>(
            state,
            static (Utf8JsonWriter writer, in DraftElements<TManagement, TUsage> s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.SourceNameUtf8, s.SourceName);
                WriteValueIfPresent(writer, JsonPropertyNames.EnvironmentUtf8, s.Environment);
                WriteValueIfPresent(writer, JsonPropertyNames.AuthKindUtf8, s.AuthKind);
                WriteValueIfPresent(writer, JsonPropertyNames.SecretRefsUtf8, s.SecretRefs);
                WriteValueIfPresent(writer, JsonPropertyNames.ConfigUtf8, s.Config);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, s.Description);
                WriteValueIfPresent(writer, JsonPropertyNames.ExpiresAtUtf8, s.ExpiresAt);
                WriteValueIfPresent(writer, JsonPropertyNames.RotatedAtUtf8, s.RotatedAt);
                if (s.HasManagementTags)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    writer.WriteStartArray();
                    var managementBuilder = new IdentityBuilder(writer);
                    TManagement managementState = s.ManagementState;
                    s.WriteManagementTags(ref managementBuilder, in managementState);
                    writer.WriteEndArray();
                }

                if (s.HasUsageTags)
                {
                    writer.WritePropertyName(JsonPropertyNames.UsageTagsUtf8);
                    writer.WriteStartArray();
                    var usageBuilder = new IdentityBuilder(writer);
                    TUsage usageState = s.UsageState;
                    s.WriteUsageTags(ref usageBuilder, in usageState);
                    writer.WriteEndArray();
                }

                // The resolved usage grantee's display (kind/label) carried bytes-to-bytes from the request body — omitted
                // when absent (the interim/owner-default usage path) or when the grantee carried no kind/label.
                WriteValueIfPresent(writer, JsonPropertyNames.UsageKindUtf8, s.UsageKind);
                WriteValueIfPresent(writer, JsonPropertyNames.UsageLabelUtf8, s.UsageLabel);

                writer.WriteEndObject();
            });
    }

    /// <summary>Writes a brand-new binding's JSON into the caller's (pooled) writer in one pass — the draft's operator
    /// content is carried bytes-to-bytes and the server fields (id/createdBy/createdAt/etag) are stamped here. Secret
    /// material never appears; each secret reference is validated to be a well-formed <see cref="SecretRef"/> so a
    /// malformed (or accidental inline-secret) value is rejected at the boundary.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="id">The assigned binding id.</param>
    /// <param name="actor">The actor creating the binding (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in SourceCredentialBinding draft, string id, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        ValidateDraft(draft);
        RequireIdentity(draft);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        WriteValueIfPresent(writer, JsonPropertyNames.SourceNameUtf8, (JsonElement)draft.SourceName);
        WriteValueIfPresent(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)draft.Environment);
        WriteValueIfPresent(writer, JsonPropertyNames.AuthKindUtf8, (JsonElement)draft.AuthKind);
        WriteValueIfPresent(writer, JsonPropertyNames.SecretRefsUtf8, (JsonElement)draft.SecretRefs);
        WriteValueIfPresent(writer, JsonPropertyNames.ConfigUtf8, (JsonElement)draft.Config);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);
        WriteValueIfPresent(writer, JsonPropertyNames.ExpiresAtUtf8, (JsonElement)draft.ExpiresAt);
        WriteValueIfPresent(writer, JsonPropertyNames.RotatedAtUtf8, (JsonElement)draft.RotatedAt);
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageTagsUtf8, (JsonElement)draft.UsageTags);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageKindUtf8, (JsonElement)draft.UsageKind);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageLabelUtf8, (JsonElement)draft.UsageLabel);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    // Copies a draft/source property to the writer bytes-to-bytes when present (skips an undefined element).
    private static void WriteValueIfPresent(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    // Writes the draft's value when it supplies one (a re-tag), else the stored value carried forward — for a field that
    // is replaced only when the update includes it (managementTags). Both undefined → the property is omitted.
    private static void WriteValuePreferringDraft(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement draftValue, in JsonElement storedValue)
    {
        JsonElement chosen = draftValue.ValueKind != JsonValueKind.Undefined ? draftValue : storedValue;
        if (chosen.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            chosen.WriteTo(writer);
        }
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values plus the resolved tag sets.
    private readonly struct DraftElements(
        JsonElement sourceName,
        JsonElement environment,
        JsonElement authKind,
        JsonElement secretRefs,
        JsonElement config,
        JsonElement description,
        JsonElement expiresAt,
        JsonElement rotatedAt,
        SecurityTagSet managementTags,
        SecurityTagSet usageTags)
    {
        public JsonElement SourceName { get; } = sourceName;

        public JsonElement Environment { get; } = environment;

        public JsonElement AuthKind { get; } = authKind;

        public JsonElement SecretRefs { get; } = secretRefs;

        public JsonElement Config { get; } = config;

        public JsonElement Description { get; } = description;

        public JsonElement ExpiresAt { get; } = expiresAt;

        public JsonElement RotatedAt { get; } = rotatedAt;

        public SecurityTagSet ManagementTags { get; } = managementTags;

        public SecurityTagSet UsageTags { get; } = usageTags;
    }

    // The create-overload draft elements: both management and usage tags are write-actions + their state (no tag
    // SecurityTagSet materialization) so the resolved tags write straight into the draft arrays.
    private readonly struct DraftElements<TManagement, TUsage>(
        JsonElement sourceName,
        JsonElement environment,
        JsonElement authKind,
        JsonElement secretRefs,
        JsonElement config,
        JsonElement description,
        JsonElement expiresAt,
        JsonElement rotatedAt,
        TManagement managementState,
        bool hasManagementTags,
        SecurityTagBuildAction<TManagement> writeManagementTags,
        TUsage usageState,
        bool hasUsageTags,
        SecurityTagBuildAction<TUsage> writeUsageTags,
        JsonElement usageKind,
        JsonElement usageLabel)
    {
        public JsonElement SourceName { get; } = sourceName;

        public JsonElement Environment { get; } = environment;

        public JsonElement AuthKind { get; } = authKind;

        public JsonElement SecretRefs { get; } = secretRefs;

        public JsonElement Config { get; } = config;

        public JsonElement Description { get; } = description;

        public JsonElement ExpiresAt { get; } = expiresAt;

        public JsonElement RotatedAt { get; } = rotatedAt;

        public TManagement ManagementState { get; } = managementState;

        public bool HasManagementTags { get; } = hasManagementTags;

        public SecurityTagBuildAction<TManagement> WriteManagementTags { get; } = writeManagementTags;

        public TUsage UsageState { get; } = usageState;

        public bool HasUsageTags { get; } = hasUsageTags;

        public SecurityTagBuildAction<TUsage> WriteUsageTags { get; } = writeUsageTags;

        public JsonElement UsageKind { get; } = usageKind;

        public JsonElement UsageLabel { get; } = usageLabel;
    }

    /// <summary>Writes an updated copy of this binding: the immutable identity (id/sourceName/environment), the immutable
    /// security tags, and the created-* audit fields are carried through from the stored binding; the draft's mutable
    /// reference/metadata content is carried bytes-to-bytes.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in SourceCredentialBinding draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        ValidateDraft(draft);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, this.IdValue);

        // Immutable identity carried forward from the stored binding, never taken from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.SourceNameUtf8, (JsonElement)this.SourceName);
        WriteValueIfPresent(writer, JsonPropertyNames.EnvironmentUtf8, (JsonElement)this.Environment);

        // Mutable content carried bytes-to-bytes from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.AuthKindUtf8, (JsonElement)draft.AuthKind);
        WriteValueIfPresent(writer, JsonPropertyNames.SecretRefsUtf8, (JsonElement)draft.SecretRefs);
        WriteValueIfPresent(writer, JsonPropertyNames.ConfigUtf8, (JsonElement)draft.Config);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);
        WriteValueIfPresent(writer, JsonPropertyNames.ExpiresAtUtf8, (JsonElement)draft.ExpiresAt);
        WriteValueIfPresent(writer, JsonPropertyNames.RotatedAtUtf8, (JsonElement)draft.RotatedAt);

        // Management tags (who may MANAGE the binding, §14.2): an administrator re-tag supplies them on the draft (already
        // merged with the preserved deployment-internal tags by the handler) → take the draft's; an update that omits them
        // carries the stored tags forward. Usage tags + the grantee display (kind/label) remain immutable identity — always
        // carried forward from the existing binding (never dropped on update — the catalog-drop lesson).
        WriteValuePreferringDraft(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags, (JsonElement)this.ManagementTags);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageTagsUtf8, (JsonElement)this.UsageTags);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageKindUtf8, (JsonElement)this.UsageKind);
        WriteValueIfPresent(writer, JsonPropertyNames.UsageLabelUtf8, (JsonElement)this.UsageLabel);

        writer.WriteString(JsonPropertyNames.CreatedByUtf8, this.CreatedByValue);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, this.CreatedAtValue);
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Validates a draft binding's references: at least one, each with a non-empty role name and a well-formed
    /// <see cref="SecretRef"/> (a pointer, not inline secret material) — read bytes-to-bytes off the draft's UTF-8.
    /// Throws <see cref="ArgumentException"/> on a malformed draft.</summary>
    /// <param name="draft">The draft to validate.</param>
    public static void ValidateDraft(in SourceCredentialBinding draft)
    {
        if (!draft.SecretRefs.IsNotUndefined() || draft.SecretRefs.GetArrayLength() == 0)
        {
            throw new ArgumentException("A source credential binding requires at least one secret reference.", nameof(draft));
        }

        foreach (SecretReference reference in draft.SecretRefs.EnumerateArray())
        {
            using UnescapedUtf8JsonString name = reference.Name.GetUtf8String();
            if (name.Span.IsEmpty)
            {
                throw new ArgumentException("A secret reference requires a non-empty name.", nameof(draft));
            }

            using UnescapedUtf8JsonString referenceValue = reference.Ref.GetUtf8String();
            if (!SecretRef.IsWellFormed(referenceValue.Span))
            {
                throw new ArgumentException(
                    $"Secret reference '{(string)reference.Name}' is not a valid SecretRef (scheme://locator[#version]); secret material must never be stored inline.",
                    nameof(draft));
            }
        }

        // mTLS (§13.1) is the one kind that needs more than one secret slot and is connection-level: it must carry a
        // 'certificate' reference, and — because the certificate is established at the TLS handshake, not per request — it
        // cannot be usage-scoped (the control-plane handler also rejects an explicit usage grantee with a clearer message).
        if (draft.AuthKind.IsNotUndefined() && draft.AuthKindValue == SourceCredentialKind.Mtls)
        {
            if (!draft.TryGetSecretRef("certificate", out _))
            {
                throw new ArgumentException("An mTLS credential binding requires a 'certificate' secret reference.", nameof(draft));
            }

            if (draft.UsageTags.IsNotUndefined() && draft.UsageTags.GetArrayLength() > 0)
            {
                throw new ArgumentException("An mTLS credential is connection-level and cannot be usage-scoped; it must not carry usage tags.", nameof(draft));
            }
        }
    }

    // A create draft must carry the immutable identity (sourceName, environment); an update draft omits it (the store
    // carries it forward from the stored binding).
    private static void RequireIdentity(in SourceCredentialBinding draft)
    {
        if (!draft.SourceName.IsNotUndefined())
        {
            throw new ArgumentException("A source credential binding requires a 'sourceName'.", nameof(draft));
        }

        if (!draft.Environment.IsNotUndefined())
        {
            throw new ArgumentException("A source credential binding requires an 'environment'.", nameof(draft));
        }
    }

    private static void WriteSecretRefs(Utf8JsonWriter writer, IReadOnlyList<SecretReferenceDefinition> secretRefs)
    {
        writer.WritePropertyName(JsonPropertyNames.SecretRefsUtf8);
        writer.WriteStartArray();
        foreach (SecretReferenceDefinition reference in secretRefs)
        {
            writer.WriteStartObject();
            writer.WriteString(SecretReference.JsonPropertyNames.NameUtf8, reference.Name);
            writer.WriteString(SecretReference.JsonPropertyNames.RefUtf8, reference.Ref);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteConfig(Utf8JsonWriter writer, IReadOnlyList<CredentialConfigDefinition>? config)
    {
        if (config is null || config.Count == 0)
        {
            return;
        }

        writer.WritePropertyName(JsonPropertyNames.ConfigUtf8);
        writer.WriteStartArray();
        foreach (CredentialConfigDefinition entry in config)
        {
            writer.WriteStartObject();
            writer.WriteString(CredentialConfigEntry.JsonPropertyNames.KeyUtf8, entry.Key);
            writer.WriteString(CredentialConfigEntry.JsonPropertyNames.ValueUtf8, entry.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteLifecycle(Utf8JsonWriter writer, SourceCredentialDefinition definition)
    {
        // Non-sensitive lifecycle metadata (§13.2); both optional and mutable (a rotation refreshes them). The typed
        // DateTimeOffset WriteString overload formats ISO-8601 straight into the buffer — no interim string.
        if (definition.ExpiresAt is { } expiresAt)
        {
            writer.WriteString(JsonPropertyNames.ExpiresAtUtf8, expiresAt);
        }

        if (definition.RotatedAt is { } rotatedAt)
        {
            writer.WriteString(JsonPropertyNames.RotatedAtUtf8, rotatedAt);
        }
    }

    // Read the instant from the strongly-typed date-time element via its native NodaTime value — no managed-string
    // realization (unlike a (string)cast + DateTimeOffset.Parse) and no JsonElement hop. Matches the house idiom
    // (e.g. RunnerRegistration, SecurityRuleDocument).
    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => ((NodaTime.OffsetDateTime)value).ToDateTimeOffset();
}
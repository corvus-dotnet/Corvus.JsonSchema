// <copyright file="SourceCredentialBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

        List<SecurityTag> runTagList = runTags.ToList();
        foreach (SecurityTagInfo required in this.UsageTags.EnumerateArray())
        {
            string key = (string)required.Key;
            string value = (string)required.Value;
            bool matched = false;
            foreach (SecurityTag have in runTagList)
            {
                if (string.Equals(have.Key, key, StringComparison.Ordinal) && string.Equals(have.Value, value, StringComparison.Ordinal))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
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
            if (string.Equals((string)reference.Name, role, StringComparison.Ordinal))
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
                if (string.Equals((string)entry.Key, key, StringComparison.Ordinal))
                {
                    value = (string)entry.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    /// <summary>Parses a binding from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The binding.</returns>
    public static SourceCredentialBinding FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this binding to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in SourceCredentialBinding v) => v.WriteTo(writer));

    /// <summary>Writes a brand-new binding's JSON into the caller's (pooled) writer in one pass — secret material
    /// never appears; each <see cref="SecretReference"/> is validated to parse as a <see cref="SecretRef"/> so a
    /// malformed (or accidental inline-secret) value is rejected at the boundary.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="id">The assigned binding id.</param>
    /// <param name="definition">The binding content (references + non-secret metadata only).</param>
    /// <param name="actor">The actor creating the binding (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string id, SourceCredentialDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        ValidateDefinition(definition);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        writer.WriteString(JsonPropertyNames.SourceNameUtf8, definition.SourceName);
        writer.WriteString(JsonPropertyNames.EnvironmentUtf8, definition.Environment);
        writer.WriteString(JsonPropertyNames.AuthKindUtf8, definition.AuthKind.ToJsonToken());
        WriteSecretRefs(writer, definition.SecretRefs);
        WriteConfig(writer, definition.Config);
        if (definition.Description is { } description)
        {
            writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
        }

        WriteLifecycle(writer, definition);

        if (!definition.ManagementTags.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
            definition.ManagementTags.WriteTo(writer);
        }

        if (!definition.UsageTags.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.UsageTagsUtf8);
            definition.UsageTags.WriteTo(writer);
        }

        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes an updated copy of this binding (immutable identity — id/sourceName/environment — and the
    /// created-* audit fields carried through; only the mutable reference/metadata fields replaced).</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="definition">The new binding content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, SourceCredentialDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        ValidateDefinition(definition);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, this.IdValue);
        writer.WriteString(JsonPropertyNames.SourceNameUtf8, this.SourceNameValue);
        writer.WriteString(JsonPropertyNames.EnvironmentUtf8, this.EnvironmentValue);
        writer.WriteString(JsonPropertyNames.AuthKindUtf8, definition.AuthKind.ToJsonToken());
        WriteSecretRefs(writer, definition.SecretRefs);
        WriteConfig(writer, definition.Config);
        if (definition.Description is { } description)
        {
            writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
        }

        WriteLifecycle(writer, definition);

        // Management and usage tags are immutable identity (the binding's row-authorization scope) — carried forward
        // from the existing binding, never taken from the update definition.
        if (!this.ManagementTagsValue.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
            this.ManagementTagsValue.WriteTo(writer);
        }

        if (!this.UsageTagsValue.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.UsageTagsUtf8);
            this.UsageTagsValue.WriteTo(writer);
        }

        writer.WriteString(JsonPropertyNames.CreatedByUtf8, this.CreatedByValue);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, this.CreatedAtValue);
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Validates a definition: at least one secret reference, every reference well-formed (and thus a
    /// pointer, not inline secret material). Throws <see cref="ArgumentException"/> on a malformed definition.</summary>
    /// <param name="definition">The definition to validate.</param>
    public static void ValidateDefinition(SourceCredentialDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.SourceName);
        ArgumentException.ThrowIfNullOrEmpty(definition.Environment);
        if (definition.SecretRefs is null || definition.SecretRefs.Count == 0)
        {
            throw new ArgumentException("A source credential binding requires at least one secret reference.", nameof(definition));
        }

        foreach (SecretReferenceDefinition reference in definition.SecretRefs)
        {
            ArgumentException.ThrowIfNullOrEmpty(reference.Name);
            if (!SecretRef.TryParse(reference.Ref, out _))
            {
                throw new ArgumentException(
                    $"Secret reference '{reference.Name}' is not a valid SecretRef (scheme://locator[#version]); secret material must never be stored inline.",
                    nameof(definition));
            }
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
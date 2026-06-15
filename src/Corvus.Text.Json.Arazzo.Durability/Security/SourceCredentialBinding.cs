// <copyright file="SourceCredentialBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

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

        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt.ToString("O", CultureInfo.InvariantCulture));
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

        writer.WriteString(JsonPropertyNames.CreatedByUtf8, this.CreatedByValue);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, (string)this.CreatedAt);
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt.ToString("O", CultureInfo.InvariantCulture));
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

    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
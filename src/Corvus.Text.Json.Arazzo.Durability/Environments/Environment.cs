// <copyright file="Environment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The persisted form of a deployment environment (design §7.7): a first-class, governed, reach-scoped resource a
/// workflow version is made available in and whose source credentials form a per-environment set. Generated from
/// <c>Schemas/Environment.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>
/// An environment is governed exactly like a workflow (§15): it carries an administrator set (stored separately, the
/// <c>IEnvironmentAdministratorStore</c>) and an audit trail, and creating one grants the creator administration.
/// <see cref="ManagementTagsValue"/> are the reach scope the deployment stamps at create — a caller sees and manages
/// only the environments their <c>AccessContext</c> admits.
/// </para>
/// <para>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteUpdated"/>): a store passes the
/// buffer it owns and the environment is realised and written in one pass — no interim detached clone, and every
/// carried-forward field is copied <strong>bytes-to-bytes</strong>. The leaf accessors realise only the three values a
/// store genuinely needs as managed forms — the key (<see cref="NameValue"/>), the concurrency token
/// (<see cref="EtagValue"/>), and the reach set (<see cref="ManagementTagsValue"/>); everything else stays JSON.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/Environment.json")]
public readonly partial struct Environment
{
    /// <summary>Gets the environment's name (its stable identity) — the store key.</summary>
    public string NameValue => (string)this.Name;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the minimum run isolation this environment requires (ADR 0058), read string-free; an absent
    /// <c>requiredIsolation</c> means <see cref="RunIsolationModel.InProcess"/>. The start gate matches this against a
    /// runner's advertised isolation model.</summary>
    public RunIsolationModel RequiredIsolationValue
        => ((JsonElement)this.RequiredIsolation).ValueKind == JsonValueKind.String && this.RequiredIsolation.ValueEquals("Isolated"u8)
            ? RunIsolationModel.Isolated : RunIsolationModel.InProcess;

    /// <summary>Gets the runtime identifier (RID) the serverless native binary targets for this environment (ADR 0055),
    /// defaulting to <c>linux-x64</c> when absent. Meaningful only when <see cref="RequiredIsolationValue"/> is
    /// <see cref="RunIsolationModel.Isolated"/>; the deploy-on-publish build and the dispatch-ready gate key on it.</summary>
    public string RuntimeIdentifierValue
        => ((JsonElement)this.RuntimeIdentifier).ValueKind == JsonValueKind.String ? (string)this.RuntimeIdentifier : "linux-x64";

    /// <summary>Gets the security tags (KVP labels) scoping who may <strong>manage and see</strong> this environment
    /// (§14.2) as a deferred holder over the persisted bytes — empty on an unscoped environment. Drives the management
    /// reach check.</summary>
    public SecurityTagSet ManagementTagsValue => SecurityTagSet.CopyFrom(this.ManagementTags);

    /// <summary>Parses an environment from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The environment.</returns>
    public static Environment FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this environment to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in Environment v) => v.WriteTo(writer));

    /// <summary>Builds a draft environment from already-parsed JSON values (carried bytes-to-bytes — no per-field
    /// strings) plus the resolved management tags, for a store to complete with the server-stamped
    /// createdBy/createdAt/etag. Pass <see langword="default"/> (an undefined element) for any field the body omits; for
    /// an update, pass <see langword="default"/> for the immutable <paramref name="name"/> and tags — the store carries
    /// those forward from the stored environment.</summary>
    /// <param name="name">The environment name value (or undefined for an update). A present name must be inside
    /// the <see cref="EnvironmentName"/> grammar — it is half of every run's composite store key.</param>
    /// <param name="displayName">The display-name value (or undefined).</param>
    /// <param name="description">The description value (or undefined).</param>
    /// <param name="managementTags">The resolved management tags (empty for an update).</param>
    /// <param name="requireEvidence">The require-evidence flag value (or undefined — absent on create means the
    /// default-off behaviour; absent on update leaves the stored flag unchanged).</param>
    /// <param name="allowsDraftRuns">The draft-run permission flag value (or undefined — same absent semantics as
    /// <paramref name="requireEvidence"/>).</param>
    /// <param name="requiredIsolation">The required run isolation value (ADR 0058) (or undefined — absent on create
    /// means the in-process default; absent on update leaves the stored requirement unchanged).</param>
    /// <param name="runtimeIdentifier">The serverless build-target RID value (ADR 0055) (or undefined — absent on
    /// create means the <c>linux-x64</c> default; absent on update leaves the stored target unchanged).</param>
    /// <returns>A pooled, disposable draft document; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<Environment> Draft(
        in JsonElement name,
        in JsonElement displayName,
        in JsonElement description,
        in SecurityTagSet managementTags,
        in JsonElement requireEvidence = default,
        in JsonElement allowsDraftRuns = default,
        in JsonElement requiredIsolation = default,
        in JsonElement runtimeIdentifier = default)
    {
        if (name.ValueKind is not JsonValueKind.Undefined)
        {
            if (name.ValueKind is not JsonValueKind.String)
            {
                throw ThrowHelper.GetEnvironmentNameOutsideGrammarException(name.GetRawText(), nameof(name));
            }

            using UnescapedUtf8JsonString nameUtf8 = name.GetUtf8String();
            if (!EnvironmentName.IsWellFormedUtf8(nameUtf8.Span))
            {
                throw ThrowHelper.GetEnvironmentNameOutsideGrammarException(name.GetString()!, nameof(name));
            }
        }

        DraftElements state = new(name, displayName, description, managementTags, requireEvidence, allowsDraftRuns, requiredIsolation, runtimeIdentifier);
        return PersistedJson.ToPooledDocument<Environment, DraftElements>(
            state,
            static (Utf8JsonWriter writer, in DraftElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, s.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, s.DisplayName);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, s.Description);
                WriteValueIfPresent(writer, JsonPropertyNames.RequireEvidenceUtf8, s.RequireEvidence);
                WriteValueIfPresent(writer, JsonPropertyNames.AllowsDraftRunsUtf8, s.AllowsDraftRuns);
                WriteValueIfPresent(writer, JsonPropertyNames.RequiredIsolationUtf8, s.RequiredIsolation);
                WriteValueIfPresent(writer, JsonPropertyNames.RuntimeIdentifierUtf8, s.RuntimeIdentifier);
                if (!s.ManagementTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.ManagementTags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Builds a draft for the control plane's own internal environment, carrying the platform marker (ADR 0065) that
    /// excludes it from the tenancy invariant's owner-group count.
    /// </summary>
    /// <param name="name">The environment name.</param>
    /// <param name="displayName">The optional display name (omitted when <see langword="null"/>).</param>
    /// <param name="description">The optional description (omitted when <see langword="null"/>).</param>
    /// <param name="managementTags">The resolved management tags (omitted when empty).</param>
    /// <returns>A pooled, disposable draft document.</returns>
    /// <remarks>
    /// This is a separate factory rather than a flag on <see cref="Draft(string, string?, string?, SecurityTagSet)"/>
    /// because the marker's whole value is that no request can produce it. A parameter on the shared factory would sit
    /// one argument away from the API handler's create path, where the exclusion it grants is exactly what an attacker
    /// wants. The marker is what excludes the row from the count; the management tags still scope who may see and
    /// manage it, so they are carried exactly as for any other environment.
    /// </remarks>
    public static ParsedJsonDocument<Environment> DraftPlatform(string name, string? displayName, string? description, SecurityTagSet managementTags)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!EnvironmentName.IsWellFormed(name))
        {
            throw ThrowHelper.GetEnvironmentNameOutsideGrammarException(name, nameof(name));
        }

        return PersistedJson.ToPooledDocument<Environment, (string Name, string? Display, string? Desc, SecurityTagSet Tags)>(
            (name, displayName, description, managementTags),
            static (Utf8JsonWriter writer, in (string Name, string? Display, string? Desc, SecurityTagSet Tags) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.NameUtf8, s.Name);
                if (s.Display is { } display)
                {
                    writer.WriteString(JsonPropertyNames.DisplayNameUtf8, display);
                }

                if (s.Desc is { } description)
                {
                    writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
                }

                if (!s.Tags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.Tags.WriteTo(writer);
                }

                writer.WriteBoolean(JsonPropertyNames.PlatformUtf8, true);
                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Builds a draft that changes <em>only</em> the key generations (ADR 0065), echoing every other mutable value
    /// from the stored environment bytes-to-bytes.
    /// </summary>
    /// <remarks>
    /// This exists because <see cref="WriteUpdated"/> takes displayName and description from the draft alone, so a
    /// minimal draft carrying just the generations would blank them. Registering or retiring a key is a partial
    /// update expressed through a full-replace write path, and echoing the stored values here keeps that knowledge in
    /// one place rather than in the key handler, which has no business knowing the environment's field list.
    /// </remarks>
    /// <param name="stored">The stored environment to echo.</param>
    /// <param name="keyGenerations">The complete new generation set.</param>
    /// <returns>A pooled draft document.</returns>
    public static ParsedJsonDocument<Environment> DraftWithKeyGenerations(in Environment stored, in EnvironmentKeyGenerationArray keyGenerations)
    {
        KeyGenerationElements state = new(
            (JsonElement)stored.Name,
            (JsonElement)stored.DisplayName,
            (JsonElement)stored.Description,
            (JsonElement)stored.ManagementTags,
            (JsonElement)stored.RequireEvidence,
            (JsonElement)stored.AllowsDraftRuns,
            (JsonElement)stored.RequiredIsolation,
            (JsonElement)stored.RuntimeIdentifier,
            (JsonElement)keyGenerations);

        return PersistedJson.ToPooledDocument<Environment, KeyGenerationElements>(
            state,
            static (Utf8JsonWriter writer, in KeyGenerationElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, s.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, s.DisplayName);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, s.Description);
                WriteValueIfPresent(writer, JsonPropertyNames.RequireEvidenceUtf8, s.RequireEvidence);
                WriteValueIfPresent(writer, JsonPropertyNames.AllowsDraftRunsUtf8, s.AllowsDraftRuns);
                WriteValueIfPresent(writer, JsonPropertyNames.RequiredIsolationUtf8, s.RequiredIsolation);
                WriteValueIfPresent(writer, JsonPropertyNames.RuntimeIdentifierUtf8, s.RuntimeIdentifier);
                WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, s.ManagementTags);
                WriteValueIfPresent(writer, JsonPropertyNames.KeyGenerationsUtf8, s.KeyGenerations);
                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Builds a draft that appends a newly registered key generation (ADR 0065), echoing every other mutable value
    /// and copying the existing generations bytes-to-bytes.
    /// </summary>
    /// <param name="stored">The stored environment.</param>
    /// <param name="keyId">The generation's id.</param>
    /// <param name="sealPublicKey">The public seal key, base64url SPKI.</param>
    /// <param name="algorithm">The seal key's signature algorithm.</param>
    /// <param name="registeredBy">The registering actor.</param>
    /// <param name="registeredAt">The registration instant.</param>
    /// <returns>A pooled draft document.</returns>
    public static ParsedJsonDocument<Environment> DraftWithKeyRegistered(
        in Environment stored, string keyId, in JsonElement sealPublicKey, in JsonElement algorithm, string registeredBy, DateTimeOffset registeredAt)
        => DraftWithKeyMutation(new KeyMutation(stored, keyId, sealPublicKey, algorithm, registeredBy, registeredAt, reason: null, retire: false));

    /// <summary>
    /// Builds a draft that marks a generation Retired (ADR 0065). Retirement is recorded rather than removed, so a
    /// checkpoint written under the generation stays attributable.
    /// </summary>
    /// <param name="stored">The stored environment.</param>
    /// <param name="keyId">The generation to retire.</param>
    /// <param name="retiredBy">The retiring actor.</param>
    /// <param name="retiredAt">The retirement instant.</param>
    /// <param name="reason">An optional note.</param>
    /// <returns>A pooled draft document.</returns>
    public static ParsedJsonDocument<Environment> DraftWithKeyRetired(
        in Environment stored, string keyId, string retiredBy, DateTimeOffset retiredAt, string? reason)
        => DraftWithKeyMutation(new KeyMutation(stored, keyId, sealPublicKey: default, algorithm: default, retiredBy, retiredAt, reason, retire: true));

    private static ParsedJsonDocument<Environment> DraftWithKeyMutation(in KeyMutation mutation)
        => PersistedJson.ToPooledDocument<Environment, KeyMutation>(
            mutation,
            static (Utf8JsonWriter writer, in KeyMutation m) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, m.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, m.DisplayName);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, m.Description);
                WriteValueIfPresent(writer, JsonPropertyNames.RequireEvidenceUtf8, m.RequireEvidence);
                WriteValueIfPresent(writer, JsonPropertyNames.AllowsDraftRunsUtf8, m.AllowsDraftRuns);
                WriteValueIfPresent(writer, JsonPropertyNames.RequiredIsolationUtf8, m.RequiredIsolation);
                WriteValueIfPresent(writer, JsonPropertyNames.RuntimeIdentifierUtf8, m.RuntimeIdentifier);
                WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, m.ManagementTags);

                writer.WritePropertyName(JsonPropertyNames.KeyGenerationsUtf8);
                writer.WriteStartArray();

                // An environment that has registered nothing has no keyGenerations property at all, and enumerating
                // an undefined array throws rather than yielding nothing. Absent and empty are the same thing here.
                foreach (EnvironmentKeyGeneration generation in Enumerate(m.Existing))
                {
                    if (m.Retire && generation.KeyId.ValueEquals(m.KeyId))
                    {
                        WriteRetired(writer, generation, m);
                    }
                    else
                    {
                        // Untouched generations are copied verbatim: never reformatted, never re-realised.
                        ((JsonElement)generation).WriteTo(writer);
                    }
                }

                if (!m.Retire)
                {
                    WriteRegistered(writer, m);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });

    /// <summary>
    /// Enumerates a generation set that may be absent. An environment which has registered nothing carries no
    /// <c>keyGenerations</c> property, and the generated array throws on enumeration when it is undefined, so every
    /// reader would otherwise need this guard.
    /// </summary>
    /// <param name="generations">The (possibly absent) set.</param>
    /// <returns>The generations, or nothing.</returns>
    /// <remarks>
    /// Returns a struct enumerable, not an <see cref="IEnumerable{T}"/>. The generated array already enumerates
    /// through a struct (<c>ArrayEnumerator</c>), so expressing this guard as a <c>yield</c> iterator would put a
    /// heap-allocated state machine, plus an interface-dispatched enumerator, on every path that reads or rewrites a
    /// generation set — including the document write path, which runs per registration and per retirement.
    /// </remarks>
    public static KeyGenerationSet Enumerate(EnvironmentKeyGenerationArray generations) => new(generations);

    /// <summary>A present-or-absent view over a generation set, enumerated without allocating.</summary>
    public readonly struct KeyGenerationSet
    {
        private readonly EnvironmentKeyGenerationArray generations;
        private readonly bool present;

        internal KeyGenerationSet(EnvironmentKeyGenerationArray generations)
        {
            this.generations = generations;
            this.present = ((JsonElement)generations).ValueKind == JsonValueKind.Array;
        }

        /// <summary>Gets an enumerator over the generations (empty when the set is absent).</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator()
            => this.present ? new Enumerator(this.generations.EnumerateArray()) : default;

        /// <summary>Enumerates the generations. Yields nothing when the set is absent, in which case the underlying
        /// enumerator is never touched.</summary>
        public struct Enumerator
        {
            private ArrayEnumerator<EnvironmentKeyGeneration> inner;
            private readonly bool present;

            internal Enumerator(ArrayEnumerator<EnvironmentKeyGeneration> inner)
            {
                this.inner = inner;
                this.present = true;
            }

            /// <summary>Gets the generation at the current position.</summary>
            public readonly EnvironmentKeyGeneration Current => this.inner.Current;

            /// <summary>Advances to the next generation.</summary>
            /// <returns><see langword="true"/> if there is one.</returns>
            public bool MoveNext() => this.present && this.inner.MoveNext();
        }
    }

    private static void WriteRegistered(Utf8JsonWriter writer, in KeyMutation m)
    {
        writer.WriteStartObject();
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.KeyIdUtf8, m.KeyId);
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.SealPublicKeyUtf8, m.SealPublicKey);
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.AlgorithmUtf8, m.Algorithm);
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.StateUtf8, "Active");
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.RegisteredByUtf8, m.Actor);
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.RegisteredAtUtf8, m.At);
        writer.WriteEndObject();
    }

    private static void WriteRetired(Utf8JsonWriter writer, in EnvironmentKeyGeneration generation, in KeyMutation m)
    {
        writer.WriteStartObject();
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.KeyIdUtf8, (JsonElement)generation.KeyId);
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.SealPublicKeyUtf8, (JsonElement)generation.SealPublicKey);
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.AlgorithmUtf8, (JsonElement)generation.Algorithm);
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.StateUtf8, "Retired");
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.RegisteredByUtf8, (JsonElement)generation.RegisteredBy);
        WriteValueIfPresent(writer, EnvironmentKeyGeneration.JsonPropertyNames.RegisteredAtUtf8, (JsonElement)generation.RegisteredAt);
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.RetiredByUtf8, m.Actor);
        writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.RetiredAtUtf8, m.At);
        if (m.Reason is { } reason)
        {
            writer.WriteString(EnvironmentKeyGeneration.JsonPropertyNames.ReasonUtf8, reason);
        }

        writer.WriteEndObject();
    }

    /// <summary>Builds a draft environment from primitive values — the cold-path / test convenience over the bytes-native
    /// <see cref="Draft(in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>: the name and optional
    /// display name/description are written straight into the draft document (the genuine construction leaf), plus the
    /// resolved management tags. No intermediate record.</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="displayName">The optional display name (omitted when <see langword="null"/>).</param>
    /// <param name="description">The optional description (omitted when <see langword="null"/>).</param>
    /// <param name="managementTags">The resolved management tags (omitted when empty).</param>
    /// <returns>A pooled, disposable draft document; the store reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<Environment> Draft(string name, string? displayName, string? description, SecurityTagSet managementTags)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!EnvironmentName.IsWellFormed(name))
        {
            throw ThrowHelper.GetEnvironmentNameOutsideGrammarException(name, nameof(name));
        }

        return PersistedJson.ToPooledDocument<Environment, (string Name, string? Display, string? Desc, SecurityTagSet Tags)>(
            (name, displayName, description, managementTags),
            static (Utf8JsonWriter writer, in (string Name, string? Display, string? Desc, SecurityTagSet Tags) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.NameUtf8, s.Name);
                if (s.Display is { } display)
                {
                    writer.WriteString(JsonPropertyNames.DisplayNameUtf8, display);
                }

                if (s.Desc is { } description)
                {
                    writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
                }

                if (!s.Tags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.Tags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Writes a brand-new environment's JSON into the caller's (pooled) writer in one pass — the draft's
    /// operator content is carried bytes-to-bytes and the server fields (createdBy/createdAt/etag) are stamped here.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor creating the environment (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in Environment draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireIdentity(draft);
        writer.WriteStartObject();
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)draft.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, (JsonElement)draft.DisplayName);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);
        WriteValueIfPresent(writer, JsonPropertyNames.RequireEvidenceUtf8, (JsonElement)draft.RequireEvidence);
        WriteValueIfPresent(writer, JsonPropertyNames.AllowsDraftRunsUtf8, (JsonElement)draft.AllowsDraftRuns);
        WriteValueIfPresent(writer, JsonPropertyNames.RequiredIsolationUtf8, (JsonElement)draft.RequiredIsolation);
        WriteValueIfPresent(writer, JsonPropertyNames.RuntimeIdentifierUtf8, (JsonElement)draft.RuntimeIdentifier);
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags);

        // Platform marker (ADR 0065): create is the ONLY moment it can be set, and only DraftPlatform emits it. Every
        // API-facing draft is built by a Draft overload that cannot, so no request body can reach this.
        WriteValueIfPresent(writer, JsonPropertyNames.PlatformUtf8, (JsonElement)draft.Platform);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes an updated copy of this environment. The immutable identity (<c>name</c>), the immutable security
    /// tags, and the created-* audit fields are carried through from the stored environment <strong>bytes-to-bytes</strong>
    /// (the original tokens, copied verbatim — never parsed-and-reformatted); the draft's mutable content (display name,
    /// description) is carried bytes-to-bytes; only the genuinely-new audit/concurrency values are written from params.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in Environment draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();

        // Immutable identity carried forward from the stored environment bytes-to-bytes, never from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)this.Name);

        // Platform marker (ADR 0065): read from the STORED environment, never the draft — deliberately not
        // replace-or-carry like the mutable values below. The marker excludes a row from the owner-group count, so a
        // draft that could set it would let an update opt a tenant environment out of the tenancy invariant, and one
        // that could clear it would let an update opt the platform's own environment in.
        WriteValueIfPresent(writer, JsonPropertyNames.PlatformUtf8, (JsonElement)this.Platform);

        // Mutable content carried bytes-to-bytes from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, (JsonElement)draft.DisplayName);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);

        // Promotion-readiness flag (workflow-designer design §4.6): an update that includes it replaces the stored
        // value; an update that omits it leaves the environment's requirement unchanged.
        WriteValuePreferringDraft(writer, JsonPropertyNames.RequireEvidenceUtf8, (JsonElement)draft.RequireEvidence, (JsonElement)this.RequireEvidence);

        // Draft-run permission (workflow-designer design §18): same replace-or-carry semantics as requireEvidence.
        WriteValuePreferringDraft(writer, JsonPropertyNames.AllowsDraftRunsUtf8, (JsonElement)draft.AllowsDraftRuns, (JsonElement)this.AllowsDraftRuns);

        // Required run isolation (ADR 0058): an update that includes it replaces the stored requirement; an update that
        // omits it leaves the environment's requirement unchanged (same replace-or-carry semantics as allowsDraftRuns).
        WriteValuePreferringDraft(writer, JsonPropertyNames.RequiredIsolationUtf8, (JsonElement)draft.RequiredIsolation, (JsonElement)this.RequiredIsolation);

        // Serverless build-target RID (ADR 0055): same replace-or-carry semantics — an update that omits it leaves the
        // environment's target unchanged.
        WriteValuePreferringDraft(writer, JsonPropertyNames.RuntimeIdentifierUtf8, (JsonElement)draft.RuntimeIdentifier, (JsonElement)this.RuntimeIdentifier);

        // Reach scope (§14.2): an administrator re-tag supplies managementTags on the draft (already merged with the
        // preserved deployment-internal tags by the handler) → take the draft's; an update that omits them carries the
        // stored tags forward bytes-to-bytes.
        WriteValuePreferringDraft(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags, (JsonElement)this.ManagementTags);

        // Key generations (ADR 0065): replace-or-carry, exactly like managementTags. The key endpoints are the only
        // caller that supplies them, through DraftWithKeyGenerations; every other update path builds its draft with a
        // Draft overload that cannot emit them, so an ordinary rename or re-description carries the stored generations
        // forward and cannot silently drop the last active one the tenancy invariant reads.
        WriteValuePreferringDraft(writer, JsonPropertyNames.KeyGenerationsUtf8, (JsonElement)draft.KeyGenerations, (JsonElement)this.KeyGenerations);

        // created-* audit carried forward bytes-to-bytes (copy the stored tokens verbatim — no parse/reformat).
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedByUtf8, (JsonElement)this.CreatedBy);
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedAtUtf8, (JsonElement)this.CreatedAt);

        // Genuinely-new values from typed params.
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
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

    // A create draft must carry the immutable identity (name); an update draft omits it (the store carries it forward).
    private static void RequireIdentity(in Environment draft)
    {
        if (!draft.Name.IsNotUndefined())
        {
            ThrowHelper.ThrowEnvironmentRequiresName();
        }

        using UnescapedUtf8JsonString name = draft.Name.GetUtf8String();
        if (name.Span.IsEmpty)
        {
            ThrowHelper.ThrowEnvironmentRequiresNonEmptyName();
        }
    }

    // One key mutation, with every echoed value read once from the stored environment.
    private readonly struct KeyMutation
    {
        public KeyMutation(in Environment stored, string keyId, in JsonElement sealPublicKey, in JsonElement algorithm, string actor, DateTimeOffset at, string? reason, bool retire)
        {
            this.Name = (JsonElement)stored.Name;
            this.DisplayName = (JsonElement)stored.DisplayName;
            this.Description = (JsonElement)stored.Description;
            this.ManagementTags = (JsonElement)stored.ManagementTags;
            this.RequireEvidence = (JsonElement)stored.RequireEvidence;
            this.AllowsDraftRuns = (JsonElement)stored.AllowsDraftRuns;
            this.RequiredIsolation = (JsonElement)stored.RequiredIsolation;
            this.RuntimeIdentifier = (JsonElement)stored.RuntimeIdentifier;
            this.Existing = stored.KeyGenerations;
            this.KeyId = keyId;
            this.SealPublicKey = sealPublicKey;
            this.Algorithm = algorithm;
            this.Actor = actor;
            this.At = at;
            this.Reason = reason;
            this.Retire = retire;
        }

        public JsonElement Name { get; }

        public JsonElement DisplayName { get; }

        public JsonElement Description { get; }

        public JsonElement ManagementTags { get; }

        public JsonElement RequireEvidence { get; }

        public JsonElement AllowsDraftRuns { get; }

        public JsonElement RequiredIsolation { get; }

        public JsonElement RuntimeIdentifier { get; }

        public EnvironmentKeyGenerationArray Existing { get; }

        public string KeyId { get; }

        public JsonElement SealPublicKey { get; }

        public JsonElement Algorithm { get; }

        public string Actor { get; }

        public DateTimeOffset At { get; }

        public string? Reason { get; }

        public bool Retire { get; }
    }

    // The key-generation draft context: every mutable value echoed from the stored environment, plus the new set.
    private readonly struct KeyGenerationElements(
        JsonElement name,
        JsonElement displayName,
        JsonElement description,
        JsonElement managementTags,
        JsonElement requireEvidence,
        JsonElement allowsDraftRuns,
        JsonElement requiredIsolation,
        JsonElement runtimeIdentifier,
        JsonElement keyGenerations)
    {
        public JsonElement Name { get; } = name;

        public JsonElement DisplayName { get; } = displayName;

        public JsonElement Description { get; } = description;

        public JsonElement ManagementTags { get; } = managementTags;

        public JsonElement RequireEvidence { get; } = requireEvidence;

        public JsonElement AllowsDraftRuns { get; } = allowsDraftRuns;

        public JsonElement RequiredIsolation { get; } = requiredIsolation;

        public JsonElement RuntimeIdentifier { get; } = runtimeIdentifier;

        public JsonElement KeyGenerations { get; } = keyGenerations;
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values plus the resolved tag set.
    private readonly struct DraftElements(
        JsonElement name,
        JsonElement displayName,
        JsonElement description,
        SecurityTagSet managementTags,
        JsonElement requireEvidence,
        JsonElement allowsDraftRuns,
        JsonElement requiredIsolation,
        JsonElement runtimeIdentifier)
    {
        public JsonElement Name { get; } = name;

        public JsonElement DisplayName { get; } = displayName;

        public JsonElement Description { get; } = description;

        public SecurityTagSet ManagementTags { get; } = managementTags;

        public JsonElement RequireEvidence { get; } = requireEvidence;

        public JsonElement AllowsDraftRuns { get; } = allowsDraftRuns;

        public JsonElement RequiredIsolation { get; } = requiredIsolation;

        public JsonElement RuntimeIdentifier { get; } = runtimeIdentifier;
    }
}
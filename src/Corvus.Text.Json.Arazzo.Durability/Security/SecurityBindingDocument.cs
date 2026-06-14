// <copyright file="SecurityBindingDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted claim→rule binding (design §14.2): the per-verb grants (read/write/purge) that a principal carrying
/// a matching claim resolves to, plus audit/concurrency metadata. This is the single binding type — generated from
/// <c>Schemas/SecurityBindingDocument.json</c> — used as the domain value <em>and</em> the persisted form, so a
/// store constructs it once (<see cref="CreateBinding"/>/<see cref="WithUpdate"/>, allocation-free) and writes its
/// JSON directly; there is no separate record and no reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/SecurityBindingDocument.json")]
public readonly partial struct SecurityBindingDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

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
    public DateTimeOffset CreatedAtValue => DateTimeOffset.Parse((string)this.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>Gets the actor that last updated the binding, or <see langword="null"/>.</summary>
    public string? UpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets when the binding was last updated, or <see langword="null"/>.</summary>
    public DateTimeOffset? UpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.Parse((string)this.LastUpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Builds a new binding, allocation-free, detached and ready to persist.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="definition">The binding content (claim match + per-verb grants).</param>
    /// <param name="actor">The actor creating the binding (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    /// <returns>The binding.</returns>
    public static SecurityBindingDocument CreateBinding(string id, SecurityBindingDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            claimType: definition.ClaimType,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            id: id,
            order: definition.Order,
            purge: definition.Purge,
            read: definition.Read,
            write: definition.Write,
            claimValue: definition.ClaimValue is { } claimValue ? (JsonString.Source)claimValue : default,
            description: definition.Description is { } description ? (JsonString.Source)description : default);
        return builder.RootElement.Clone();
    }

    /// <summary>Builds an updated copy of this binding, allocation-free (preserving id/created metadata).</summary>
    /// <param name="definition">The new binding content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    /// <returns>The updated binding.</returns>
    public SecurityBindingDocument WithUpdate(SecurityBindingDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            claimType: definition.ClaimType,
            createdAt: this.CreatedAt,
            createdBy: this.CreatedBy,
            etag: etag.Value ?? string.Empty,
            id: this.Id,
            order: definition.Order,
            purge: definition.Purge,
            read: definition.Read,
            write: definition.Write,
            claimValue: definition.ClaimValue is { } claimValue ? (JsonString.Source)claimValue : default,
            description: definition.Description is { } description ? (JsonString.Source)description : default,
            lastUpdatedAt: updatedAt,
            lastUpdatedBy: actor);
        return builder.RootElement.Clone();
    }

    /// <summary>Parses a binding from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The binding.</returns>
    public static SecurityBindingDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<SecurityBindingDocument> doc = ParsedJsonDocument<SecurityBindingDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this binding to its persisted JSON form.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// A per-verb grant: either <see cref="IsUnrestrictedValue"/> access (a <see langword="null"/> reach — the
    /// operator escape) or a set of rule names ANDed together. The generated grant type; convenience helpers and
    /// allocation-free factories let callers read and construct grants without touching JSON.
    /// </summary>
    public readonly partial struct VerbGrantInfo
    {
        /// <summary>Gets a grant that confers nothing (the verb is not granted by this binding).</summary>
        public static VerbGrantInfo None => Build(unrestricted: false);

        /// <summary>Gets a grant of unrestricted (full-reach) access for the verb.</summary>
        public static VerbGrantInfo Full => Build(unrestricted: true);

        /// <summary>Gets a value indicating whether the verb is unrestricted (full reach).</summary>
        public bool IsUnrestrictedValue => this.Unrestricted.IsNotUndefined() && (bool)this.Unrestricted;

        /// <summary>Gets a value indicating whether this grant confers nothing.</summary>
        public bool IsEmptyValue => !this.IsUnrestrictedValue && (!this.RuleNames.IsNotUndefined() || this.RuleNames.GetArrayLength() == 0);

        /// <summary>Gets the rule names ANDed for the verb (empty when unrestricted or ungranted).</summary>
        public IReadOnlyList<string> RuleNameList
        {
            get
            {
                var list = new List<string>();
                if (this.RuleNames.IsNotUndefined())
                {
                    foreach (JsonString ruleName in this.RuleNames.EnumerateArray())
                    {
                        list.Add((string)ruleName);
                    }
                }

                return list;
            }
        }

        /// <summary>Builds a grant of the conjunction of the named rules, detached and ready to use.</summary>
        /// <param name="ruleNames">The rule names (ANDed).</param>
        /// <returns>The grant.</returns>
        public static VerbGrantInfo Rules(params string[] ruleNames)
        {
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
                workspace,
                unrestricted: false,
                ruleNames: new JsonStringArray.Source((ref JsonStringArray.Builder array) =>
                {
                    foreach (string ruleName in ruleNames)
                    {
                        array.AddItem(ruleName);
                    }
                }));
            return builder.RootElement.Clone();
        }

        private static VerbGrantInfo Build(bool unrestricted)
        {
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using JsonDocumentBuilder<Mutable> builder = CreateBuilder(workspace, unrestricted: unrestricted);
            return builder.RootElement.Clone();
        }
    }
}
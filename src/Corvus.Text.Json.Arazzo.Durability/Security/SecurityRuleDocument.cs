// <copyright file="SecurityRuleDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted, named row-authorization rule (design §14.2): the rule expression in the security-rule grammar (see
/// <see cref="SecurityRule"/>) plus audit/concurrency metadata. This is the single rule type — generated from
/// <c>Schemas/SecurityRuleDocument.json</c> — used as the domain value <em>and</em> the persisted form, so a store
/// constructs it once (<see cref="CreateRule"/>/<see cref="WithUpdate"/>, allocation-free) and writes its JSON
/// directly (<see cref="ToJsonBytes"/>/<see cref="IJsonValue.WriteTo"/>); there is no separate record and no
/// reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/SecurityRuleDocument.json")]
public readonly partial struct SecurityRuleDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the rule's unique name.</summary>
    public string NameValue => (string)this.Name;

    /// <summary>Gets the rule text in the security-rule grammar.</summary>
    public string ExpressionValue => (string)this.Expression;

    /// <summary>Gets the optional human description, or <see langword="null"/>.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the actor that created the rule.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the rule was created.</summary>
    public DateTimeOffset CreatedAtValue => DateTimeOffset.Parse((string)this.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>Gets the actor that last updated the rule, or <see langword="null"/> if never updated.</summary>
    public string? UpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets when the rule was last updated, or <see langword="null"/> if never updated.</summary>
    public DateTimeOffset? UpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.Parse((string)this.LastUpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Builds a new rule, allocation-free, detached and ready to persist.</summary>
    /// <param name="name">The rule's unique name.</param>
    /// <param name="definition">The rule content (expression + optional description).</param>
    /// <param name="actor">The actor creating the rule (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    /// <returns>The rule.</returns>
    public static SecurityRuleDocument CreateRule(string name, SecurityRuleDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            expression: definition.Expression,
            name: name,
            description: definition.Description is { } description ? (JsonString.Source)description : default);
        return builder.RootElement.Clone();
    }

    /// <summary>Builds an updated copy of this rule, allocation-free (preserving name/created metadata).</summary>
    /// <param name="definition">The new rule content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    /// <returns>The updated rule.</returns>
    public SecurityRuleDocument WithUpdate(SecurityRuleDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            createdAt: this.CreatedAt,
            createdBy: this.CreatedBy,
            etag: etag.Value ?? string.Empty,
            expression: definition.Expression,
            name: this.Name,
            description: definition.Description is { } description ? (JsonString.Source)description : default,
            lastUpdatedAt: updatedAt,
            lastUpdatedBy: actor);
        return builder.RootElement.Clone();
    }

    /// <summary>Parses a rule from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The rule.</returns>
    public static SecurityRuleDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<SecurityRuleDocument> doc = ParsedJsonDocument<SecurityRuleDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this rule to its persisted JSON form.</summary>
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
}
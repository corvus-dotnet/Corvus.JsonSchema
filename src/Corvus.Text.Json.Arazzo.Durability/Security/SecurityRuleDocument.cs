// <copyright file="SecurityRuleDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted, named row-authorization rule (design §14.2): the rule expression in the security-rule grammar (see
/// <see cref="SecurityRule"/>) plus audit/concurrency metadata. This is the single rule type — generated from
/// <c>Schemas/SecurityRuleDocument.json</c> — used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// Construction threads the destination through: a store passes the buffer (or stream) it already owns and the rule
/// is realised and written into it in one pass (<see cref="WriteNewRule"/>/<see cref="WriteUpdatedRule"/>) — no
/// interim detached clone, no second serialization, and no array copied out of a hidden buffer. The backend then
/// consumes that span directly (Cosmos base64-encodes it; a SQL driver materialises a <see cref="byte"/> array only
/// at the ADO leaf that demands one), and where the value is needed back it is parsed once (<see cref="FromJson"/>).
/// The leaf accessors realise a <see cref="string"/> only where a string is actually required (a SQL parameter, a
/// rule to compile, an HTTP response field).
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/SecurityRuleDocument.json")]
public readonly partial struct SecurityRuleDocument
{
    /// <summary>Gets the rule's unique name.</summary>
    public string NameValue => (string)this.Name;

    /// <summary>Gets the rule text in the security-rule grammar.</summary>
    public string ExpressionValue => (string)this.Expression;

    /// <summary>Gets the optional human description, or <see langword="null"/>.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the actor that created the rule.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the rule was created.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets the actor that last updated the rule, or <see langword="null"/> if never updated.</summary>
    public string? UpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets when the rule was last updated, or <see langword="null"/> if never updated.</summary>
    public DateTimeOffset? UpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.LastUpdatedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Realises a new rule into a workspace and writes its JSON to the caller's (pooled) writer in one pass —
    /// a store serializes the result to its driver bytes via <see cref="PersistedJson.ToArray"/> and returns a pooled
    /// document over those bytes.</summary>
    /// <param name="writer">The writer to serialize into (typically the pooled writer from <see cref="PersistedJson"/>).</param>
    /// <param name="name">The rule's unique name.</param>
    /// <param name="definition">The rule content (expression + optional description).</param>
    /// <param name="actor">The actor creating the rule (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string name, SecurityRuleDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = BuildNew(workspace, name, definition, actor, createdAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Realises an updated copy of this rule (modifying only the fields the update touches; name/created
    /// metadata carried through) and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="definition">The new rule content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, SecurityRuleDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyUpdate(workspace, definition, actor, updatedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Parses a rule from its persisted JSON as a detached value (one owned copy). Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The rule.</returns>
    public static SecurityRuleDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    // Realises a new rule into the pooled workspace arena (shared by the buffer-writing and detached-value paths).
    private static JsonDocumentBuilder<Mutable> BuildNew(JsonWorkspace workspace, string name, SecurityRuleDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => CreateBuilder(
            workspace,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            expression: definition.Expression,
            name: name,
            description: definition.Description is { } description ? (JsonString.Source)description : default);

    // Realises a mutable builder over this document and modifies only the fields an update touches; name and the
    // created-* metadata are carried through unchanged (no field-by-field rebuild).
    private JsonDocumentBuilder<Mutable> ApplyUpdate(JsonWorkspace workspace, SecurityRuleDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetExpression(definition.Expression);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetLastUpdatedAt(updatedAt);
        builder.RootElement.SetLastUpdatedBy(actor);
        if (definition.Description is { } description)
        {
            builder.RootElement.SetDescription(description);
        }
        else
        {
            builder.RootElement.RemoveDescription();
        }

        return builder;
    }
}
// <copyright file="SecurityRuleDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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

    /// <summary>Realises a new rule and writes its canonical JSON into the caller's buffer in a single pass.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    /// <param name="name">The rule's unique name.</param>
    /// <param name="definition">The rule content (expression + optional description).</param>
    /// <param name="actor">The actor creating the rule (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNewRule(IBufferWriter<byte> buffer, string name, SecurityRuleDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
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
        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Realises an updated copy of this rule (preserving name/created metadata) into the caller's buffer.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    /// <param name="definition">The new rule content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdatedRule(IBufferWriter<byte> buffer, SecurityRuleDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Realise a mutable builder over this document and modify only the fields the update touches; name and the
        // created-* metadata are carried through unchanged (no field-by-field rebuild).
        using JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
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

        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Parses a rule from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The rule.</returns>
    public static SecurityRuleDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);
}
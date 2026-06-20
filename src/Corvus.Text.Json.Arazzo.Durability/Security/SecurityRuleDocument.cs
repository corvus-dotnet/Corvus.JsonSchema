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
    /// <param name="draft">The draft rule carrying the operator-supplied content (expression + optional description) as JSON values — read bytes-to-bytes.</param>
    /// <param name="actor">The actor creating the rule (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, string name, in SecurityRuleDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = BuildNew(workspace, name, draft, actor, createdAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Realises an updated copy of this rule (modifying only the fields the update touches; name/created
    /// metadata carried through) and writes its JSON to the caller's (pooled) writer.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft rule carrying the new operator-supplied content as JSON values — read bytes-to-bytes.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in SecurityRuleDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = this.ApplyUpdate(workspace, draft, actor, updatedAt, etag);
        builder.RootElement.WriteTo(writer);
    }

    /// <summary>Parses a rule from its persisted JSON as a detached value (one owned copy). Prefer
    /// <see cref="PersistedJson.ToPooledDocument{T}"/> on read paths to keep the buffer pooled.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The rule.</returns>
    public static SecurityRuleDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>
    /// Builds a draft rule from operator-supplied content (expression + optional description) for a store to complete
    /// with the server-stamped name/etag/created metadata — the programmatic counterpart of carrying an HTTP request
    /// body as the draft via <c>From</c>. The store reads only the content fields (bytes-to-bytes) and stamps the rest.
    /// </summary>
    /// <param name="expression">The rule text in the security-rule grammar.</param>
    /// <param name="description">An optional human description (omitted when <see langword="null"/>).</param>
    /// <returns>A pooled, disposable draft document carrying only the supplied content — <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before the draft is disposed.</returns>
    public static ParsedJsonDocument<SecurityRuleDocument> Draft(string expression, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument, (string Expression, string? Description)>(
            (expression, description),
            static (Utf8JsonWriter writer, in (string Expression, string? Description) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString("expression"u8, c.Expression);
                if (c.Description is { } description)
                {
                    writer.WriteString("description"u8, description);
                }

                writer.WriteEndObject();
            });
    }

    // Realises a new rule into the pooled workspace arena: the draft's operator content is carried bytes-to-bytes (its
    // JSON values flow straight into the builder); name and the server-stamped audit/concurrency fields are added here.
    private static JsonDocumentBuilder<Mutable> BuildNew(JsonWorkspace workspace, string name, in SecurityRuleDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => CreateBuilder(
            workspace,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            expression: draft.Expression,
            name: name,
            description: draft.Description.IsNotUndefined() ? (JsonString.Source)draft.Description : default);

    // Realises a mutable builder over this document and modifies only the fields an update touches, carrying the draft's
    // content bytes-to-bytes; name and the created-* metadata are carried through unchanged (no field-by-field rebuild).
    private JsonDocumentBuilder<Mutable> ApplyUpdate(JsonWorkspace workspace, in SecurityRuleDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetExpression(draft.Expression);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetLastUpdatedAt(updatedAt);
        builder.RootElement.SetLastUpdatedBy(actor);
        if (draft.Description.IsNotUndefined())
        {
            builder.RootElement.SetDescription(draft.Description);
        }
        else
        {
            builder.RootElement.RemoveDescription();
        }

        return builder;
    }
}
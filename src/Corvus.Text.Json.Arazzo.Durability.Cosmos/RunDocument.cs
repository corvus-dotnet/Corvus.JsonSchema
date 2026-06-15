// <copyright file="RunDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a workflow run, generated from <c>Schemas/RunDocument.json</c>. The store
/// writes it straight to a Cosmos stream from a checkpoint + index entry (<see cref="WriteJson"/>) and reads it back
/// from a stream response or query page (<see cref="FromJson"/> → <see cref="ToIndexEntry"/>) entirely through
/// Corvus.Text.Json — no reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/RunDocument.json")]
public readonly partial struct RunDocument
{
    /// <summary>Gets the run id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the status as its persisted string.</summary>
    public string StatusValue => (string)this.Status;

    /// <summary>Gets the awaiting correlation id, or <see langword="null"/>.</summary>
    public string? AwaitingCorrelationIdOrNull => this.AwaitingCorrelationId.IsNotUndefined() ? (string)this.AwaitingCorrelationId : null;

    /// <summary>Decodes the opaque checkpoint bytes.</summary>
    /// <returns>The checkpoint bytes.</returns>
    public byte[] CheckpointBytes() => Convert.FromBase64String((string)this.Checkpoint);

    /// <summary>
    /// Writes the run document's persisted JSON straight to <paramref name="writer"/> from a checkpoint and its index
    /// entry — no intermediate <see cref="RunDocument"/> value and no re-serialization (the store hands this to
    /// <c>CosmosJson.WriteToStream</c> so the run is serialized exactly once, into a pooled stream).
    /// </summary>
    /// <param name="writer">The writer to write the document to.</param>
    /// <param name="id">The run id.</param>
    /// <param name="checkpoint">The opaque checkpoint bytes.</param>
    /// <param name="index">The projected index entry.</param>
    public static void WriteJson(Utf8JsonWriter writer, WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id.Value);

        // Base64-encode the checkpoint straight into the writer — no intermediate base64 string (which would scale with
        // checkpoint size on every write). Read back by RunDocument.CheckpointBytes via Convert.FromBase64String.
        writer.WriteBase64String(JsonPropertyNames.CheckpointUtf8, checkpoint);
        writer.WriteString(JsonPropertyNames.StatusUtf8, index.Status.ToString());
        writer.WriteString(JsonPropertyNames.WorkflowIdUtf8, index.WorkflowId);
        writer.WriteNumber(JsonPropertyNames.CreatedAtUtf8, index.CreatedAt.ToUnixTimeMilliseconds());
        writer.WriteNumber(JsonPropertyNames.UpdatedAtUtf8, index.UpdatedAt.ToUnixTimeMilliseconds());
        if (index.DueAt is { } dueAt)
        {
            writer.WriteNumber(JsonPropertyNames.DueAtUtf8, dueAt.ToUnixTimeMilliseconds());
        }

        if (index.AwaitingChannel is { } awaitingChannel)
        {
            writer.WriteString(JsonPropertyNames.AwaitingChannelUtf8, awaitingChannel);
        }

        if (index.AwaitingCorrelationId is { } awaitingCorrelationId)
        {
            writer.WriteString(JsonPropertyNames.AwaitingCorrelationIdUtf8, awaitingCorrelationId);
        }

        if (index.ErrorType is { } errorType)
        {
            writer.WriteString(JsonPropertyNames.ErrorTypeUtf8, errorType);
        }

        if (index.CorrelationId is { } correlationId)
        {
            writer.WriteString(JsonPropertyNames.CorrelationIdUtf8, correlationId);
        }

        if (!index.Tags.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.TagsUtf8);
            index.Tags.WriteTo(writer);
        }

        if (index.SecurityTags is { Count: > 0 } securityTags)
        {
            writer.WriteStartArray(JsonPropertyNames.SecurityTagsUtf8);
            foreach (SecurityTag tag in securityTags)
            {
                writer.WriteStartObject();
                writer.WriteString(EmbeddedSecurityTag.JsonPropertyNames.KUtf8, tag.Key);
                writer.WriteString(EmbeddedSecurityTag.JsonPropertyNames.VUtf8, tag.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static RunDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<RunDocument> doc = ParsedJsonDocument<RunDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Projects this document back to its index entry.</summary>
    /// <returns>The index entry.</returns>
    public WorkflowRunIndexEntry ToIndexEntry()
    {
        List<SecurityTag>? securityTags = null;
        if (this.SecurityTags.IsNotUndefined())
        {
            securityTags = [];
            foreach (EmbeddedSecurityTag tag in this.SecurityTags.EnumerateArray())
            {
                securityTags.Add(new SecurityTag((string)tag.K, (string)tag.V));
            }
        }

        return new WorkflowRunIndexEntry(
            (string)this.WorkflowId,
            Enum.Parse<WorkflowRunStatus>((string)this.Status),
            DateTimeOffset.FromUnixTimeMilliseconds((long)this.CreatedAt),
            DateTimeOffset.FromUnixTimeMilliseconds((long)this.UpdatedAt),
            this.DueAt.IsNotUndefined() ? DateTimeOffset.FromUnixTimeMilliseconds((long)this.DueAt) : null,
            this.AwaitingChannel.IsNotUndefined() ? (string)this.AwaitingChannel : null,
            this.AwaitingCorrelationId.IsNotUndefined() ? (string)this.AwaitingCorrelationId : null,
            this.ErrorType.IsNotUndefined() ? (string)this.ErrorType : null,
            CorrelationId: this.CorrelationId.IsNotUndefined() ? (string)this.CorrelationId : null,
            Tags: TagSet.CopyFrom(this.Tags),
            SecurityTags: securityTags is { Count: > 0 } ? securityTags : null);
    }
}
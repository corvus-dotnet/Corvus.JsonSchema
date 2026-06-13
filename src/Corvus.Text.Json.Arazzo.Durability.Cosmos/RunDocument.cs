// <copyright file="RunDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a workflow run, generated from <c>Schemas/RunDocument.json</c>. The store
/// builds it from a checkpoint + index entry (<see cref="Create"/> → <see cref="ToJsonBytes"/>) and reads it back
/// from a stream response or query page (<see cref="FromJson"/> → <see cref="ToIndexEntry"/>) entirely through
/// Corvus.Text.Json — no reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/RunDocument.json")]
public readonly partial struct RunDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the run id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the status as its persisted string.</summary>
    public string StatusValue => (string)this.Status;

    /// <summary>Gets the awaiting correlation id, or <see langword="null"/>.</summary>
    public string? AwaitingCorrelationIdOrNull => this.AwaitingCorrelationId.IsNotUndefined() ? (string)this.AwaitingCorrelationId : null;

    /// <summary>Decodes the opaque checkpoint bytes.</summary>
    /// <returns>The checkpoint bytes.</returns>
    public byte[] CheckpointBytes() => Convert.FromBase64String((string)this.Checkpoint);

    /// <summary>Builds the document from a checkpoint and its index entry, detached and ready to persist.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="checkpoint">The opaque checkpoint bytes.</param>
    /// <param name="index">The projected index entry.</param>
    /// <returns>The document.</returns>
    public static RunDocument Create(WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, id.Value);
            writer.WriteString(JsonPropertyNames.CheckpointUtf8, Convert.ToBase64String(checkpoint));
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

            if (index.Tags is { Count: > 0 } tags)
            {
                writer.WriteStartArray(JsonPropertyNames.TagsUtf8);
                foreach (string tag in tags)
                {
                    writer.WriteStringValue(tag);
                }

                writer.WriteEndArray();
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

        using ParsedJsonDocument<RunDocument> doc = ParsedJsonDocument<RunDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static RunDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<RunDocument> doc = ParsedJsonDocument<RunDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this document to its persisted JSON form.</summary>
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

        List<string>? tags = null;
        if (this.Tags.IsNotUndefined())
        {
            tags = [];
            foreach (JsonString tag in this.Tags.EnumerateArray())
            {
                tags.Add((string)tag);
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
            Tags: tags is { Count: > 0 } ? tags : null,
            SecurityTags: securityTags is { Count: > 0 } ? securityTags : null);
    }
}
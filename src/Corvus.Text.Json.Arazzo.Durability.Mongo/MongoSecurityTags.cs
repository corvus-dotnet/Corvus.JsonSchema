// <copyright file="MongoSecurityTags.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using MongoDB.Bson;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// Maps a <see cref="SecurityTagSet"/> to and from its embedded BSON form (an array of <c>{ k, v }</c>
/// subdocuments) for the Mongo run-state and catalog stores. The BSON↔JSON transcode is inherent to a BSON
/// backend; it is centralized here so the two stores spell the embedding once.
/// </summary>
internal static class MongoSecurityTags
{
    /// <summary>Builds the embedded BSON value for a security-tag set (<see cref="BsonNull.Value"/> when empty).</summary>
    /// <param name="tags">The security tags.</param>
    /// <returns>A BSON array of <c>{ k, v }</c> subdocuments, or <see cref="BsonNull.Value"/>.</returns>
    public static BsonValue ToBson(SecurityTagSet tags)
    {
        if (tags.IsEmpty)
        {
            return BsonNull.Value;
        }

        var array = new BsonArray();
        foreach (SecurityTag tag in tags)
        {
            array.Add(new BsonDocument { ["k"] = tag.Key, ["v"] = tag.Value });
        }

        return array;
    }

    /// <summary>Reads the embedded BSON security tags into a deferred holder, normalizing <c>{ k, v }</c> to the canonical <c>{ key, value }</c> bytes.</summary>
    /// <param name="document">The Mongo document.</param>
    /// <returns>The security-tag set (empty when absent).</returns>
    public static SecurityTagSet Read(BsonDocument document)
    {
        if (!document.TryGetValue("securityTags", out BsonValue value) || value.IsBsonNull)
        {
            return default;
        }

        BsonArray array = value.AsBsonArray;
        if (array.Count == 0)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            foreach (BsonValue element in array)
            {
                BsonDocument tag = element.AsBsonDocument;
                writer.WriteStartObject();
                writer.WriteString("key"u8, tag["k"].AsString);
                writer.WriteString("value"u8, tag["v"].AsString);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();
            return SecurityTagSet.CopyFromJsonArray(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }
}
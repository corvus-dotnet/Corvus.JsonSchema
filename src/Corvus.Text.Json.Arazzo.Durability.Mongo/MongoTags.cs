// <copyright file="MongoTags.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using MongoDB.Bson;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// Reads the free-form <c>tags</c> BSON string array into the deferred <see cref="TagSet"/> holder for the Mongo
/// run-state and catalog stores — the free-form counterpart of <see cref="MongoSecurityTags"/>. The BSON string
/// elements are transcoded straight into the holder's canonical JSON bytes through a pooled <see cref="Utf8JsonWriter"/>,
/// so a list/search read does not stand up a LINQ projection + an intermediate managed-string <see cref="List{T}"/> per
/// row before re-encoding (the per-element <see cref="BsonValue.AsString"/> is the driver's already-materialised string).
/// </summary>
internal static class MongoTags
{
    /// <summary>Reads the free-form <c>tags</c> array into a deferred holder (empty when absent or empty).</summary>
    /// <param name="document">The Mongo document.</param>
    /// <returns>The tag set.</returns>
    public static TagSet Read(BsonDocument document)
    {
        if (!document.TryGetValue("tags", out BsonValue value) || value.IsBsonNull)
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
                writer.WriteStringValue(element.AsString);
            }

            writer.WriteEndArray();
            writer.Flush();
            return TagSet.CopyFromJsonArray(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }
}
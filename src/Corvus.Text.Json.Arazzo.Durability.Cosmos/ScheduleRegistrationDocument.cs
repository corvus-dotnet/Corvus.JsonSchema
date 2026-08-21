// <copyright file="ScheduleRegistrationDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a deployment-global schedule registration, generated from
/// <c>Schemas/ScheduleRegistrationDocument.json</c>. Round-tripped through Corvus.Text.Json — never a
/// reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/ScheduleRegistrationDocument.json")]
public readonly partial struct ScheduleRegistrationDocument
{
    /// <summary>Gets the registered environment.</summary>
    public string EnvironmentValue => (string)this.Environment;

    /// <summary>Gets the scheduler run's id.</summary>
    public string RunIdValue => (string)this.RunId;

    /// <summary>
    /// Writes a registration document's persisted JSON straight to <paramref name="writer"/> — no intermediate
    /// <see cref="ScheduleRegistrationDocument"/> value and no re-serialization (the registry hands this to
    /// <c>CosmosJson.WriteToStream</c> so the registration is serialized exactly once, into a pooled stream).
    /// </summary>
    /// <param name="writer">The writer to write the document to.</param>
    /// <param name="scheduleId">The deployment-globally-unique schedule id.</param>
    /// <param name="environment">The environment the schedule is pinned to.</param>
    /// <param name="runId">The scheduler run's id.</param>
    public static void WriteJson(Utf8JsonWriter writer, string scheduleId, string environment, string runId)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, scheduleId);
        writer.WriteString(JsonPropertyNames.EnvironmentUtf8, environment);
        writer.WriteString(JsonPropertyNames.RunIdUtf8, runId);
        writer.WriteEndObject();
    }

    /// <summary>Parses a registration document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static ScheduleRegistrationDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<ScheduleRegistrationDocument> doc = ParsedJsonDocument<ScheduleRegistrationDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }
}
// <copyright file="ExternalSchemaRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace TestUtilities;

/// <summary>
/// Registers a JSON-Schema-Test-Suite test case's <c>externalSchemas</c> with a document resolver.
/// </summary>
/// <remarks>
/// External schema URIs are not unique across the suite: the same URI can carry a different
/// schema in another test case. Callers must register them on a resolver that serves only the one
/// test case.
/// </remarks>
public static class ExternalSchemaRegistration
{
    /// <summary>
    /// Registers each external schema at its retrieval URI.
    /// </summary>
    /// <param name="externalSchemas">The <c>externalSchemas</c> object as JSON text, or <see langword="null"/>.</param>
    /// <param name="addDocument">Adds a document to the resolver at the given URI.</param>
    public static void Register(string? externalSchemas, Action<string, JsonDocument> addDocument)
    {
        if (externalSchemas is null)
        {
            return;
        }

        using JsonDocument container = JsonDocument.Parse(externalSchemas);
        foreach (JsonProperty property in container.RootElement.EnumerateObject())
        {
            addDocument(property.Name, JsonDocument.Parse(property.Value.GetRawText()));
        }
    }
}
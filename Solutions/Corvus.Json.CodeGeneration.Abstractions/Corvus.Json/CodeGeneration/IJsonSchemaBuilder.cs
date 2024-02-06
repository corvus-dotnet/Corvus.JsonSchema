// <copyright file="IJsonSchemaBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Interface implemented by Json Schema Builders.
/// </summary>
public interface IJsonSchemaBuilder
{
    /// <summary>
    /// Adds a virtual document to the document resolver for this builder.
    /// </summary>
    /// <param name="path">The virtual path to the document.</param>
    /// <param name="jsonDocument">The document to add.</param>
    void AddDocument(string path, JsonDocument jsonDocument);

    /// <summary>
    /// Builds types for the schema provided by the given reference.
    /// </summary>
    /// <param name="reference">a uri-reference to the schema in which to build the types.</param>
    /// <param name="rootNamespace">The root namespace to use for types.</param>
    /// <param name="rebase">Indicates whether to rebase the root reference as if it were a root document.</param>
    /// <param name="baseUriToNamespaceMap">A map of base URIs to namespaces to use for specific types.</param>
    /// <param name="rootTypeName">A specific root type name for the root entity.</param>
    /// <returns>A <see cref="Task"/> which completes once the types are built. The tuple provides the root type name, and the generated types.</returns>
    Task<(string RootTypeName, ImmutableDictionary<JsonReference, TypeAndCode> GeneratedTypes)> BuildTypesFor(JsonReference reference, string rootNamespace, bool rebase = false, ImmutableDictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null);
}
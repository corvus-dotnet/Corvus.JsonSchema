// <copyright file="IWorkflowMetadataProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Builds the precomputed schema-metadata document baked into a workflow package at catalog-add time, so UIs
/// can render strongly-typed forms (typed inputs, a typed output/patch builder, a workflow editor) without
/// re-parsing the OpenAPI/AsyncAPI sources on the read side. The code-generation layer implements this; the
/// catalog depends only on this abstraction and treats the result as opaque UTF-8 JSON bytes.
/// </summary>
public interface IWorkflowMetadataProvider
{
    /// <summary>
    /// Builds the schema-metadata document for a workflow and its referenced source documents.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents (name to UTF-8 JSON bytes), keyed by their
    /// <c>sourceDescriptions</c> name.</param>
    /// <returns>The metadata document as UTF-8 JSON, or <see langword="null"/> if none can be produced.</returns>
    ReadOnlyMemory<byte>? BuildSchemas(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources);
}
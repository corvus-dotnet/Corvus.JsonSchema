// <copyright file="WorkflowSchemaMetadataProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// The code-generation-backed <see cref="IWorkflowMetadataProvider"/> — produces the package schema metadata
/// via <see cref="WorkflowSchemaMetadataGenerator"/>. Wire an instance into the catalog store/host so the
/// metadata is baked into each version at add time.
/// </summary>
public sealed class WorkflowSchemaMetadataProvider : IWorkflowMetadataProvider
{
    /// <inheritdoc/>
    public ReadOnlyMemory<byte>? BuildSchemas(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
        => WorkflowSchemaMetadataGenerator.Generate(workflowUtf8, sources);
}
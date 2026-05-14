// <copyright file="ClientModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The complete client model extracted from an OpenAPI or AsyncAPI specification.
/// This is the intermediate representation between the spec walker and the code emitter.
/// </summary>
public sealed class ClientModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientModel"/> class.
    /// </summary>
    /// <param name="title">The API title from the spec info object.</param>
    /// <param name="version">The API version from the spec info object.</param>
    /// <param name="description">Optional API description from the spec info object.</param>
    /// <param name="operations">The operations in the API.</param>
    /// <param name="schemaPointers">
    /// JSON pointers to all schemas reachable from the selected operations.
    /// These will be fed to the V5 <c>JsonSchemaTypeBuilder</c> for type generation.
    /// </param>
    public ClientModel(
        string? title,
        string? version,
        string? description,
        IReadOnlyList<ClientOperation> operations,
        IReadOnlyList<string> schemaPointers)
    {
        this.Title = title;
        this.Version = version;
        this.Description = description;
        this.Operations = operations;
        this.SchemaPointers = schemaPointers;
    }

    /// <summary>
    /// Gets the API title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the API version.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the API description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the operations in the API.
    /// </summary>
    public IReadOnlyList<ClientOperation> Operations { get; }

    /// <summary>
    /// Gets the JSON pointers to all schemas reachable from the selected operations.
    /// </summary>
    public IReadOnlyList<string> SchemaPointers { get; }

    /// <summary>
    /// Gets the operations grouped by their first tag. Operations with no tags
    /// are placed in a group with the key <c>"Default"</c>.
    /// </summary>
    /// <returns>A dictionary mapping tag names to their operations.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<ClientOperation>> GetOperationsByTag()
    {
        var groups = new Dictionary<string, List<ClientOperation>>(StringComparer.Ordinal);

        foreach (ClientOperation op in this.Operations)
        {
            string tag = op.Tags.Count > 0 ? op.Tags[0] : "Default";

            if (!groups.TryGetValue(tag, out List<ClientOperation>? list))
            {
                list = [];
                groups[tag] = list;
            }

            list.Add(op);
        }

        return groups.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<ClientOperation>)kvp.Value,
            StringComparer.Ordinal);
    }
}
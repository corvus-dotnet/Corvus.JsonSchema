namespace Corvus.Text.Json.Playground.Models;

/// <summary>
/// A bundled playground sample that includes schema files and user code.
/// </summary>
public class PlaygroundSample
{
    /// <summary>
    /// Gets the recipe identifier (e.g. "001").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display name shown in the dropdown (e.g. "Data Object").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the schema files bundled with this sample.
    /// </summary>
    public required IReadOnlyList<SampleSchemaFile> Schemas { get; init; }

    /// <summary>
    /// Gets the user code (Program.cs) for this sample, transformed for the playground.
    /// </summary>
    public required string UserCode { get; init; }
}

/// <summary>
/// A schema file within a playground sample.
/// </summary>
public class SampleSchemaFile
{
    /// <summary>
    /// Gets the filename (e.g. "person.json").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the JSON schema content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets a value indicating whether this schema is a root type for generation.
    /// </summary>
    public required bool IsRootType { get; init; }

    /// <summary>
    /// Gets the explicit .NET type name to use for this schema.
    /// When set, overrides the name derived from the schema content or filename.
    /// </summary>
    public string? TypeName { get; init; }
}

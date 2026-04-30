namespace Corvus.Text.Json.Playground.Models;

/// <summary>
/// Represents a JSON Schema file loaded into the playground.
/// </summary>
public class SchemaFile
{
    /// <summary>
    /// Gets or sets the display name for this schema file (e.g. "person.json").
    /// </summary>
    public string Name { get; set; } = "schema.json";

    /// <summary>
    /// Gets or sets the JSON content of the schema.
    /// </summary>
    public string Content { get; set; } = "{\n  \"type\": \"object\"\n}";

    /// <summary>
    /// Gets or sets a value indicating whether this schema should be generated as a root type.
    /// </summary>
    public bool IsRootType { get; set; }

    /// <summary>
    /// Gets or sets the explicit .NET type name to use for this schema.
    /// When set, overrides the name derived from the schema content or filename.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the clean content snapshot (set on load/save). Used for dirty tracking.
    /// </summary>
    public string CleanContent { get; set; } = "";

    /// <summary>
    /// Gets a value indicating whether the content has been modified since last load/save.
    /// </summary>
    public bool IsDirty => Content != CleanContent;

    /// <summary>
    /// Marks the current content as clean (e.g. after load or save).
    /// </summary>
    public void MarkClean() => CleanContent = Content;

    /// <summary>
    /// Gets or sets a value indicating whether this is a newly added file (not yet saved).
    /// </summary>
    public bool IsNew { get; set; }
}

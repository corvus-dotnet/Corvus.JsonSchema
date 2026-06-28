using System.Text.Json.Serialization;

namespace Corvus.Text.Json.TypeScript.Playground.Models;

/// <summary>
/// Shareable playground state encoded in the URL hash (the schema files + the user's TypeScript).
/// </summary>
public class UrlState
{
    /// <summary>
    /// Gets or sets the schema files.
    /// </summary>
    [JsonPropertyName("f")]
    public List<UrlFile>? Files { get; set; }

    /// <summary>
    /// Gets or sets the user's TypeScript source.
    /// </summary>
    [JsonPropertyName("u")]
    public string? UserCode { get; set; }
}

/// <summary>
/// A single schema file in the shared URL state.
/// </summary>
public class UrlFile
{
    /// <summary>
    /// Gets or sets the file name (e.g. "schema.json").
    /// </summary>
    [JsonPropertyName("n")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the schema content.
    /// </summary>
    [JsonPropertyName("c")]
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this file is generated as a root type.
    /// </summary>
    [JsonPropertyName("r")]
    public bool IsRoot { get; set; }
}

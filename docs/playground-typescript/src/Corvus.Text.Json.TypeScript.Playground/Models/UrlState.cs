using System.Text.Json.Serialization;

namespace Corvus.Text.Json.TypeScript.Playground.Models;

/// <summary>
/// Shareable playground state encoded in the URL hash (the schema + the user's TypeScript).
/// </summary>
public class UrlState
{
    /// <summary>
    /// Gets or sets the JSON Schema source.
    /// </summary>
    [JsonPropertyName("s")]
    public string? Schema { get; set; }

    /// <summary>
    /// Gets or sets the user's TypeScript source.
    /// </summary>
    [JsonPropertyName("u")]
    public string? UserCode { get; set; }
}

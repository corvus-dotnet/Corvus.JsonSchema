using System.Text.Json.Serialization;

namespace Corvus.Text.Json.JsonLogic.Playground.Models;

/// <summary>
/// A node in the schema property tree, representing a JSON Schema type
/// with its properties, array items, and type information.
/// Serialized to JSON and passed to the Blockly JS for dropdown population.
/// </summary>
public sealed class SchemaNode
{
    /// <summary>
    /// Gets the possible JSON types for this node (e.g. ["object"], ["string", "null"]).
    /// </summary>
    [JsonPropertyName("types")]
    public List<string> Types { get; init; } = [];

    /// <summary>
    /// Gets the named properties of this node (when types includes "object").
    /// Keys are JSON property names.
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, SchemaPropertyNode>? Properties { get; init; }

    /// <summary>
    /// Gets the schema node for array items (when types includes "array").
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SchemaNode? Items { get; init; }

    /// <summary>
    /// Gets whether this schema allows additional properties beyond those listed.
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AdditionalProperties { get; init; }
}

/// <summary>
/// A property within a schema node, extending SchemaNode with property-specific metadata.
/// </summary>
public sealed class SchemaPropertyNode
{
    /// <summary>
    /// Gets the possible JSON types for this property.
    /// </summary>
    [JsonPropertyName("types")]
    public List<string> Types { get; init; } = [];

    /// <summary>
    /// Gets whether this property is required.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Required { get; init; }

    /// <summary>
    /// Gets the named properties of this node (when types includes "object").
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, SchemaPropertyNode>? Properties { get; init; }

    /// <summary>
    /// Gets the schema node for array items (when types includes "array").
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SchemaNode? Items { get; init; }

    /// <summary>
    /// Gets whether this schema allows additional properties beyond those listed.
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AdditionalProperties { get; init; }
}

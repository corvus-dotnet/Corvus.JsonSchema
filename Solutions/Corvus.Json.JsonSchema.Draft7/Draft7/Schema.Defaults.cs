//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.Draft7;
/// <summary>
/// Core schema meta-schema
/// </summary>
public readonly partial struct Schema
{
    private static readonly ImmutableList<JsonObjectProperty> __CorvusDefaults = BuildDefaults();
    /// <inheritdoc/>
    public bool TryGetDefault(in JsonPropertyName name, out JsonAny value)
    {
        return __CorvusDefaults.TryGetValue(name, out value);
    }

    /// <inheritdoc/>
    public bool TryGetDefault(string name, out JsonAny value)
    {
        return __CorvusDefaults.TryGetValue(name, out value);
    }

    /// <inheritdoc/>
    public bool TryGetDefault(ReadOnlySpan<char> name, out JsonAny value)
    {
        return __CorvusDefaults.TryGetValue(name, out value);
    }

    /// <inheritdoc/>
    public bool TryGetDefault(ReadOnlySpan<byte> utf8Name, out JsonAny value)
    {
        return __CorvusDefaults.TryGetValue(utf8Name, out value);
    }

    /// <inheritdoc/>
    public bool HasDefault(in JsonPropertyName name)
    {
        return __CorvusDefaults.TryGetValue(name, out _);
    }

    /// <inheritdoc/>
    public bool HasDefault(string name)
    {
        return __CorvusDefaults.TryGetValue(name, out _);
    }

    /// <inheritdoc/>
    public bool HasDefault(ReadOnlySpan<char> name)
    {
        return __CorvusDefaults.TryGetValue(name, out _);
    }

    /// <inheritdoc/>
    public bool HasDefault(ReadOnlySpan<byte> utf8Name)
    {
        return __CorvusDefaults.TryGetValue(utf8Name, out _);
    }

    private static ImmutableList<JsonObjectProperty> BuildDefaults()
    {
        ImmutableList<JsonObjectProperty>.Builder builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(JsonPropertyNames.AdditionalItems, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.AdditionalProperties, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.Contains, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.Definitions, Corvus.Json.JsonSchema.Draft7.Schema.DefinitionsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Else, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.If, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.Items, Corvus.Json.JsonSchema.Draft7.Schema.ItemsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Not, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.PatternProperties, Corvus.Json.JsonSchema.Draft7.Schema.PatternPropertiesEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Properties, Corvus.Json.JsonSchema.Draft7.Schema.PropertiesEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.PropertyNames, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.ReadOnly, Corvus.Json.JsonSchema.Draft7.Schema.ReadOnlyEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Required, Corvus.Json.JsonSchema.Draft7.Schema.StringArray.DefaultInstance);
        builder.Add(JsonPropertyNames.Then, Corvus.Json.JsonSchema.Draft7.Schema.DefaultInstance);
        builder.Add(JsonPropertyNames.UniqueItems, Corvus.Json.JsonSchema.Draft7.Schema.UniqueItemsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.WriteOnly, Corvus.Json.JsonSchema.Draft7.Schema.WriteOnlyEntity.DefaultInstance);
        return builder.ToImmutable();
    }
}
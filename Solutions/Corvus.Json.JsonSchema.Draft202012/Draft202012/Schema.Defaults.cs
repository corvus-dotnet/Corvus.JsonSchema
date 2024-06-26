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

namespace Corvus.Json.JsonSchema.Draft202012;
/// <summary>
/// Core and Validation specifications meta-schema
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
        builder.Add(JsonPropertyNames.Definitions, Corvus.Json.JsonSchema.Draft202012.Schema.DefinitionsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Dependencies, Corvus.Json.JsonSchema.Draft202012.Schema.DependenciesEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.DependentSchemas, Corvus.Json.JsonSchema.Draft202012.Applicator.DependentSchemasEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Deprecated, Corvus.Json.JsonSchema.Draft202012.MetaData.DeprecatedEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.MinContains, Corvus.Json.JsonSchema.Draft202012.Validation.MinContainsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.MinItems, Corvus.Json.JsonSchema.Draft202012.Validation.NonNegativeIntegerDefault0.DefaultInstance);
        builder.Add(JsonPropertyNames.MinLength, Corvus.Json.JsonSchema.Draft202012.Validation.NonNegativeIntegerDefault0.DefaultInstance);
        builder.Add(JsonPropertyNames.MinProperties, Corvus.Json.JsonSchema.Draft202012.Validation.NonNegativeIntegerDefault0.DefaultInstance);
        builder.Add(JsonPropertyNames.PatternProperties, Corvus.Json.JsonSchema.Draft202012.Applicator.PatternPropertiesEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Properties, Corvus.Json.JsonSchema.Draft202012.Applicator.PropertiesEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.ReadOnly, Corvus.Json.JsonSchema.Draft202012.MetaData.ReadOnlyEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.Required, Corvus.Json.JsonSchema.Draft202012.Validation.StringArray.DefaultInstance);
        builder.Add(JsonPropertyNames.UniqueItems, Corvus.Json.JsonSchema.Draft202012.Validation.UniqueItemsEntity.DefaultInstance);
        builder.Add(JsonPropertyNames.WriteOnly, Corvus.Json.JsonSchema.Draft202012.MetaData.WriteOnlyEntity.DefaultInstance);
        return builder.ToImmutable();
    }
}
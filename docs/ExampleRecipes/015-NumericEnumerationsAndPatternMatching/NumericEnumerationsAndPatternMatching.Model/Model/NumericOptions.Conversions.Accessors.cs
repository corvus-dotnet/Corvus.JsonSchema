//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using Corvus.Json;

namespace JsonSchemaSample.Api;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct NumericOptions
{
    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Foo"/>.
    /// </summary>
    public JsonSchemaSample.Api.NumericOptions.Foo AsFoo
    {
        get
        {
            return (JsonSchemaSample.Api.NumericOptions.Foo)this;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid <see cref = "JsonSchemaSample.Api.NumericOptions.Foo"/>.
    /// </summary>
    public bool IsFoo
    {
        get
        {
            return ((JsonSchemaSample.Api.NumericOptions.Foo)this).IsValid();
        }
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Foo"/>.
    /// </summary>
    /// <param name = "result">The result of the conversion.</param>
    /// <returns><c>True</c> if the conversion was valid.</returns>
    public bool TryGetAsFoo(out JsonSchemaSample.Api.NumericOptions.Foo result)
    {
        result = (JsonSchemaSample.Api.NumericOptions.Foo)this;
        return result.IsValid();
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Bar"/>.
    /// </summary>
    public JsonSchemaSample.Api.NumericOptions.Bar AsBar
    {
        get
        {
            return (JsonSchemaSample.Api.NumericOptions.Bar)this;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid <see cref = "JsonSchemaSample.Api.NumericOptions.Bar"/>.
    /// </summary>
    public bool IsBar
    {
        get
        {
            return ((JsonSchemaSample.Api.NumericOptions.Bar)this).IsValid();
        }
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Bar"/>.
    /// </summary>
    /// <param name = "result">The result of the conversion.</param>
    /// <returns><c>True</c> if the conversion was valid.</returns>
    public bool TryGetAsBar(out JsonSchemaSample.Api.NumericOptions.Bar result)
    {
        result = (JsonSchemaSample.Api.NumericOptions.Bar)this;
        return result.IsValid();
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Baz"/>.
    /// </summary>
    public JsonSchemaSample.Api.NumericOptions.Baz AsBaz
    {
        get
        {
            return (JsonSchemaSample.Api.NumericOptions.Baz)this;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid <see cref = "JsonSchemaSample.Api.NumericOptions.Baz"/>.
    /// </summary>
    public bool IsBaz
    {
        get
        {
            return ((JsonSchemaSample.Api.NumericOptions.Baz)this).IsValid();
        }
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NumericOptions.Baz"/>.
    /// </summary>
    /// <param name = "result">The result of the conversion.</param>
    /// <returns><c>True</c> if the conversion was valid.</returns>
    public bool TryGetAsBaz(out JsonSchemaSample.Api.NumericOptions.Baz result)
    {
        result = (JsonSchemaSample.Api.NumericOptions.Baz)this;
        return result.IsValid();
    }
}
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
public readonly partial struct Pet
{
    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NewPet"/>.
    /// </summary>
    public JsonSchemaSample.Api.NewPet AsNewPet
    {
        get
        {
            return (JsonSchemaSample.Api.NewPet)this;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid <see cref = "JsonSchemaSample.Api.NewPet"/>.
    /// </summary>
    public bool IsNewPet
    {
        get
        {
            return ((JsonSchemaSample.Api.NewPet)this).IsValid();
        }
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.NewPet"/>.
    /// </summary>
    /// <param name = "result">The result of the conversion.</param>
    /// <returns><c>True</c> if the conversion was valid.</returns>
    public bool TryGetAsNewPet(out JsonSchemaSample.Api.NewPet result)
    {
        result = (JsonSchemaSample.Api.NewPet)this;
        return result.IsValid();
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.Pet.AllOf1Entity"/>.
    /// </summary>
    public JsonSchemaSample.Api.Pet.AllOf1Entity AsAllOf1Entity
    {
        get
        {
            return (JsonSchemaSample.Api.Pet.AllOf1Entity)this;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid <see cref = "JsonSchemaSample.Api.Pet.AllOf1Entity"/>.
    /// </summary>
    public bool IsAllOf1Entity
    {
        get
        {
            return ((JsonSchemaSample.Api.Pet.AllOf1Entity)this).IsValid();
        }
    }

    /// <summary>
    /// Gets the value as a <see cref = "JsonSchemaSample.Api.Pet.AllOf1Entity"/>.
    /// </summary>
    /// <param name = "result">The result of the conversion.</param>
    /// <returns><c>True</c> if the conversion was valid.</returns>
    public bool TryGetAsAllOf1Entity(out JsonSchemaSample.Api.Pet.AllOf1Entity result)
    {
        result = (JsonSchemaSample.Api.Pet.AllOf1Entity)this;
        return result.IsValid();
    }
}
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

namespace Corvus.Json.Benchmarking.Models.V3;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// A person's other (middle) names.
/// </para>
/// <para>
/// This may be either a single name represented as a string, or an array of strings, representing one or more other names.
/// </para>
/// </remarks>
public readonly partial struct OtherNames
{
    /// <summary>
    /// Conversion to <see cref = "Corvus.Json.Benchmarking.Models.V3.PersonNameElement"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static explicit operator Corvus.Json.Benchmarking.Models.V3.PersonNameElement(OtherNames value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return new(value.AsJsonElement);
        }

        if ((value.backing & Backing.String) != 0)
        {
            return new(value.stringBacking);
        }

        return Corvus.Json.Benchmarking.Models.V3.PersonNameElement.Undefined;
    }

    /// <summary>
    /// Conversion from <see cref = "Corvus.Json.Benchmarking.Models.V3.PersonNameElement"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator OtherNames(Corvus.Json.Benchmarking.Models.V3.PersonNameElement value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => new(value.GetString()!),
            _ => Undefined
        };
    }

    /// <summary>
    /// Conversion to <see cref = "Corvus.Json.Benchmarking.Models.V3.PersonNameElementArray"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static explicit operator Corvus.Json.Benchmarking.Models.V3.PersonNameElementArray(OtherNames value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return new(value.AsJsonElement);
        }

        if ((value.backing & Backing.Array) != 0)
        {
            return new(value.arrayBacking);
        }

        return Corvus.Json.Benchmarking.Models.V3.PersonNameElementArray.Undefined;
    }

    /// <summary>
    /// Conversion from <see cref = "Corvus.Json.Benchmarking.Models.V3.PersonNameElementArray"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator OtherNames(Corvus.Json.Benchmarking.Models.V3.PersonNameElementArray value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => new(value.AsImmutableList()),
            _ => Undefined
        };
    }
}
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

namespace Corvus.Json.Patch.Model;
/// <summary>
/// A type generated from a JsonSchema specification.
/// </summary>
public readonly partial struct Copy
{
    /// <summary>
    /// Conversion from <see cref = "Corvus.Json.Patch.Model.PatchOperationCommon"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator Copy(Corvus.Json.Patch.Model.PatchOperationCommon value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => new((ImmutableDictionary<JsonPropertyName, JsonAny>)value),
            _ => Undefined
        };
    }

    /// <summary>
    /// Conversion to <see cref = "Corvus.Json.Patch.Model.PatchOperationCommon"/>.
    /// </summary>
    /// <param name = "value">The value from which to convert.</param>
    public static implicit operator Corvus.Json.Patch.Model.PatchOperationCommon(Copy value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return new(value.AsJsonElement);
        }

        if ((value.backing & Backing.Object) != 0)
        {
            return new(value.objectBacking);
        }

        return Corvus.Json.Patch.Model.PatchOperationCommon.Undefined;
    }
}
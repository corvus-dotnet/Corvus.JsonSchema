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

namespace Corvus.Json.JsonSchema.Draft201909;
public readonly partial struct Validation
{
    /// <summary>
    /// A type generated from a JsonSchema specification.
    /// </summary>
    public readonly partial struct TypeEntity
    {
        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.Draft201909.Validation.SimpleTypes"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator TypeEntity(Corvus.Json.JsonSchema.Draft201909.Validation.SimpleTypes value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => new((string)value),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.Draft201909.Validation.SimpleTypes"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.Draft201909.Validation.SimpleTypes(TypeEntity value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.String) != 0)
            {
                return new(value.stringBacking);
            }

            return Corvus.Json.JsonSchema.Draft201909.Validation.SimpleTypes.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.Draft201909.Validation.TypeEntity.SimpleTypesArray"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator TypeEntity(Corvus.Json.JsonSchema.Draft201909.Validation.TypeEntity.SimpleTypesArray value)
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

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.Draft201909.Validation.TypeEntity.SimpleTypesArray"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.Draft201909.Validation.TypeEntity.SimpleTypesArray(TypeEntity value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Array) != 0)
            {
                return new(value.arrayBacking);
            }

            return Corvus.Json.JsonSchema.Draft201909.Validation.TypeEntity.SimpleTypesArray.Undefined;
        }
    }
}
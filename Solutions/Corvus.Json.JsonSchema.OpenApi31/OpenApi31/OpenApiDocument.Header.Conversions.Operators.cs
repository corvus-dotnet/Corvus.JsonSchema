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

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct Header
    {
        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredSchema"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredSchema(Header value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredSchema.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredSchema"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Header(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredSchema value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredContent"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredContent(Header value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredContent.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredContent"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Header(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredContent value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions(Header value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Header(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }
    }
}
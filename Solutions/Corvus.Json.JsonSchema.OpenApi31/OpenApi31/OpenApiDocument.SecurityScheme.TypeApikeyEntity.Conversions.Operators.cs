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
    public readonly partial struct SecurityScheme
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct TypeApikeyEntity
        {
            /// <summary>
            /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn(TypeApikeyEntity value)
            {
                if ((value.backing & Backing.JsonElement) != 0)
                {
                    return new(value.AsJsonElement);
                }

                if ((value.backing & Backing.Object) != 0)
                {
                    return new(value.objectBacking);
                }

                return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.Undefined;
            }

            /// <summary>
            /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static explicit operator TypeApikeyEntity(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn value)
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
}
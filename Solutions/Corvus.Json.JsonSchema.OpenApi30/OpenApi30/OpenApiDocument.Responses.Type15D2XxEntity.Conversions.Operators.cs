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

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    public readonly partial struct Responses
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct Type15D2XxEntity
        {
            /// <summary>
            /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Response"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static explicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Response(Type15D2XxEntity value)
            {
                if ((value.backing & Backing.JsonElement) != 0)
                {
                    return new(value.AsJsonElement);
                }

                if ((value.backing & Backing.Object) != 0)
                {
                    return new(value.objectBacking);
                }

                return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Response.Undefined;
            }

            /// <summary>
            /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Response"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static implicit operator Type15D2XxEntity(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Response value)
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
            /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static explicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference(Type15D2XxEntity value)
            {
                if ((value.backing & Backing.JsonElement) != 0)
                {
                    return new(value.AsJsonElement);
                }

                if ((value.backing & Backing.Object) != 0)
                {
                    return new(value.objectBacking);
                }

                return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference.Undefined;
            }

            /// <summary>
            /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference"/>.
            /// </summary>
            /// <param name = "value">The value from which to convert.</param>
            public static implicit operator Type15D2XxEntity(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference value)
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
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
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Schema and content are mutually exclusive, at least one is required
    /// </para>
    /// </remarks>
    public readonly partial struct SchemaXorContent
    {
        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.RequiredSchema"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.RequiredSchema(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.RequiredSchema.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.RequiredSchema"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.RequiredSchema value)
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
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent value)
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
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf0Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf0Entity(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf0Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf0Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf0Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf1Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf1Entity(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf1Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf1Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf1Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf2Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf2Entity(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf2Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf2Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf2Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf3Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf3Entity(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf3Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf3Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf3Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf4Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf4Entity(SchemaXorContent value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            return Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf4Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf4Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SchemaXorContent(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXorContent.SomePropertiesAreNotAllowedIfContentIsPresent.AllOf4Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                _ => Undefined
            };
        }
    }
}
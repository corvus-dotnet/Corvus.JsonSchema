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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct OAuth2SecurityScheme
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public const string Description = "description";
            /// <summary>
            /// JSON property name for <see cref = "Flows"/>.
            /// </summary>
            public static ReadOnlySpan<byte> FlowsUtf8 => "flows"u8;

            /// <summary>
            /// JSON property name for <see cref = "Flows"/>.
            /// </summary>
            public const string Flows = "flows";
            /// <summary>
            /// JSON property name for <see cref = "Type"/>.
            /// </summary>
            public static ReadOnlySpan<byte> TypeUtf8 => "type"u8;

            /// <summary>
            /// JSON property name for <see cref = "Type"/>.
            /// </summary>
            public const string Type = "type";
        }

        /// <summary>
        /// Gets the (optional) <c>description</c> property.
        /// </summary>
        public Corvus.Json.JsonString Description
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DescriptionUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Description, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the <c>flows</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows Flows
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.FlowsUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Flows, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the <c>type</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity Type
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.TypeUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Type, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "OAuth2SecurityScheme"/>.
        /// </summary>
        public static OAuth2SecurityScheme Create(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows flows, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity type, Corvus.Json.JsonString? description = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            builder.Add(JsonPropertyNames.Flows, flows.AsAny);
            builder.Add(JsonPropertyNames.Type, type.AsAny);
            if (description is Corvus.Json.JsonString description__)
            {
                builder.Add(JsonPropertyNames.Description, description__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public OAuth2SecurityScheme WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets flows.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public OAuth2SecurityScheme WithFlows(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows value)
        {
            return this.SetProperty(JsonPropertyNames.Flows, value);
        }

        /// <summary>
        /// Sets type.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public OAuth2SecurityScheme WithType(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Type, value);
        }

        private static ValidationContext __CorvusValidateType(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuth2SecurityScheme.TypeEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateFlows(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.OAuthFlows>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateDescription(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
        }

        /// <summary>
        /// Tries to get the validator for the given property.
        /// </summary>
        /// <param name = "property">The property for which to get the validator.</param>
        /// <param name = "hasJsonElementBacking"><c>True</c> if the object containing the property has a JsonElement backing.</param>
        /// <param name = "propertyValidator">The validator for the property, if provided by this schema.</param>
        /// <returns><c>True</c> if the validator was found.</returns>
        private bool __TryGetCorvusLocalPropertiesValidator(in JsonObjectProperty property, bool hasJsonElementBacking, [NotNullWhen(true)] out ObjectPropertyValidator? propertyValidator)
        {
            if (hasJsonElementBacking)
            {
                if (property.NameEquals(JsonPropertyNames.TypeUtf8))
                {
                    propertyValidator = __CorvusValidateType;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.FlowsUtf8))
                {
                    propertyValidator = __CorvusValidateFlows;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.DescriptionUtf8))
                {
                    propertyValidator = __CorvusValidateDescription;
                    return true;
                }
            }
            else
            {
                if (property.NameEquals(JsonPropertyNames.Type))
                {
                    propertyValidator = __CorvusValidateType;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Flows))
                {
                    propertyValidator = __CorvusValidateFlows;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Description))
                {
                    propertyValidator = __CorvusValidateDescription;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }
    }
}
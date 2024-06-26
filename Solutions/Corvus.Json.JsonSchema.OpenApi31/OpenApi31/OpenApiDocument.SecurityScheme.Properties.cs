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

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct SecurityScheme
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "BearerFormat"/>.
            /// </summary>
            public static ReadOnlySpan<byte> BearerFormatUtf8 => "bearerFormat"u8;

            /// <summary>
            /// JSON property name for <see cref = "BearerFormat"/>.
            /// </summary>
            public const string BearerFormat = "bearerFormat";
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
            /// JSON property name for <see cref = "In"/>.
            /// </summary>
            public static ReadOnlySpan<byte> InUtf8 => "in"u8;

            /// <summary>
            /// JSON property name for <see cref = "In"/>.
            /// </summary>
            public const string In = "in";
            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public static ReadOnlySpan<byte> NameUtf8 => "name"u8;

            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public const string Name = "name";
            /// <summary>
            /// JSON property name for <see cref = "OpenIdConnectUrl"/>.
            /// </summary>
            public static ReadOnlySpan<byte> OpenIdConnectUrlUtf8 => "openIdConnectUrl"u8;

            /// <summary>
            /// JSON property name for <see cref = "OpenIdConnectUrl"/>.
            /// </summary>
            public const string OpenIdConnectUrl = "openIdConnectUrl";
            /// <summary>
            /// JSON property name for <see cref = "Scheme"/>.
            /// </summary>
            public static ReadOnlySpan<byte> SchemeUtf8 => "scheme"u8;

            /// <summary>
            /// JSON property name for <see cref = "Scheme"/>.
            /// </summary>
            public const string Scheme = "scheme";
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
        /// Gets the (optional) <c>bearerFormat</c> property.
        /// </summary>
        public Corvus.Json.JsonString BearerFormat
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.BearerFormatUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.BearerFormat, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
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
        /// Gets the (optional) <c>flows</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows Flows
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Flows, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>in</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity In
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.InUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.In, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>name</c> property.
        /// </summary>
        public Corvus.Json.JsonString Name
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.NameUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Name, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>openIdConnectUrl</c> property.
        /// </summary>
        public Corvus.Json.JsonUri OpenIdConnectUrl
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.OpenIdConnectUrlUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonUri(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.OpenIdConnectUrl, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonUri>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>scheme</c> property.
        /// </summary>
        public Corvus.Json.JsonString Scheme
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.SchemeUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Scheme, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the <c>type</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity Type
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Type, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "SecurityScheme"/>.
        /// </summary>
        public static SecurityScheme Create(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity type, Corvus.Json.JsonString? bearerFormat = null, Corvus.Json.JsonString? description = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows? flows = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity? @in = null, Corvus.Json.JsonString? name = null, Corvus.Json.JsonUri? openIdConnectUrl = null, Corvus.Json.JsonString? scheme = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            builder.Add(JsonPropertyNames.Type, type.AsAny);
            if (bearerFormat is Corvus.Json.JsonString bearerFormat__)
            {
                builder.Add(JsonPropertyNames.BearerFormat, bearerFormat__.AsAny);
            }

            if (description is Corvus.Json.JsonString description__)
            {
                builder.Add(JsonPropertyNames.Description, description__.AsAny);
            }

            if (flows is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows flows__)
            {
                builder.Add(JsonPropertyNames.Flows, flows__.AsAny);
            }

            if (@in is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity @in__)
            {
                builder.Add(JsonPropertyNames.In, @in__.AsAny);
            }

            if (name is Corvus.Json.JsonString name__)
            {
                builder.Add(JsonPropertyNames.Name, name__.AsAny);
            }

            if (openIdConnectUrl is Corvus.Json.JsonUri openIdConnectUrl__)
            {
                builder.Add(JsonPropertyNames.OpenIdConnectUrl, openIdConnectUrl__.AsAny);
            }

            if (scheme is Corvus.Json.JsonString scheme__)
            {
                builder.Add(JsonPropertyNames.Scheme, scheme__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets bearerFormat.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithBearerFormat(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.BearerFormat, value);
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets flows.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithFlows(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.OauthFlows value)
        {
            return this.SetProperty(JsonPropertyNames.Flows, value);
        }

        /// <summary>
        /// Sets in.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithIn(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.InEntity value)
        {
            return this.SetProperty(JsonPropertyNames.In, value);
        }

        /// <summary>
        /// Sets name.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithName(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Name, value);
        }

        /// <summary>
        /// Sets openIdConnectUrl.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithOpenIdConnectUrl(in Corvus.Json.JsonUri value)
        {
            return this.SetProperty(JsonPropertyNames.OpenIdConnectUrl, value);
        }

        /// <summary>
        /// Sets scheme.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithScheme(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Scheme, value);
        }

        /// <summary>
        /// Sets type.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public SecurityScheme WithType(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Type, value);
        }

        private static ValidationContext __CorvusValidateType(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity>().Validate(validationContext, level);
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
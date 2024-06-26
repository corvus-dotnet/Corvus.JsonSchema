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
    public readonly partial struct ServerVariable
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "Default"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DefaultUtf8 => "default"u8;

            /// <summary>
            /// JSON property name for <see cref = "Default"/>.
            /// </summary>
            public const string Default = "default";
            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public const string Description = "description";
            /// <summary>
            /// JSON property name for <see cref = "Enum"/>.
            /// </summary>
            public static ReadOnlySpan<byte> EnumUtf8 => "enum"u8;

            /// <summary>
            /// JSON property name for <see cref = "Enum"/>.
            /// </summary>
            public const string Enum = "enum";
        }

        /// <summary>
        /// Gets the <c>default</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
        /// </summary>
        public Corvus.Json.JsonString Default
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DefaultUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Default, out JsonAny result))
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
        /// Gets the (optional) <c>enum</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray Enum
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.EnumUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Enum, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "ServerVariable"/>.
        /// </summary>
        public static ServerVariable Create(Corvus.Json.JsonString @default, Corvus.Json.JsonString? description = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray? @enum = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            builder.Add(JsonPropertyNames.Default, @default.AsAny);
            if (description is Corvus.Json.JsonString description__)
            {
                builder.Add(JsonPropertyNames.Description, description__.AsAny);
            }

            if (@enum is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray @enum__)
            {
                builder.Add(JsonPropertyNames.Enum, @enum__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets default.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ServerVariable WithDefault(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Default, value);
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ServerVariable WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets enum.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ServerVariable WithEnum(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray value)
        {
            return this.SetProperty(JsonPropertyNames.Enum, value);
        }

        private static ValidationContext __CorvusValidateDefault(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateEnum(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.ServerVariable.JsonStringArray>().Validate(validationContext, level);
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
                if (property.NameEquals(JsonPropertyNames.DefaultUtf8))
                {
                    propertyValidator = __CorvusValidateDefault;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.EnumUtf8))
                {
                    propertyValidator = __CorvusValidateEnum;
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
                if (property.NameEquals(JsonPropertyNames.Default))
                {
                    propertyValidator = __CorvusValidateDefault;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Enum))
                {
                    propertyValidator = __CorvusValidateEnum;
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
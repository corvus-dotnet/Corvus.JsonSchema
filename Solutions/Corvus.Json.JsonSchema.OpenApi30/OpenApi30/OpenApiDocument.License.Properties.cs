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
    public readonly partial struct License
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public static ReadOnlySpan<byte> NameUtf8 => "name"u8;

            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public const string Name = "name";
            /// <summary>
            /// JSON property name for <see cref = "Url"/>.
            /// </summary>
            public static ReadOnlySpan<byte> UrlUtf8 => "url"u8;

            /// <summary>
            /// JSON property name for <see cref = "Url"/>.
            /// </summary>
            public const string Url = "url";
        }

        /// <summary>
        /// Gets the <c>name</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
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
        /// Gets the (optional) <c>url</c> property.
        /// </summary>
        public Corvus.Json.JsonUriReference Url
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.UrlUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonUriReference(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Url, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonUriReference>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "License"/>.
        /// </summary>
        public static License Create(Corvus.Json.JsonString name, Corvus.Json.JsonUriReference? url = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            builder.Add(JsonPropertyNames.Name, name.AsAny);
            if (url is Corvus.Json.JsonUriReference url__)
            {
                builder.Add(JsonPropertyNames.Url, url__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets name.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public License WithName(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Name, value);
        }

        /// <summary>
        /// Sets url.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public License WithUrl(in Corvus.Json.JsonUriReference value)
        {
            return this.SetProperty(JsonPropertyNames.Url, value);
        }

        private static ValidationContext __CorvusValidateName(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateUrl(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonUriReference>().Validate(validationContext, level);
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
                if (property.NameEquals(JsonPropertyNames.NameUtf8))
                {
                    propertyValidator = __CorvusValidateName;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.UrlUtf8))
                {
                    propertyValidator = __CorvusValidateUrl;
                    return true;
                }
            }
            else
            {
                if (property.NameEquals(JsonPropertyNames.Name))
                {
                    propertyValidator = __CorvusValidateName;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Url))
                {
                    propertyValidator = __CorvusValidateUrl;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }
    }
}
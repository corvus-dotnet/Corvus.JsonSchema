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
    public readonly partial struct HttpSecurityScheme
    {
        public readonly partial struct NonBearer
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct RequiredBearerFormat
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
                }

                /// <summary>
                /// Gets the <c>bearerFormat</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
                /// </summary>
                public Corvus.Json.JsonAny BearerFormat
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
                                return new Corvus.Json.JsonAny(result);
                            }
                        }

                        if ((this.backing & Backing.Object) != 0)
                        {
                            if (this.objectBacking.TryGetValue(JsonPropertyNames.BearerFormat, out JsonAny result))
                            {
                                return result.As<Corvus.Json.JsonAny>();
                            }
                        }

                        return default;
                    }
                }

                /// <summary>
                /// Creates an instance of a <see cref = "RequiredBearerFormat"/>.
                /// </summary>
                public static RequiredBearerFormat Create(Corvus.Json.JsonAny bearerFormat)
                {
                    var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                    builder.Add(JsonPropertyNames.BearerFormat, bearerFormat.AsAny);
                    return new(builder.ToImmutable());
                }

                /// <summary>
                /// Sets bearerFormat.
                /// </summary>
                /// <param name = "value">The value to set.</param>
                /// <returns>The entity with the updated property.</returns>
                public RequiredBearerFormat WithBearerFormat(in Corvus.Json.JsonAny value)
                {
                    return this.SetProperty(JsonPropertyNames.BearerFormat, value);
                }

                private static ValidationContext __CorvusValidateBearerFormat(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
                {
                    return property.ValueAs<Corvus.Json.JsonAny>().Validate(validationContext, level);
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
                        if (property.NameEquals(JsonPropertyNames.BearerFormatUtf8))
                        {
                            propertyValidator = __CorvusValidateBearerFormat;
                            return true;
                        }
                    }
                    else
                    {
                        if (property.NameEquals(JsonPropertyNames.BearerFormat))
                        {
                            propertyValidator = __CorvusValidateBearerFormat;
                            return true;
                        }
                    }

                    propertyValidator = null;
                    return false;
                }
            }
        }
    }
}
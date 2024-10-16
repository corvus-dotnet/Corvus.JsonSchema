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
    public readonly partial struct SecurityScheme
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct TypeHttpBearerEntity
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
            /// Creates an instance of a <see cref = "TypeHttpBearerEntity"/>.
            /// </summary>
            public static TypeHttpBearerEntity Create(Corvus.Json.JsonString? bearerFormat = null)
            {
                var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                if (bearerFormat is Corvus.Json.JsonString bearerFormat__)
                {
                    builder.Add(JsonPropertyNames.BearerFormat, bearerFormat__.AsAny);
                }

                return new(builder.ToImmutable());
            }

            /// <summary>
            /// Sets bearerFormat.
            /// </summary>
            /// <param name = "value">The value to set.</param>
            /// <returns>The entity with the updated property.</returns>
            public TypeHttpBearerEntity WithBearerFormat(in Corvus.Json.JsonString value)
            {
                return this.SetProperty(JsonPropertyNames.BearerFormat, value);
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
                }
                else
                {
                }

                propertyValidator = null;
                return false;
            }
        }
    }
}
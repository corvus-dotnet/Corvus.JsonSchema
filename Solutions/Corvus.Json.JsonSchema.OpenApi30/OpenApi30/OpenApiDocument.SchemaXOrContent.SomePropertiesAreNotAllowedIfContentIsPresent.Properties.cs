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
    public readonly partial struct SchemaXOrContent
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Some properties are not allowed if content is present
        /// </para>
        /// </remarks>
        public readonly partial struct SomePropertiesAreNotAllowedIfContentIsPresent
        {
            /// <summary>
            /// The well-known property names in the JSON object.
            /// </summary>
            public static class JsonPropertyNames
            {
                /// <summary>
                /// JSON property name for <see cref = "Content"/>.
                /// </summary>
                public static ReadOnlySpan<byte> ContentUtf8 => "content"u8;

                /// <summary>
                /// JSON property name for <see cref = "Content"/>.
                /// </summary>
                public const string Content = "content";
            }

            /// <summary>
            /// Gets the <c>content</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
            /// </summary>
            public Corvus.Json.JsonAny Content
            {
                get
                {
                    if ((this.backing & Backing.JsonElement) != 0)
                    {
                        if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                        {
                            return default;
                        }

                        if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ContentUtf8, out JsonElement result))
                        {
                            return new Corvus.Json.JsonAny(result);
                        }
                    }

                    if ((this.backing & Backing.Object) != 0)
                    {
                        if (this.objectBacking.TryGetValue(JsonPropertyNames.Content, out JsonAny result))
                        {
                            return result.As<Corvus.Json.JsonAny>();
                        }
                    }

                    return default;
                }
            }

            /// <summary>
            /// Creates an instance of a <see cref = "SomePropertiesAreNotAllowedIfContentIsPresent"/>.
            /// </summary>
            public static SomePropertiesAreNotAllowedIfContentIsPresent Create(Corvus.Json.JsonAny content)
            {
                var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                builder.Add(JsonPropertyNames.Content, content.AsAny);
                return new(builder.ToImmutable());
            }

            /// <summary>
            /// Sets content.
            /// </summary>
            /// <param name = "value">The value to set.</param>
            /// <returns>The entity with the updated property.</returns>
            public SomePropertiesAreNotAllowedIfContentIsPresent WithContent(in Corvus.Json.JsonAny value)
            {
                return this.SetProperty(JsonPropertyNames.Content, value);
            }

            private static ValidationContext __CorvusValidateContent(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
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
                    if (property.NameEquals(JsonPropertyNames.ContentUtf8))
                    {
                        propertyValidator = __CorvusValidateContent;
                        return true;
                    }
                }
                else
                {
                    if (property.NameEquals(JsonPropertyNames.Content))
                    {
                        propertyValidator = __CorvusValidateContent;
                        return true;
                    }
                }

                propertyValidator = null;
                return false;
            }
        }
    }
}
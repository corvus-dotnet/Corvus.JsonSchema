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
    public readonly partial struct Example
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct RequiredValueAndExternalValue
        {
            /// <summary>
            /// The well-known property names in the JSON object.
            /// </summary>
            public static class JsonPropertyNames
            {
                /// <summary>
                /// JSON property name for <see cref = "ExternalValue"/>.
                /// </summary>
                public static ReadOnlySpan<byte> ExternalValueUtf8 => "externalValue"u8;

                /// <summary>
                /// JSON property name for <see cref = "ExternalValue"/>.
                /// </summary>
                public const string ExternalValue = "externalValue";
                /// <summary>
                /// JSON property name for <see cref = "Value"/>.
                /// </summary>
                public static ReadOnlySpan<byte> ValueUtf8 => "value"u8;

                /// <summary>
                /// JSON property name for <see cref = "Value"/>.
                /// </summary>
                public const string Value = "value";
            }

            /// <summary>
            /// Gets the <c>externalValue</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
            /// </summary>
            public Corvus.Json.JsonAny ExternalValue
            {
                get
                {
                    if ((this.backing & Backing.JsonElement) != 0)
                    {
                        if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                        {
                            return default;
                        }

                        if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ExternalValueUtf8, out JsonElement result))
                        {
                            return new Corvus.Json.JsonAny(result);
                        }
                    }

                    if ((this.backing & Backing.Object) != 0)
                    {
                        if (this.objectBacking.TryGetValue(JsonPropertyNames.ExternalValue, out JsonAny result))
                        {
                            return result.As<Corvus.Json.JsonAny>();
                        }
                    }

                    return default;
                }
            }

            /// <summary>
            /// Gets the <c>value</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
            /// </summary>
            public Corvus.Json.JsonAny Value
            {
                get
                {
                    if ((this.backing & Backing.JsonElement) != 0)
                    {
                        if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                        {
                            return default;
                        }

                        if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ValueUtf8, out JsonElement result))
                        {
                            return new Corvus.Json.JsonAny(result);
                        }
                    }

                    if ((this.backing & Backing.Object) != 0)
                    {
                        if (this.objectBacking.TryGetValue(JsonPropertyNames.Value, out JsonAny result))
                        {
                            return result.As<Corvus.Json.JsonAny>();
                        }
                    }

                    return default;
                }
            }

            /// <summary>
            /// Creates an instance of a <see cref = "RequiredValueAndExternalValue"/>.
            /// </summary>
            public static RequiredValueAndExternalValue Create(Corvus.Json.JsonAny externalValue, Corvus.Json.JsonAny value)
            {
                var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                builder.Add(JsonPropertyNames.ExternalValue, externalValue.AsAny);
                builder.Add(JsonPropertyNames.Value, value.AsAny);
                return new(builder.ToImmutable());
            }

            /// <summary>
            /// Sets externalValue.
            /// </summary>
            /// <param name = "value">The value to set.</param>
            /// <returns>The entity with the updated property.</returns>
            public RequiredValueAndExternalValue WithExternalValue(in Corvus.Json.JsonAny value)
            {
                return this.SetProperty(JsonPropertyNames.ExternalValue, value);
            }

            /// <summary>
            /// Sets value.
            /// </summary>
            /// <param name = "value">The value to set.</param>
            /// <returns>The entity with the updated property.</returns>
            public RequiredValueAndExternalValue WithValue(in Corvus.Json.JsonAny value)
            {
                return this.SetProperty(JsonPropertyNames.Value, value);
            }

            private static ValidationContext __CorvusValidateValue(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
            {
                return property.ValueAs<Corvus.Json.JsonAny>().Validate(validationContext, level);
            }

            private static ValidationContext __CorvusValidateExternalValue(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
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
                    if (property.NameEquals(JsonPropertyNames.ValueUtf8))
                    {
                        propertyValidator = __CorvusValidateValue;
                        return true;
                    }
                    else if (property.NameEquals(JsonPropertyNames.ExternalValueUtf8))
                    {
                        propertyValidator = __CorvusValidateExternalValue;
                        return true;
                    }
                }
                else
                {
                    if (property.NameEquals(JsonPropertyNames.Value))
                    {
                        propertyValidator = __CorvusValidateValue;
                        return true;
                    }
                    else if (property.NameEquals(JsonPropertyNames.ExternalValue))
                    {
                        propertyValidator = __CorvusValidateExternalValue;
                        return true;
                    }
                }

                propertyValidator = null;
                return false;
            }
        }
    }
}
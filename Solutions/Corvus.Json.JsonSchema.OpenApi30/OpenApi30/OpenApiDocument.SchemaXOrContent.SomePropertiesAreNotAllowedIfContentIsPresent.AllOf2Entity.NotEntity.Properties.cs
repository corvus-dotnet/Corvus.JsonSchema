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
        public readonly partial struct SomePropertiesAreNotAllowedIfContentIsPresent
        {
            public readonly partial struct AllOf2Entity
            {
                /// <summary>
                /// Generated from JSON Schema.
                /// </summary>
                public readonly partial struct NotEntity
                {
                    /// <summary>
                    /// The well-known property names in the JSON object.
                    /// </summary>
                    public static class JsonPropertyNames
                    {
                        /// <summary>
                        /// JSON property name for <see cref = "AllowReserved"/>.
                        /// </summary>
                        public static ReadOnlySpan<byte> AllowReservedUtf8 => "allowReserved"u8;

                        /// <summary>
                        /// JSON property name for <see cref = "AllowReserved"/>.
                        /// </summary>
                        public const string AllowReserved = "allowReserved";
                    }

                    /// <summary>
                    /// Gets the <c>allowReserved</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
                    /// </summary>
                    public Corvus.Json.JsonAny AllowReserved
                    {
                        get
                        {
                            if ((this.backing & Backing.JsonElement) != 0)
                            {
                                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                                {
                                    return default;
                                }

                                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.AllowReservedUtf8, out JsonElement result))
                                {
                                    return new Corvus.Json.JsonAny(result);
                                }
                            }

                            if ((this.backing & Backing.Object) != 0)
                            {
                                if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowReserved, out JsonAny result))
                                {
                                    return result.As<Corvus.Json.JsonAny>();
                                }
                            }

                            return default;
                        }
                    }

                    /// <summary>
                    /// Creates an instance of a <see cref = "NotEntity"/>.
                    /// </summary>
                    public static NotEntity Create(Corvus.Json.JsonAny allowReserved)
                    {
                        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                        builder.Add(JsonPropertyNames.AllowReserved, allowReserved.AsAny);
                        return new(builder.ToImmutable());
                    }

                    /// <summary>
                    /// Sets allowReserved.
                    /// </summary>
                    /// <param name = "value">The value to set.</param>
                    /// <returns>The entity with the updated property.</returns>
                    public NotEntity WithAllowReserved(in Corvus.Json.JsonAny value)
                    {
                        return this.SetProperty(JsonPropertyNames.AllowReserved, value);
                    }

                    private static ValidationContext __CorvusValidateAllowReserved(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
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
                            if (property.NameEquals(JsonPropertyNames.AllowReservedUtf8))
                            {
                                propertyValidator = __CorvusValidateAllowReserved;
                                return true;
                            }
                        }
                        else
                        {
                            if (property.NameEquals(JsonPropertyNames.AllowReserved))
                            {
                                propertyValidator = __CorvusValidateAllowReserved;
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
}
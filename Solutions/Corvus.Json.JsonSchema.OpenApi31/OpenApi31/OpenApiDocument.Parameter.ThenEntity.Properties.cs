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
    public readonly partial struct Parameter
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct ThenEntity
        {
            /// <summary>
            /// The well-known property names in the JSON object.
            /// </summary>
            public static class JsonPropertyNames
            {
                /// <summary>
                /// JSON property name for <see cref = "AllowEmptyValue"/>.
                /// </summary>
                public static ReadOnlySpan<byte> AllowEmptyValueUtf8 => "allowEmptyValue"u8;

                /// <summary>
                /// JSON property name for <see cref = "AllowEmptyValue"/>.
                /// </summary>
                public const string AllowEmptyValue = "allowEmptyValue";
            }

            /// <summary>
            /// Gets the (optional) <c>allowEmptyValue</c> property.
            /// </summary>
            public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity AllowEmptyValue
            {
                get
                {
                    if ((this.backing & Backing.JsonElement) != 0)
                    {
                        if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                        {
                            return default;
                        }

                        if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.AllowEmptyValueUtf8, out JsonElement result))
                        {
                            return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity(result);
                        }
                    }

                    if ((this.backing & Backing.Object) != 0)
                    {
                        if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowEmptyValue, out JsonAny result))
                        {
                            return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity>();
                        }
                    }

                    return default;
                }
            }

            /// <summary>
            /// Creates an instance of a <see cref = "ThenEntity"/>.
            /// </summary>
            public static ThenEntity Create(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity? allowEmptyValue = null)
            {
                var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
                if (allowEmptyValue is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity allowEmptyValue__)
                {
                    builder.Add(JsonPropertyNames.AllowEmptyValue, allowEmptyValue__.AsAny);
                }

                return new(builder.ToImmutable());
            }

            /// <summary>
            /// Sets allowEmptyValue.
            /// </summary>
            /// <param name = "value">The value to set.</param>
            /// <returns>The entity with the updated property.</returns>
            public ThenEntity WithAllowEmptyValue(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity value)
            {
                return this.SetProperty(JsonPropertyNames.AllowEmptyValue, value);
            }

            private static ValidationContext __CorvusValidateAllowEmptyValue(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
            {
                return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity>().Validate(validationContext, level);
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
                    if (property.NameEquals(JsonPropertyNames.AllowEmptyValueUtf8))
                    {
                        propertyValidator = __CorvusValidateAllowEmptyValue;
                        return true;
                    }
                }
                else
                {
                    if (property.NameEquals(JsonPropertyNames.AllowEmptyValue))
                    {
                        propertyValidator = __CorvusValidateAllowEmptyValue;
                        return true;
                    }
                }

                propertyValidator = null;
                return false;
            }
        }
    }
}
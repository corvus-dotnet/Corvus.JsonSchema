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

namespace JsonSchemaSample.Api;
public readonly partial struct Pet
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct RequiredId
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "Id"/>.
            /// </summary>
            public static ReadOnlySpan<byte> IdUtf8 => "id"u8;

            /// <summary>
            /// JSON property name for <see cref = "Id"/>.
            /// </summary>
            public const string Id = "id";
        }

        /// <summary>
        /// Gets the <c>id</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
        /// </summary>
        public Corvus.Json.JsonInt64 Id
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.IdUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonInt64(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Id, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonInt64>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "RequiredId"/>.
        /// </summary>
        public static RequiredId Create(Corvus.Json.JsonInt64 id)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            builder.Add(JsonPropertyNames.Id, id.AsAny);
            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets id.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public RequiredId WithId(in Corvus.Json.JsonInt64 value)
        {
            return this.SetProperty(JsonPropertyNames.Id, value);
        }

        private static ValidationContext __CorvusValidateId(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonInt64>().Validate(validationContext, level);
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
                if (property.NameEquals(JsonPropertyNames.IdUtf8))
                {
                    propertyValidator = __CorvusValidateId;
                    return true;
                }
            }
            else
            {
                if (property.NameEquals(JsonPropertyNames.Id))
                {
                    propertyValidator = __CorvusValidateId;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }
    }
}
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
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct Header
    {
        private static ReadOnlySpan<byte> __DependentSchema1Utf8JsonPropertyName => "schema"u8;

        private const string __DependentSchema1JsonPropertyName = "schema";
        /// <summary>
        /// Try to match the instance with the dependent schema for property schema, and get it as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple"/> if the property is present.
        /// </summary>
        /// <param name = "result">The value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple"/>.</param>.
        /// <returns><c>True</c> if the property was present.</returns>
        public bool TryAsDependentSchemaForSchema(out Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple result)
        {
            if ((this.HasJsonElementBacking && this.HasProperty(__DependentSchema1Utf8JsonPropertyName) || (!this.HasJsonElementBacking && this.HasProperty(__DependentSchema1JsonPropertyName))))
            {
                result = this.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple>();
                return true;
            }

            result = Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.Undefined;
            return false;
        }

        /// <summary>
        /// Tries to get the validator for the given property.
        /// </summary>
        /// <param name = "property">The property for which to get the validator.</param>
        /// <param name = "hasJsonElementBacking"><c>True</c> if the object containing the property has a JsonElement backing.</param>
        /// <param name = "propertyValidator">The validator for the property, if provided by this schema.</param>
        /// <returns><c>True</c> if the validator was found.</returns>
        public bool __TryGetCorvusDependentSchemaValidator(in JsonObjectProperty property, bool hasJsonElementBacking, [NotNullWhen(true)] out PropertyValidator<Header>? propertyValidator)
        {
            if (hasJsonElementBacking)
            {
                if (property.NameEquals(__DependentSchema1Utf8JsonPropertyName))
                {
                    propertyValidator = __CorvusValidateDependentSchema1;
                    return true;
                }
            }
            else
            {
                if (property.NameEquals(__DependentSchema1JsonPropertyName))
                {
                    propertyValidator = __CorvusValidateDependentSchema1;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }

        private static ValidationContext __CorvusValidateDependentSchema1(in Header that, in ValidationContext validationContext, ValidationLevel level)
        {
            return that.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple>().Validate(validationContext, level);
        }
    }
}
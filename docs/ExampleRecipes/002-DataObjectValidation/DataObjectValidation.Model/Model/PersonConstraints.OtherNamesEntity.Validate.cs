//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Text.Json;
using Corvus.Json;

namespace JsonSchemaSample.Api;
public readonly partial struct PersonConstraints
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct OtherNamesEntity
    {
        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            if (level > ValidationLevel.Flag)
            {
                result = result.UsingResults();
            }

            if (level > ValidationLevel.Basic)
            {
                result = result.UsingStack();
                result = result.PushSchemaLocation("person-constraints.json#/properties/otherNames");
            }

            JsonValueKind valueKind = this.ValueKind;
            result = this.ValidateType(valueKind, result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            result = Corvus.Json.Validate.ValidateString(this, result, level, 256, 1, null);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            if (level != ValidationLevel.Flag)
            {
                result = result.PopLocation();
            }

            return result;
        }
    }
}
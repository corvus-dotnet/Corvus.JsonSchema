//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;

namespace Corvus.Json.JsonSchema.OpenApi31;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// The description of OpenAPI v3.1.x documents without schema validation, as defined by https://spec.openapis.org/oas/v3.1.0
/// </para>
/// </remarks>
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct ContentEntity
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
                result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.1/schema/2022-10-07?dynamicScope=https%3A%2F%2Fraw.githubusercontent.com%2FOAI%2FOpenAPI-Specification%2Fmain%2Fschemas%2Fv3.1%2Fschema.json#/$defs/parameter/properties/content");
            }

            JsonValueKind valueKind = this.ValueKind;
            result = CorvusValidation.CompositionAllOfValidationHandler(this, result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            result = CorvusValidation.ObjectValidationHandler(this, valueKind, result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            if (level > ValidationLevel.Basic)
            {
                result = result.PopLocation();
            }

            return result;
        }

        /// <summary>
        /// Validation constants for the type.
        /// </summary>
        public static partial class CorvusValidation
        {
            /// <summary>
            /// A constant for the <c>maxProperties</c> keyword.
            /// </summary>
            public static readonly long MaxProperties = 1;

            /// <summary>
            /// A constant for the <c>minProperties</c> keyword.
            /// </summary>
            public static readonly long MinProperties = 1;

            /// <summary>
            /// Composition validation (all-of).
            /// </summary>
            /// <param name="value">The value to validate.</param>
            /// <param name="validationContext">The current validation context.</param>
            /// <param name="level">The current validation level.</param>
            /// <returns>The resulting validation context after validation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ValidationContext CompositionAllOfValidationHandler(
                in ContentEntity value,
                in ValidationContext validationContext,
                ValidationLevel level = ValidationLevel.Flag)
            {
                ValidationContext result = validationContext;
                ValidationContext childContextBase = result;
                ValidationContext refResult = childContextBase.CreateChildContext();
                if (level > ValidationLevel.Basic)
                {
                    refResult = refResult.PushValidationLocationReducedPathModifier(new("#/$ref"));
                }

                refResult = value.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content>().Validate(refResult, level);
                if (!refResult.IsValid)
                {
                    if (level >= ValidationLevel.Basic)
                    {
                        result = result.MergeChildContext(refResult, true).PushValidationLocationProperty("$ref").WithResult(isValid: false, "Validation - $ref failed to validate against the schema.").PopLocation();
                    }
                    else
                    {
                        result = result.MergeChildContext(refResult, false).WithResult(isValid: false);
                        return result;
                    }
                }
                else
                {
                    result = result.MergeChildContext(refResult, level >= ValidationLevel.Detailed);
                }

                return result;
            }

            /// <summary>
            /// Object validation.
            /// </summary>
            /// <param name="value">The value to validate.</param>
            /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
            /// <param name="validationContext">The current validation context.</param>
            /// <param name="level">The current validation level.</param>
            /// <returns>The resulting validation context after validation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ValidationContext ObjectValidationHandler(
                in ContentEntity value,
                JsonValueKind valueKind,
                in ValidationContext validationContext,
                ValidationLevel level = ValidationLevel.Flag)
            {
                ValidationContext result = validationContext;
                if (valueKind != JsonValueKind.Object)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        ValidationContext ignoredResult = validationContext;
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation maxProperties - ignored because the value is not an object", "maxProperties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation minProperties - ignored because the value is not an object", "minProperties");
                        return ignoredResult;
                    }

                    return validationContext;
                }

                int propertyCount = value.Count;
                if (propertyCount <= MaxProperties)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation maxProperties - property count {propertyCount} is less than or equal to {MaxProperties}", "maxProperties");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation maxProperties - array of length {propertyCount} is greater than {MaxProperties}", "maxProperties");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation maxProperties - is greater than the required count.", "maxProperties");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }

                if (propertyCount >= MinProperties)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation minProperties - property count {propertyCount} is greater than or equal to {MinProperties}", "minProperties");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation minProperties - array of length {propertyCount} is less than {MinProperties}", "minProperties");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation minProperties - is less than the required count.", "minProperties");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }

                return result;
            }
        }
    }
}
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

namespace Feature408;
/// <summary>
/// Combined document schema
/// </summary>
public readonly partial struct CombinedDocument
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
            result = result.PushSchemaLocation("CombinedDocumentSchema.json");
        }

        JsonValueKind valueKind = this.ValueKind;
        result = CorvusValidation.TypeValidationHandler(valueKind, result, level);
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        result = CorvusValidation.CompositionAllOfValidationHandler(this, result, level);
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
        /// Core type validation.
        /// </summary>
        /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
        /// <param name="validationContext">The current validation context.</param>
        /// <param name="level">The current validation level.</param>
        /// <returns>The resulting validation context after validation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ValidationContext TypeValidationHandler(
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            return Corvus.Json.ValidateWithoutCoreType.TypeObject(valueKind, validationContext, level, "type");
        }

        /// <summary>
        /// Composition validation (all-of).
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The current validation context.</param>
        /// <param name="level">The current validation level.</param>
        /// <returns>The resulting validation context after validation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ValidationContext CompositionAllOfValidationHandler(
            in CombinedDocument value,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            ValidationContext childContextBase = result;
            ValidationContext allOfResult0 = childContextBase.CreateChildContext();
            if (level > ValidationLevel.Basic)
            {
                allOfResult0 = allOfResult0.PushValidationLocationReducedPathModifier(new("#/allOf/0/$ref"));
            }

            allOfResult0 = value.As<Feature408.DocumentSchema>().Validate(allOfResult0, level);
            if (!allOfResult0.IsValid)
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.MergeChildContext(allOfResult0, true).PushValidationLocationProperty("allOf").WithResult(isValid: false, "Validation - allOf failed to validate against the schema.").PopLocation();
                }
                else
                {
                    result = result.MergeChildContext(allOfResult0, false).WithResult(isValid: false);
                    return result;
                }
            }
            else
            {
                result = result.MergeChildContext(allOfResult0, level >= ValidationLevel.Detailed);
            }

            ValidationContext allOfResult1 = childContextBase.CreateChildContext();
            if (level > ValidationLevel.Basic)
            {
                allOfResult1 = allOfResult1.PushValidationLocationReducedPathModifier(new("#/allOf/1/$ref"));
            }

            allOfResult1 = value.As<Feature408.DocumentExtensionsSchema>().Validate(allOfResult1, level);
            if (!allOfResult1.IsValid)
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.MergeChildContext(allOfResult1, true).PushValidationLocationProperty("allOf").WithResult(isValid: false, "Validation - allOf failed to validate against the schema.").PopLocation();
                }
                else
                {
                    result = result.MergeChildContext(allOfResult1, false).WithResult(isValid: false);
                    return result;
                }
            }
            else
            {
                result = result.MergeChildContext(allOfResult1, level >= ValidationLevel.Detailed);
            }

            return result;
        }
    }
}
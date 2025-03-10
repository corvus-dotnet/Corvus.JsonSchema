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

namespace Sandbox.Models;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// A name of a person.
/// </para>
/// </remarks>
public readonly partial struct PersonName
{
    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
    {
        ValidationContext result = validationContext;
        if (level > ValidationLevel.Flag && !result.IsUsingResults)
        {
            result = result.UsingResults();
        }

        if (level > ValidationLevel.Basic)
        {
            if (!result.IsUsingStack)
            {
                result = result.UsingStack();
            }

            result = result.PushSchemaLocation("#/$defs/PersonName");
        }

        JsonValueKind valueKind = this.ValueKind;

        result = CorvusValidation.TypeValidationHandler(valueKind, result, level);

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
        /// Object validation.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
        /// <param name="validationContext">The current validation context.</param>
        /// <param name="level">The current validation level.</param>
        /// <returns>The resulting validation context after validation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ValidationContext ObjectValidationHandler(
            in PersonName value,
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
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation properties - ignored because the value is not an object", "properties");
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation required - ignored because the value is not an object", "required");

                    return ignoredResult;
                }

                return validationContext;
            }

            bool hasSeenFamilyName = false;

            int propertyCount = 0;
            foreach (JsonObjectProperty property in value.EnumerateObject())
            {
                if (property.NameEquals(JsonPropertyNames.FamilyNameUtf8, JsonPropertyNames.FamilyName))
                {
                    hasSeenFamilyName = true;
                    result = result.WithLocalProperty(propertyCount);
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/familyName/$ref"), JsonPropertyNames.FamilyName);
                    }

                    ValidationContext propertyResult = property.Value.As<Sandbox.Models.PersonNameElement>().Validate(result.CreateChildContext(), level);
                    if (level == ValidationLevel.Flag && !propertyResult.IsValid)
                    {
                        return propertyResult;
                    }

                    result = result.MergeResults(propertyResult.IsValid, level, propertyResult);

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }
                }
                else if (property.NameEquals(JsonPropertyNames.GivenNameUtf8, JsonPropertyNames.GivenName))
                {
                    result = result.WithLocalProperty(propertyCount);
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/givenName/$ref"), JsonPropertyNames.GivenName);
                    }

                    ValidationContext propertyResult = property.Value.As<Sandbox.Models.PersonNameElement>().Validate(result.CreateChildContext(), level);
                    if (level == ValidationLevel.Flag && !propertyResult.IsValid)
                    {
                        return propertyResult;
                    }

                    result = result.MergeResults(propertyResult.IsValid, level, propertyResult);

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }
                }
                else if (property.NameEquals(JsonPropertyNames.OtherNamesUtf8, JsonPropertyNames.OtherNames))
                {
                    result = result.WithLocalProperty(propertyCount);
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/otherNames/$ref"), JsonPropertyNames.OtherNames);
                    }

                    ValidationContext propertyResult = property.Value.As<Sandbox.Models.OtherNames>().Validate(result.CreateChildContext(), level);
                    if (level == ValidationLevel.Flag && !propertyResult.IsValid)
                    {
                        return propertyResult;
                    }

                    result = result.MergeResults(propertyResult.IsValid, level, propertyResult);

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }
                }

                propertyCount++;
            }

            if (level > ValidationLevel.Basic)
            {
                result = result.PushValidationLocationReducedPathModifier(new("#/required/0"));
            }

            if (!hasSeenFamilyName)
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.WithResult(isValid: false, "Validation properties - the required property 'familyName' was not present.");
                }
                else
                {
                    return ValidationContext.InvalidContext;
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                result = result.WithResult(isValid: true, "Validation properties - the required property 'familyName' was present.");
            }

            if (level > ValidationLevel.Basic)
            {
                result = result.PopLocation();
            }

            return result;
        }
    }
}

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

namespace Corvus.Json.Benchmarking.Models.V3;

/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct HeightRangeDouble
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
            result = result.PushSchemaLocation("#/$defs/HeightRangeDouble");
        }

        JsonValueKind valueKind = this.ValueKind;
        result = CorvusValidation.TypeValidationHandler(this, valueKind, result, level);
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        result = CorvusValidation.NumberValidationHandler(this, valueKind, result, level);
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

    private static partial class CorvusValidation
    {
        public static readonly BinaryJsonNumber Maximum = new(3.0);

        public static readonly BinaryJsonNumber Minimum = new(0);

        public static ValidationContext TypeValidationHandler(
            in HeightRangeDouble value,
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            bool isValid = false;
            ValidationContext localResultNumber = Corvus.Json.Validate.TypeNumber(valueKind, result.CreateChildContext(), level);
            if (level == ValidationLevel.Flag && localResultNumber.IsValid)
            {
                return validationContext;
            }

            if (localResultNumber.IsValid)
            {
                isValid = true;
            }

            return result.MergeResults(
                isValid,
                level,
                localResultNumber);
        }

        public static ValidationContext NumberValidationHandler(
            in HeightRangeDouble value,
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            if (valueKind != JsonValueKind.Number)
            {
                if (level == ValidationLevel.Verbose)
                {
                    ValidationContext ignoredResult = validationContext;
                    ignoredResult = ignoredResult.PushValidationLocationProperty("maximum");
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation maximum - ignored because the value is not a number");
                    ignoredResult = ignoredResult.PopLocation();
                    ignoredResult = ignoredResult.PushValidationLocationProperty("minimum");
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation minimum - ignored because the value is not a number");
                    ignoredResult = ignoredResult.PopLocation();
                    return ignoredResult;
                }

                return validationContext;
            }

            ValidationContext result = validationContext;
            if ((value.HasJsonElementBacking
                ? BinaryJsonNumber.Compare(value.AsJsonElement, Maximum)
                : BinaryJsonNumber.Compare(value.AsBinaryJsonNumber, Maximum))<= 0)
            {
                if (level == ValidationLevel.Verbose)
                {
                    result = result.PushValidationLocationProperty("maximum");
                    result = result.WithResult(isValid: true, $"Validation maximum - {value} is less than or equal to {Maximum}");
                    result = result.PopLocation();
                }
            }
            else
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationProperty("maximum");
                }

                if (level >= ValidationLevel.Detailed)
                {
                    result = result.WithResult(isValid: false, $"Validation maximum - {value} is greater than {Maximum}");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    result = result.WithResult(isValid: false, "Validation maximum - is greater than the required value.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }

                if (level >= ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }
            }

            if ((value.HasJsonElementBacking
                ? BinaryJsonNumber.Compare(value.AsJsonElement, Minimum)
                : BinaryJsonNumber.Compare(value.AsBinaryJsonNumber, Minimum))>= 0)
            {
                if (level == ValidationLevel.Verbose)
                {
                    result = result.PushValidationLocationProperty("minimum");
                    result = result.WithResult(isValid: true, $"Validation minimum - {value} is greater than or equal to {Minimum}");
                    result = result.PopLocation();
                }
            }
            else
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationProperty("minimum");
                }

                if (level >= ValidationLevel.Detailed)
                {
                    result = result.WithResult(isValid: false, $"Validation minimum - {value} is less than {Minimum}");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    result = result.WithResult(isValid: false, "Validation minimum - is less than the required value.");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }

                if (level >= ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }
            }
            return result;
        }
    }
}
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

namespace Corvus.Json.Benchmarking.Models.V3;

/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct PersonNameElement
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
            result = result.PushSchemaLocation("#/$defs/PersonNameElement");
        }

        JsonValueKind valueKind = this.ValueKind;
        result = CorvusValidation.TypeValidationHandler(this, valueKind, result, level);
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        result = CorvusValidation.StringValidationHandler(this, valueKind, result, level);
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
        public static readonly long MaxLength = 256;

        public static readonly long MinLength = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationContext TypeValidationHandler(
            in PersonNameElement value,
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            bool isValid = false;
            ValidationContext localResultString = Corvus.Json.Validate.TypeString(valueKind, result.CreateChildContext(), level);
            if (level == ValidationLevel.Flag && localResultString.IsValid)
            {
                return validationContext;
            }

            if (localResultString.IsValid)
            {
                isValid = true;
            }

            return result.MergeResults(
                isValid,
                level,
                localResultString);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationContext StringValidationHandler(
            in PersonNameElement value,
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level = ValidationLevel.Flag)
        {
            if (valueKind != JsonValueKind.String)
            {
                if (level == ValidationLevel.Verbose)
                {
                    ValidationContext ignoredResult = validationContext;
                    ignoredResult = ignoredResult.PushValidationLocationProperty("maxLength");
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation maxLength - ignored because the value is not a string");
                    ignoredResult = ignoredResult.PopLocation();
                    ignoredResult = ignoredResult.PushValidationLocationProperty("minLength");
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation minLength - ignored because the value is not a string");
                    ignoredResult = ignoredResult.PopLocation();
                    return ignoredResult;
                }

                return validationContext;
            }

            ValidationContext result = validationContext;
            value.AsString.TryGetValue(StringValidator, new Corvus.Json.Validate.ValidationContextWrapper(result, level), out result);
            return result;

            static bool StringValidator(ReadOnlySpan<char> input, in Corvus.Json.Validate.ValidationContextWrapper context, out ValidationContext result)
            {
                int length = Corvus.Json.Validate.CountRunes(input);
                result = context.Context;
                if (context.Level > ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationReducedPathModifier(new("#/maxLength"));
                }

                if (length <= MaxLength)
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation maxLength - {input.ToString()} of {length} is less than or equal to {MaxLength}");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation maxLength - {input.ToString()} of {length} is greater than {MaxLength}");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation maxLength - is greater than the required length.");
                    }
                    else
                    {
                        result = context.Context.WithResult(isValid: false);
                        return true;
                    }
                }

                if (context.Level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                if (context.Level > ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationReducedPathModifier(new("#/minLength"));
                }

                if (length >= MinLength)
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation minLength - {input.ToString()} of {length} is greater than or equal to {MinLength}");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation minLength - {input.ToString()} of {length} is less than {MinLength}");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation minLength - is less than the required length.");
                    }
                    else
                    {
                        result = context.Context.WithResult(isValid: false);
                        return true;
                    }
                }

                if (context.Level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                return true;
            }
        }
    }
}
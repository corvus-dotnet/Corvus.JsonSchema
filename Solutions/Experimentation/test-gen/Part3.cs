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
using System.Text.RegularExpressions;
using Corvus.Json;

namespace PatternDraft202012Feature.PatternValidation;

/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct Schema
{
    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;
        if (level > ValidationLevel.Flag)
        {
            result = result.UsingResults();
        }

        if (level > ValidationLevel.Basic)
        {
            result = result.UsingStack();
            result = result.PushSchemaLocation("https://endjin.com/5704c009-cdae-4e93-b8a5-1c0694d06734/Schema");
        }

        JsonValueKind valueKind = this.ValueKind;
        result = CorvusValidation.StringValidationHandler(this, valueKind, result, level);
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

    private static partial class CorvusValidation
    {
        public static readonly Regex Pattern = CreatePattern();

        public static ValidationContext StringValidationHandler(
            in Schema value,
            JsonValueKind valueKind,
            in ValidationContext validationContext,
            ValidationLevel level)
        {
            ValidationContext result = validationContext;
            if (valueKind != JsonValueKind.String)
            {
                if (level == ValidationLevel.Verbose)
                {
                    ValidationContext ignoredResult = validationContext;
                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation pattern - ignored because the value is not a string");
                    return ignoredResult;
                }

                return validationContext;
            }

            value.AsString.TryGetValue(StringValidator, new Corvus.Json.Validate.ValidationContextWrapper(result, level), out result);
            return result;

            static bool StringValidator(ReadOnlySpan<char> input, in Corvus.Json.Validate.ValidationContextWrapper context, out ValidationContext result)
            {
                result = context.Context;
                if (Pattern.IsMatch(input))
                {
                    if (context.Level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation pattern - {input.ToString()} matched  '^a*$'");
                    }
                }
                else
                {
                    if (context.Level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation pattern - {input.ToString()} did not match  '^a*$'");
                    }
                    else if (context.Level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation pattern - The value did not match  '^a*$'");
                    }
                    else
                    {
                        result = context.Context.WithResult(isValid: false);
                        return true;
                    }
                }

                return true;
            }
        }

#if NET8_0_OR_GREATER
        [GeneratedRegex("^a*$")]
        private static partial Regex CreatePattern();
#else
        private static Regex CreatePattern() => new("^a*$", RegexOptions.Compiled);
#endif
    }
}
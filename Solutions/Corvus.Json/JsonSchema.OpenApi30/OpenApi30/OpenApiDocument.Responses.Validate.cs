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
using System.Text.RegularExpressions;
using Corvus.Json;

namespace Corvus.Json.JsonSchema.OpenApi30;

/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// The description of OpenAPI v3.0.x documents, as defined by https://spec.openapis.org/oas/v3.0.3
/// </para>
/// </remarks>
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct Responses
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
                result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.0/schema/2021-09-28#/definitions/Responses");
            }

            JsonValueKind valueKind = this.ValueKind;
            result = result.UsingEvaluatedProperties();
            result = CorvusValidation.TypeValidationHandler(this, valueKind, result, level);
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

        private static partial class CorvusValidation
        {
            public static readonly long MinProperties = 1;

            public static readonly Regex PatternProperties1 = CreatePatternProperties1();
            public static readonly Regex PatternProperties2 = CreatePatternProperties2();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ValidationContext TypeValidationHandler(
                in Responses value,
                JsonValueKind valueKind,
                in ValidationContext validationContext,
                ValidationLevel level = ValidationLevel.Flag)
            {
                ValidationContext result = validationContext;
                bool isValid = false;
                ValidationContext localResultObject = Corvus.Json.Validate.TypeObject(valueKind, result.CreateChildContext(), level);
                if (level == ValidationLevel.Flag && localResultObject.IsValid)
                {
                    return validationContext;
                }

                if (localResultObject.IsValid)
                {
                    isValid = true;
                }

                return result.MergeResults(
                    isValid,
                    level,
                    localResultObject);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ValidationContext ObjectValidationHandler(
                in Responses value,
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
                        ignoredResult = ignoredResult.PushValidationLocationProperty("minProperties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation minProperties - ignored because the value is not an object");
                        ignoredResult = ignoredResult.PopLocation();
                        ignoredResult = ignoredResult.PushValidationLocationProperty("additionalProperties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation additionalProperties - ignored because the value is not an object");
                        ignoredResult = ignoredResult.PopLocation();
                        ignoredResult = ignoredResult.PushValidationLocationProperty("properties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation properties - ignored because the value is not an object");
                        ignoredResult = ignoredResult.PopLocation();
                        ignoredResult = ignoredResult.PushValidationLocationProperty("patternProperties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation patternProperties - ignored because the value is not an object");
                        ignoredResult = ignoredResult.PopLocation();
                        return ignoredResult;
                    }

                    return validationContext;
                }

                int propertyCount = 0;
                foreach (JsonObjectProperty property in value.EnumerateObject())
                {
                    string? propertyNameAsString = null;
                    propertyNameAsString ??= property.Name.GetString();
                    if (PatternProperties1.IsMatch(propertyNameAsString))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference("#/patternProperties"), propertyNameAsString);
                        }

                        result = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Responses.Type15D2XxEntity>().Validate(result, level);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PopLocation();
                        }

                        if (level == ValidationLevel.Flag && !result.IsValid)
                        {
                            return result;
                        }
                    }

                    if (PatternProperties2.IsMatch(propertyNameAsString))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference("#/patternProperties"), propertyNameAsString);
                        }

                        result = property.Value.As<Corvus.Json.JsonAny>().Validate(result, level);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PopLocation();
                        }

                        if (level == ValidationLevel.Flag && !result.IsValid)
                        {
                            return result;
                        }
                    }

                    if (property.NameEquals(JsonPropertyNames.DefaultUtf8, JsonPropertyNames.Default))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/default"), JsonPropertyNames.Default);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Responses.DefaultEntity>().Validate(result.CreateChildContext(), level);
                        result = result.MergeResults(propertyResult.IsValid, level, propertyResult);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PopLocation();
                        }

                        if (level == ValidationLevel.Flag && !result.IsValid)
                        {
                            return result;
                        }
                    }
                    if (!result.HasEvaluatedLocalProperty(propertyCount))
                    {
                        if (level > ValidationLevel.Basic)
                        {
                            string localEvaluatedPropertyName = (propertyNameAsString ??= property.Name.GetString());
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference("#/additionalProperties").AppendUnencodedPropertyNameToFragment(localEvaluatedPropertyName), localEvaluatedPropertyName);
                        }

                        result = property.Value.As<Corvus.Json.JsonNotAny>().Validate(result, level);
                        if (level == ValidationLevel.Flag && !result.IsValid)
                        {
                            return result;
                        }

                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PopLocation();
                        }

                        result = result.WithLocalProperty(propertyCount);
                    }

                    propertyCount++;
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationProperty("minProperties");
                }

                if (propertyCount >= MinProperties)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation minProperties - property count {propertyCount} is greater than or equal to {MinProperties}");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation minProperties - array of length {propertyCount} is less than {MinProperties}");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation minProperties - is less than the required count.");
                    }
                    else
                    {
                        return result.WithResult(isValid: false);
                    }
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                return result;
            }

#if NET8_0_OR_GREATER && !SPECFLOW_BUILD
            [GeneratedRegex("^[1-5](?:\\d{2}|XX)$")]
            private static partial Regex CreatePatternProperties1();
#else
            private static Regex CreatePatternProperties1() => new("^[1-5](?:\\d{2}|XX)$", RegexOptions.Compiled);
#endif
#if NET8_0_OR_GREATER && !SPECFLOW_BUILD
            [GeneratedRegex("^x-")]
            private static partial Regex CreatePatternProperties2();
#else
            private static Regex CreatePatternProperties2() => new("^x-", RegexOptions.Compiled);
#endif
        }
    }
}
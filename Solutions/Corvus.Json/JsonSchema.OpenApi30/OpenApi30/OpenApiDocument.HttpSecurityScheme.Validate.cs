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
    public readonly partial struct HttpSecurityScheme
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
                result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.0/schema/2021-09-28#/definitions/HTTPSecurityScheme");
            }

            JsonValueKind valueKind = this.ValueKind;
            result = result.UsingEvaluatedProperties();
            result = CorvusValidation.TypeValidationHandler(this, valueKind, result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            result = CorvusValidation.CompositionOneOfValidationHandler(this, result, level);
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
            public static readonly Regex PatternProperties = CreatePatternProperties();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ValidationContext TypeValidationHandler(
                in HttpSecurityScheme value,
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
            public static ValidationContext CompositionOneOfValidationHandler(
                in HttpSecurityScheme value,
                in ValidationContext validationContext,
                ValidationLevel level = ValidationLevel.Flag)
            {
                ValidationContext result = validationContext;
                result = ValidateOneOf(value, result, level);
                if (!result.IsValid && level == ValidationLevel.Flag)
                {
                    return result;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static ValidationContext ValidateOneOf(in HttpSecurityScheme value, in ValidationContext validationContext, ValidationLevel level)
                {
                    ValidationContext result = validationContext;
                    int oneOfFoundValid = 0;
                    ValidationContext oneOfChildContext0 = validationContext.CreateChildContext();
                    if (level > ValidationLevel.Basic)
                    {
                        oneOfChildContext0 = oneOfChildContext0.PushValidationLocationReducedPathModifier(new("#/oneOf/0"));
                    }

                    ValidationContext oneOfResult0 = value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.HttpSecurityScheme.Bearer>().Validate(oneOfChildContext0, level);
                    if (oneOfResult0.IsValid)
                    {
                        result = result.MergeChildContext(oneOfResult0, level >= ValidationLevel.Verbose);
                        oneOfFoundValid++;
                    }
                    else
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.MergeResults(result.IsValid, level, oneOfResult0);
                        }
                    }

                    ValidationContext oneOfChildContext1 = validationContext.CreateChildContext();
                    if (level > ValidationLevel.Basic)
                    {
                        oneOfChildContext1 = oneOfChildContext1.PushValidationLocationReducedPathModifier(new("#/oneOf/1"));
                    }

                    ValidationContext oneOfResult1 = value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.HttpSecurityScheme.NonBearer>().Validate(oneOfChildContext1, level);
                    if (oneOfResult1.IsValid)
                    {
                        result = result.MergeChildContext(oneOfResult1, level >= ValidationLevel.Verbose);
                        oneOfFoundValid++;
                    }
                    else
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.MergeResults(result.IsValid, level, oneOfResult1);
                        }
                    }

                    if (level >= ValidationLevel.Basic)
                    {
                        result.PushValidationLocationProperty("oneOf");
                    }

                    if (oneOfFoundValid == 1)
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.WithResult(isValid: true, "Validation oneOf - validated against the schema.");
                        }
                    }
                    else if (oneOfFoundValid > 1)
                    {
                        if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation oneOf - validated against more than 1 of the schema.");
                        }
                        else
                        {
                            result = result.WithResult(isValid: false);
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation oneOf - did not validate against any of the schema.");
                        }
                        else
                        {
                            result = result.WithResult(isValid: false);
                        }
                    }

                    if (level >= ValidationLevel.Basic)
                    {
                        result.PopLocation();
                    }

                    return result;
                }

                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ValidationContext ObjectValidationHandler(
                in HttpSecurityScheme value,
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
                        ignoredResult = ignoredResult.PushValidationLocationProperty("required");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation required - ignored because the value is not an object");
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

                bool hasSeenScheme = false;
                bool hasSeenType = false;
                int propertyCount = 0;
                foreach (JsonObjectProperty property in value.EnumerateObject())
                {
                    string? propertyNameAsString = null;
                    propertyNameAsString ??= property.Name.GetString();
                    if (PatternProperties.IsMatch(propertyNameAsString))
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

                    if (property.NameEquals(JsonPropertyNames.BearerFormatUtf8, JsonPropertyNames.BearerFormat))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/bearerFormat"), JsonPropertyNames.BearerFormat);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonString>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.DescriptionUtf8, JsonPropertyNames.Description))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/description"), JsonPropertyNames.Description);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonString>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.SchemeUtf8, JsonPropertyNames.Scheme))
                    {
                        hasSeenScheme = true;
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/scheme"), JsonPropertyNames.Scheme);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonString>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.TypeUtf8, JsonPropertyNames.Type))
                    {
                        hasSeenType = true;
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/type"), JsonPropertyNames.Type);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.HttpSecurityScheme.TypeEntity>().Validate(result.CreateChildContext(), level);
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
                    result = result.PushValidationLocationReducedPathModifier(new("#/required/0"));
                }

                if (!hasSeenScheme)
                {
                    if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation properties - the required property 'scheme' was not present.");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
                else if (level == ValidationLevel.Verbose)
                {
                    result = result.WithResult(isValid: true, "Validation properties - the required property 'scheme' was present.");
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PushValidationLocationReducedPathModifier(new("#/required/1"));
                }

                if (!hasSeenType)
                {
                    if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation properties - the required property 'type' was not present.");
                    }
                    else
                    {
                        return ValidationContext.InvalidContext;
                    }
                }
                else if (level == ValidationLevel.Verbose)
                {
                    result = result.WithResult(isValid: true, "Validation properties - the required property 'type' was present.");
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                return result;
            }

#if NET8_0_OR_GREATER && !SPECFLOW_BUILD
            [GeneratedRegex("^x-")]
            private static partial Regex CreatePatternProperties();
#else
            private static Regex CreatePatternProperties() => new("^x-", RegexOptions.Compiled);
#endif
        }
    }
}
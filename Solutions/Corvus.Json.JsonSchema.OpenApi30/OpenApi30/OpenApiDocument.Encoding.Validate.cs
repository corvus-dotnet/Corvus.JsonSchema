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
    public readonly partial struct Encoding
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
                result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.0/schema/2021-09-28#/definitions/Encoding");
            }

            JsonValueKind valueKind = this.ValueKind;
            result = result.UsingEvaluatedProperties();
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
            /// A regular expression for the <c>patternProperties</c> keyword.
            /// </summary>
            public static readonly Regex PatternProperties = CreatePatternProperties();

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
                in Encoding value,
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
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation additionalProperties - ignored because the value is not an object", "additionalProperties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation properties - ignored because the value is not an object", "properties");
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation patternProperties - ignored because the value is not an object", "patternProperties");
                        return ignoredResult;
                    }

                    return validationContext;
                }

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

                    if (property.NameEquals(JsonPropertyNames.AllowReservedUtf8, JsonPropertyNames.AllowReserved))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/allowReserved"), JsonPropertyNames.AllowReserved);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Encoding.AllowReservedEntity>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.ContentTypeUtf8, JsonPropertyNames.ContentType))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/contentType"), JsonPropertyNames.ContentType);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonString>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.ExplodeUtf8, JsonPropertyNames.Explode))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/explode"), JsonPropertyNames.Explode);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonBoolean>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.HeadersUtf8, JsonPropertyNames.Headers))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/headers"), JsonPropertyNames.Headers);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Encoding.HeadersEntity>().Validate(result.CreateChildContext(), level);
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
                    else if (property.NameEquals(JsonPropertyNames.StyleUtf8, JsonPropertyNames.Style))
                    {
                        result = result.WithLocalProperty(propertyCount);
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PushValidationLocationReducedPathModifierAndProperty(new("#/properties/style"), JsonPropertyNames.Style);
                        }

                        ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Encoding.StyleEntity>().Validate(result.CreateChildContext(), level);
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

                return result;
            }

#if NET8_0_OR_GREATER && !DYNAMIC_BUILD
            [GeneratedRegex("^x-")]
            private static partial Regex CreatePatternProperties();
#else
            private static Regex CreatePatternProperties() => new("^x-", RegexOptions.Compiled);
#endif
        }
    }
}
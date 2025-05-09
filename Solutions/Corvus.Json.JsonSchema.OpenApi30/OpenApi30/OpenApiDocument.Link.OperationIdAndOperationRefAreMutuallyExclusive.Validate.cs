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
    public readonly partial struct Link
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operation Id and Operation Ref are mutually exclusive
        /// </para>
        /// </remarks>
        public readonly partial struct OperationIdAndOperationRefAreMutuallyExclusive
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

                    result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.0/schema/2021-09-28#/definitions/Link/not");
                }

                JsonValueKind valueKind = this.ValueKind;

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
                /// Object validation.
                /// </summary>
                /// <param name="value">The value to validate.</param>
                /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
                /// <param name="validationContext">The current validation context.</param>
                /// <param name="level">The current validation level.</param>
                /// <returns>The resulting validation context after validation.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static ValidationContext ObjectValidationHandler(
                    in OperationIdAndOperationRefAreMutuallyExclusive value,
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
                            ignoredResult = ignoredResult.WithResult(isValid: true, "Validation required - ignored because the value is not an object", "required");

                            return ignoredResult;
                        }

                        return validationContext;
                    }

                    bool hasSeenOperationId = false;
                    bool hasSeenOperationRef = false;

                    int propertyCount = 0;
                    foreach (JsonObjectProperty property in value.EnumerateObject())
                    {
                        if (property.NameEquals(JsonPropertyNames.OperationIdUtf8, JsonPropertyNames.OperationId))
                        {
                            hasSeenOperationId = true;
                            result = result.WithLocalProperty(propertyCount);
                            if (level > ValidationLevel.Basic)
                            {
                                result = result.PushValidationLocationReducedPathModifierAndProperty(new(""), JsonPropertyNames.OperationId);
                            }

                            ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonAny>().Validate(result.CreateChildContext(), level);
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
                        else if (property.NameEquals(JsonPropertyNames.OperationRefUtf8, JsonPropertyNames.OperationRef))
                        {
                            hasSeenOperationRef = true;
                            result = result.WithLocalProperty(propertyCount);
                            if (level > ValidationLevel.Basic)
                            {
                                result = result.PushValidationLocationReducedPathModifierAndProperty(new(""), JsonPropertyNames.OperationRef);
                            }

                            ValidationContext propertyResult = property.Value.As<Corvus.Json.JsonAny>().Validate(result.CreateChildContext(), level);
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

                    if (!hasSeenOperationId)
                    {
                        if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation required - the required property 'operationId' was not present.");
                        }
                        else
                        {
                            return ValidationContext.InvalidContext;
                        }
                    }
                    else if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, "Validation required - the required property 'operationId' was present.");
                    }

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushValidationLocationReducedPathModifier(new("#/required/1"));
                    }

                    if (!hasSeenOperationRef)
                    {
                        if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation required - the required property 'operationRef' was not present.");
                        }
                        else
                        {
                            return ValidationContext.InvalidContext;
                        }
                    }
                    else if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, "Validation required - the required property 'operationRef' was present.");
                    }

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }

                    return result;
                }
            }
        }
    }
}

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
    public readonly partial struct SecurityScheme
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct TypeHttpBearerEntity
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct RequiredSchemeAndType
            {
                /// <summary>
                /// Generated from JSON Schema.
                /// </summary>
                public readonly partial struct SchemeEntity
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
                            result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.1/schema/2022-10-07#/$defs/security-scheme/$defs/type-http-bearer/if/properties/scheme");
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

                    /// <summary>
                    /// Validation constants for the type.
                    /// </summary>
                    public static partial class CorvusValidation
                    {
                        /// <summary>
                        /// A regular expression for the <c>pattern</c> keyword.
                        /// </summary>
                        public static readonly Regex Pattern = CreatePattern();

                        /// <summary>
                        /// Core type validation.
                        /// </summary>
                        /// <param name="value">The value to validate.</param>
                        /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
                        /// <param name="validationContext">The current validation context.</param>
                        /// <param name="level">The current validation level.</param>
                        /// <returns>The resulting validation context after validation.</returns>
                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        internal static ValidationContext TypeValidationHandler(
                            in SchemeEntity value,
                            JsonValueKind valueKind,
                            in ValidationContext validationContext,
                            ValidationLevel level = ValidationLevel.Flag)
                        {
                            ValidationContext result = validationContext;
                            return Corvus.Json.Validate.TypeString(valueKind, result, level);
                        }

                        /// <summary>
                        /// String validation.
                        /// </summary>
                        /// <param name="value">The value to validate.</param>
                        /// <param name="valueKind">The <see cref="JsonValueKind" /> of the value to validate.</param>
                        /// <param name="validationContext">The current validation context.</param>
                        /// <param name="level">The current validation level.</param>
                        /// <returns>The resulting validation context after validation.</returns>
                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        internal static ValidationContext StringValidationHandler(
                            in SchemeEntity value,
                            JsonValueKind valueKind,
                            in ValidationContext validationContext,
                            ValidationLevel level = ValidationLevel.Flag)
                        {
                            if (valueKind != JsonValueKind.String)
                            {
                                if (level == ValidationLevel.Verbose)
                                {
                                    ValidationContext ignoredResult = validationContext;
                                    ignoredResult = ignoredResult.PushValidationLocationProperty("pattern");
                                    ignoredResult = ignoredResult.WithResult(isValid: true, "Validation pattern - ignored because the value is not a string");
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
                                result = context.Context;
                                if (context.Level > ValidationLevel.Basic)
                                {
                                    result = result.PushValidationLocationReducedPathModifier(new("#/pattern"));
                                }

                                if (Pattern.IsMatch(input))
                                {
                                    if (context.Level == ValidationLevel.Verbose)
                                    {
                                        result = result.WithResult(isValid: true, $"Validation pattern - {input.ToString()} matched  '^[Bb][Ee][Aa][Rr][Ee][Rr]$'");
                                    }
                                }
                                else
                                {
                                    if (context.Level >= ValidationLevel.Detailed)
                                    {
                                        result = result.WithResult(isValid: false, $"Validation pattern - {input.ToString()} did not match  '^[Bb][Ee][Aa][Rr][Ee][Rr]$'");
                                    }
                                    else if (context.Level >= ValidationLevel.Basic)
                                    {
                                        result = result.WithResult(isValid: false, "Validation pattern - The value did not match  '^[Bb][Ee][Aa][Rr][Ee][Rr]$'");
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

#if NET8_0_OR_GREATER && !SPECFLOW_BUILD
                        [GeneratedRegex("^[Bb][Ee][Aa][Rr][Ee][Rr]$")]
                        private static partial Regex CreatePattern();
#else
                        private static Regex CreatePattern() => new("^[Bb][Ee][Aa][Rr][Ee][Rr]$", RegexOptions.Compiled);
#endif
                    }
                }
            }
        }
    }
}
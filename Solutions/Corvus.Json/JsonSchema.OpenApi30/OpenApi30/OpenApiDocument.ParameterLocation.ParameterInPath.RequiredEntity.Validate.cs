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
    /// <remarks>
    /// <para>
    /// Parameter location
    /// </para>
    /// </remarks>
    public readonly partial struct ParameterLocation
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Parameter in path
        /// </para>
        /// </remarks>
        public readonly partial struct ParameterInPath
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct RequiredEntity
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
                        result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.0/schema/2021-09-28#/definitions/ParameterLocation/oneOf/0/properties/required");
                    }

                    result = CorvusValidation.CompositionAnyOfValidationHandler(this, result, level);
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
                /// Constant values for the enum keyword.
                /// </summary>
                public static class EnumValues
                {
                    /// <summary>
                    /// Gets the boolean value 'true'
                    /// as a <see cref="Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ParameterLocation.ParameterInPath.RequiredEntity"/>.
                    /// </summary>
                    public static RequiredEntity Item1 { get; } = CorvusValidation.Enum.As<RequiredEntity>();
                }

                private static partial class CorvusValidation
                {
                    public static readonly JsonBoolean Enum = JsonBoolean.ParseValue("true");

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public static ValidationContext CompositionAnyOfValidationHandler(
                        in RequiredEntity value,
                        in ValidationContext validationContext,
                        ValidationLevel level = ValidationLevel.Flag)
                    {
                        ValidationContext result = validationContext;
                        result = ValidateEnum(value, result, level);
                        if (!result.IsValid && level == ValidationLevel.Flag)
                        {
                            return result;
                        }

                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        static ValidationContext ValidateEnum(in RequiredEntity value, in ValidationContext validationContext, ValidationLevel level)
                        {
                            ValidationContext result = validationContext;
                            bool enumFoundValid = false;
                            enumFoundValid = value.Equals(CorvusValidation.Enum);
                            if (level >= ValidationLevel.Basic)
                            {
                                result.PushValidationLocationProperty("enum");
                            }

                            if (enumFoundValid)
                            {
                                if (level >= ValidationLevel.Verbose)
                                {
                                    result = result.WithResult(isValid: true, "Validation enum - validated against the enumeration.");
                                }
                            }
                            else
                            {
                                if (level >= ValidationLevel.Basic)
                                {
                                    result = result.WithResult(isValid: false, "Validation enum - did not validate against the enumeration.");
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
                }
            }
        }
    }
}
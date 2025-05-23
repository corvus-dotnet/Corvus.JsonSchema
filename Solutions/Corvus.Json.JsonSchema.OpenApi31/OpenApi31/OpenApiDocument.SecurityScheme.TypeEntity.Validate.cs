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
        public readonly partial struct TypeEntity
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

                    result = result.PushSchemaLocation("https://spec.openapis.org/oas/3.1/schema/2022-10-07#/$defs/security-scheme/properties/type");
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
                /// Gets the string 'apiKey'
                /// as a <see cref="Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity"/>.
                /// </summary>
                public static TypeEntity ApiKey { get; } = CorvusValidation.Enum1.As<TypeEntity>();

                /// <summary>
                /// Gets the string 'apiKey'
                /// as a UTF8 byte array.
                /// </summary>
                public static ReadOnlySpan<byte> ApiKeyUtf8 => CorvusValidation.Enum1Utf8;

                /// <summary>
                /// Gets the string 'http'
                /// as a <see cref="Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity"/>.
                /// </summary>
                public static TypeEntity Http { get; } = CorvusValidation.Enum2.As<TypeEntity>();

                /// <summary>
                /// Gets the string 'http'
                /// as a UTF8 byte array.
                /// </summary>
                public static ReadOnlySpan<byte> HttpUtf8 => CorvusValidation.Enum2Utf8;

                /// <summary>
                /// Gets the string 'mutualTLS'
                /// as a <see cref="Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity"/>.
                /// </summary>
                public static TypeEntity MutualTls { get; } = CorvusValidation.Enum3.As<TypeEntity>();

                /// <summary>
                /// Gets the string 'mutualTLS'
                /// as a UTF8 byte array.
                /// </summary>
                public static ReadOnlySpan<byte> MutualTlsUtf8 => CorvusValidation.Enum3Utf8;

                /// <summary>
                /// Gets the string 'oauth2'
                /// as a <see cref="Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity"/>.
                /// </summary>
                public static TypeEntity Oauth2 { get; } = CorvusValidation.Enum4.As<TypeEntity>();

                /// <summary>
                /// Gets the string 'oauth2'
                /// as a UTF8 byte array.
                /// </summary>
                public static ReadOnlySpan<byte> Oauth2Utf8 => CorvusValidation.Enum4Utf8;

                /// <summary>
                /// Gets the string 'openIdConnect'
                /// as a <see cref="Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeEntity"/>.
                /// </summary>
                public static TypeEntity OpenIdConnect { get; } = CorvusValidation.Enum5.As<TypeEntity>();

                /// <summary>
                /// Gets the string 'openIdConnect'
                /// as a UTF8 byte array.
                /// </summary>
                public static ReadOnlySpan<byte> OpenIdConnectUtf8 => CorvusValidation.Enum5Utf8;
            }

            /// <summary>
            /// Validation constants for the type.
            /// </summary>
            public static partial class CorvusValidation
            {
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static readonly JsonString Enum1 = JsonString.ParseValue("\"apiKey\"");
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static readonly JsonString Enum2 = JsonString.ParseValue("\"http\"");
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static readonly JsonString Enum3 = JsonString.ParseValue("\"mutualTLS\"");
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static readonly JsonString Enum4 = JsonString.ParseValue("\"oauth2\"");
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static readonly JsonString Enum5 = JsonString.ParseValue("\"openIdConnect\"");

                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static ReadOnlySpan<byte> Enum1Utf8 => "\"apiKey\""u8;
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static ReadOnlySpan<byte> Enum2Utf8 => "\"http\""u8;
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static ReadOnlySpan<byte> Enum3Utf8 => "\"mutualTLS\""u8;
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static ReadOnlySpan<byte> Enum4Utf8 => "\"oauth2\""u8;
                /// <summary>
                /// A constant for the <c>enum</c> keyword.
                /// </summary>
                public static ReadOnlySpan<byte> Enum5Utf8 => "\"openIdConnect\""u8;

                /// <summary>
                /// Composition validation (any-of).
                /// </summary>
                /// <param name="value">The value to validate.</param>
                /// <param name="validationContext">The current validation context.</param>
                /// <param name="level">The current validation level.</param>
                /// <returns>The resulting validation context after validation.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static ValidationContext CompositionAnyOfValidationHandler(
                    in TypeEntity value,
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
                    static ValidationContext ValidateEnum(in TypeEntity value, in ValidationContext validationContext, ValidationLevel level)
                    {
                        ValidationContext result = validationContext;
                        bool enumFoundValid = false;

                        enumFoundValid = value.Equals(CorvusValidation.Enum1);
                        if (!enumFoundValid)
                        {
                            enumFoundValid = value.Equals(CorvusValidation.Enum2);
                        }
                        if (!enumFoundValid)
                        {
                            enumFoundValid = value.Equals(CorvusValidation.Enum3);
                        }
                        if (!enumFoundValid)
                        {
                            enumFoundValid = value.Equals(CorvusValidation.Enum4);
                        }
                        if (!enumFoundValid)
                        {
                            enumFoundValid = value.Equals(CorvusValidation.Enum5);
                        }

                        if (enumFoundValid)
                        {
                            if (level >= ValidationLevel.Verbose)
                            {
                                result = result.WithResult(isValid: true, "Validation enum - validated against the enumeration.", "enum");
                            }
                        }
                        else
                        {
                            if (level == ValidationLevel.Flag)
                            {
                                result = result.WithResult(isValid: false);
                            }
                            else
                            {
                                result = result.WithResult(isValid: false, "Validation enum - did not validate against the enumeration.", "enum");
                            }
                        }

                        return result;
                    }

                    return result;
                }
            }
        }
    }
}

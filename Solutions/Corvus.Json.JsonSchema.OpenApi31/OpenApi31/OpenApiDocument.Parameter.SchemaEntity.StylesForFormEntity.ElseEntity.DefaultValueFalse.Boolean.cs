//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

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
    public readonly partial struct Parameter
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct SchemaEntity
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct StylesForFormEntity
            {
                /// <summary>
                /// Generated from JSON Schema.
                /// </summary>
                public readonly partial struct ElseEntity
                {
                    /// <summary>
                    /// Generated from JSON Schema.
                    /// </summary>
                    /// <remarks>
                    /// <para>
                    /// Examples:
                    /// <example>
                    /// <code>
                    /// false
                    /// </code>
                    /// </example>
                    /// </para>
                    /// </remarks>
                    public readonly partial struct DefaultValueFalse
                        : IJsonBoolean<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForFormEntity.ElseEntity.DefaultValueFalse>
                    {
                        /// <summary>
                        /// Conversion from <see cref="bool"/>.
                        /// </summary>
                        /// <param name="value">The value from which to convert.</param>
                        public static implicit operator DefaultValueFalse(bool value)
                        {
                            return new(value);
                        }

                        /// <summary>
                        /// Conversion from JsonBoolean.
                        /// </summary>
                        /// <param name="value">The value from which to convert.</param>
                        public static implicit operator DefaultValueFalse(JsonBoolean value)
                        {
                            if (value.HasDotnetBacking && (value.ValueKind == JsonValueKind.False || value.ValueKind == JsonValueKind.True))
                            {
                                return new(
                                    (bool)value);
                            }

                            return new(value.AsJsonElement);
                        }

                        /// <summary>
                        /// Conversion to JsonBoolean.
                        /// </summary>
                        /// <param name="value">The value from which to convert.</param>
                        public static implicit operator JsonBoolean(DefaultValueFalse value)
                        {
                            return
                                value.AsBoolean;
                        }

                        /// <summary>
                        /// Conversion to <see langword="bool"/>.
                        /// </summary>
                        /// <param name="value">The value from which to convert.</param>
                        /// <exception cref="InvalidOperationException">The value was not a boolean.</exception>
                        public static implicit operator bool(DefaultValueFalse value)
                        {
                            return value.GetBoolean() ?? throw new InvalidOperationException();
                        }

                        /// <summary>
                        /// Try to retrieve the value as a boolean.
                        /// </summary>
                        /// <param name="result"><see langword="true"/> if the value was true, otherwise <see langword="false"/>.</param>
                        /// <returns><see langword="true"/> if the value was representable as a boolean, otherwise <see langword="false"/>.</returns>
                        public bool TryGetBoolean([NotNullWhen(true)] out bool result)
                        {
                            switch (this.ValueKind)
                            {
                                case JsonValueKind.True:
                                    result = true;
                                    return true;
                                case JsonValueKind.False:
                                    result = false;
                                    return true;
                                default:
                                    result = default;
                                    return false;
                            }
                        }

                        /// <summary>
                        /// Get the value as a boolean.
                        /// </summary>
                        /// <returns>The value of the boolean, or <see langword="null"/> if the value was not representable as a boolean.</returns>
                        public bool? GetBoolean()
                        {
                            if (this.TryGetBoolean(out bool result))
                            {
                                return result;
                            }

                            return null;
                        }
                    }
                }
            }
        }
    }
}
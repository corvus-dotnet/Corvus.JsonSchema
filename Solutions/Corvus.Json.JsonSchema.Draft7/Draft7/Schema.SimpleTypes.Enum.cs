//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.Draft7;
public readonly partial struct Schema
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct SimpleTypes
    {
        /// <summary>
        /// Matches the value against each of the enumeration values, and returns the result of calling the provided match function for the first match found.
        /// </summary>
        /// <param name = "context">The context to pass to the match function.</param>
        /// <param name = "matchArray">The function to call if the value matches the JSON value "\"array\"".</param>
        /// <param name = "matchBoolean">The function to call if the value matches the JSON value "\"boolean\"".</param>
        /// <param name = "matchInteger">The function to call if the value matches the JSON value "\"integer\"".</param>
        /// <param name = "matchNull">The function to call if the value matches the JSON value "\"null\"".</param>
        /// <param name = "matchNumber">The function to call if the value matches the JSON value "\"number\"".</param>
        /// <param name = "matchObject">The function to call if the value matches the JSON value "\"object\"".</param>
        /// <param name = "matchString">The function to call if the value matches the JSON value "\"string\"".</param>
        /// <param name = "defaultMatch">The fallback match.</param>
        public TOut Match<TIn, TOut>(in TIn context, Func<TIn, TOut> matchArray, Func<TIn, TOut> matchBoolean, Func<TIn, TOut> matchInteger, Func<TIn, TOut> matchNull, Func<TIn, TOut> matchNumber, Func<TIn, TOut> matchObject, Func<TIn, TOut> matchString, Func<TIn, TOut> defaultMatch)
        {
            if (this.ValueKind == JsonValueKind.String)
            {
                if (this.HasJsonElementBacking)
                {
                    if (this.jsonElementBacking.ValueEquals(EnumValues.ArrayUtf8))
                    {
                        return matchArray(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.BooleanUtf8))
                    {
                        return matchBoolean(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.IntegerUtf8))
                    {
                        return matchInteger(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.NullUtf8))
                    {
                        return matchNull(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.NumberUtf8))
                    {
                        return matchNumber(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.ObjectUtf8))
                    {
                        return matchObject(context);
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.StringUtf8))
                    {
                        return matchString(context);
                    }
                }
                else
                {
                    switch (this.stringBacking)
                    {
                        case "array":
                            return matchArray(context);
                        case "boolean":
                            return matchBoolean(context);
                        case "integer":
                            return matchInteger(context);
                        case "null":
                            return matchNull(context);
                        case "number":
                            return matchNumber(context);
                        case "object":
                            return matchObject(context);
                        case "string":
                            return matchString(context);
                        default:
                            break;
                    }
                }
            }

            return defaultMatch(context);
        }

        /// <summary>
        /// Matches the value against each of the enumeration values, and returns the result of calling the provided match function for the first match found.
        /// </summary>
        /// <param name = "matchArray">The function to call if the value matches the JSON value "\"array\"".</param>
        /// <param name = "matchBoolean">The function to call if the value matches the JSON value "\"boolean\"".</param>
        /// <param name = "matchInteger">The function to call if the value matches the JSON value "\"integer\"".</param>
        /// <param name = "matchNull">The function to call if the value matches the JSON value "\"null\"".</param>
        /// <param name = "matchNumber">The function to call if the value matches the JSON value "\"number\"".</param>
        /// <param name = "matchObject">The function to call if the value matches the JSON value "\"object\"".</param>
        /// <param name = "matchString">The function to call if the value matches the JSON value "\"string\"".</param>
        /// <param name = "defaultMatch">The fallback match.</param>
        public TOut Match<TOut>(Func<TOut> matchArray, Func<TOut> matchBoolean, Func<TOut> matchInteger, Func<TOut> matchNull, Func<TOut> matchNumber, Func<TOut> matchObject, Func<TOut> matchString, Func<TOut> defaultMatch)
        {
            if (this.ValueKind == JsonValueKind.String)
            {
                if (this.HasJsonElementBacking)
                {
                    if (this.jsonElementBacking.ValueEquals(EnumValues.ArrayUtf8))
                    {
                        return matchArray();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.BooleanUtf8))
                    {
                        return matchBoolean();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.IntegerUtf8))
                    {
                        return matchInteger();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.NullUtf8))
                    {
                        return matchNull();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.NumberUtf8))
                    {
                        return matchNumber();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.ObjectUtf8))
                    {
                        return matchObject();
                    }

                    if (this.jsonElementBacking.ValueEquals(EnumValues.StringUtf8))
                    {
                        return matchString();
                    }
                }
                else
                {
                    switch (this.stringBacking)
                    {
                        case "array":
                            return matchArray();
                        case "boolean":
                            return matchBoolean();
                        case "integer":
                            return matchInteger();
                        case "null":
                            return matchNull();
                        case "number":
                            return matchNumber();
                        case "object":
                            return matchObject();
                        case "string":
                            return matchString();
                        default:
                            break;
                    }
                }
            }

            return defaultMatch();
        }

        /// <summary>
        /// Permitted values.
        /// </summary>
        public static class EnumValues
        {
            /// <summary>
            /// Gets "array" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Array = SimpleTypes.Parse("\"array\"");
            /// <summary>
            /// Gets "array" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> ArrayUtf8 => "array"u8;

            /// <summary>
            /// Gets "boolean" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Boolean = SimpleTypes.Parse("\"boolean\"");
            /// <summary>
            /// Gets "boolean" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> BooleanUtf8 => "boolean"u8;

            /// <summary>
            /// Gets "integer" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Integer = SimpleTypes.Parse("\"integer\"");
            /// <summary>
            /// Gets "integer" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> IntegerUtf8 => "integer"u8;

            /// <summary>
            /// Gets "null" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Null = SimpleTypes.Parse("\"null\"");
            /// <summary>
            /// Gets "null" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> NullUtf8 => "null"u8;

            /// <summary>
            /// Gets "number" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Number = SimpleTypes.Parse("\"number\"");
            /// <summary>
            /// Gets "number" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> NumberUtf8 => "number"u8;

            /// <summary>
            /// Gets "object" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes Object = SimpleTypes.Parse("\"object\"");
            /// <summary>
            /// Gets "object" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> ObjectUtf8 => "object"u8;

            /// <summary>
            /// Gets "string" as a JSON value.
            /// </summary>
            public static readonly SimpleTypes String = SimpleTypes.Parse("\"string\"");
            /// <summary>
            /// Gets "string" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> StringUtf8 => "string"u8;

            /// <summary>
            /// Gets "array" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item0 = SimpleTypes.Parse("\"array\"");
            /// <summary>
            /// Gets "boolean" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item1 = SimpleTypes.Parse("\"boolean\"");
            /// <summary>
            /// Gets "integer" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item2 = SimpleTypes.Parse("\"integer\"");
            /// <summary>
            /// Gets "null" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item3 = SimpleTypes.Parse("\"null\"");
            /// <summary>
            /// Gets "number" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item4 = SimpleTypes.Parse("\"number\"");
            /// <summary>
            /// Gets "object" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item5 = SimpleTypes.Parse("\"object\"");
            /// <summary>
            /// Gets "string" as a JSON value.
            /// </summary>
            internal static readonly SimpleTypes Item6 = SimpleTypes.Parse("\"string\"");
        }
    }
}
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

namespace Model.V3;
public readonly partial struct Basictypes
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct ExtJsonString2
    {
        /// <summary>
        /// Matches the value against each of the enumeration values, and returns the result of calling the provided match function for the first match found.
        /// </summary>
        /// <param name = "context">The context to pass to the match function.</param>
        /// <param name = "matchType1">The function to call if the value matches the JSON value "\"1\"".</param>
        /// <param name = "defaultMatch">The fallback match.</param>
        public TOut Match<TIn, TOut>(in TIn context, Func<TIn, TOut> matchType1, Func<TIn, TOut> defaultMatch)
        {
            if (this.ValueKind == JsonValueKind.String)
            {
                if (this.HasJsonElementBacking)
                {
                    if (this.jsonElementBacking.ValueEquals(EnumValues.Type1Utf8))
                    {
                        return matchType1(context);
                    }
                }
                else
                {
                    switch (this.stringBacking)
                    {
                        case "1":
                            return matchType1(context);
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
        /// <param name = "matchType1">The function to call if the value matches the JSON value "\"1\"".</param>
        /// <param name = "defaultMatch">The fallback match.</param>
        public TOut Match<TOut>(Func<TOut> matchType1, Func<TOut> defaultMatch)
        {
            if (this.ValueKind == JsonValueKind.String)
            {
                if (this.HasJsonElementBacking)
                {
                    if (this.jsonElementBacking.ValueEquals(EnumValues.Type1Utf8))
                    {
                        return matchType1();
                    }
                }
                else
                {
                    switch (this.stringBacking)
                    {
                        case "1":
                            return matchType1();
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
            /// Gets "1" as a JSON value.
            /// </summary>
            public static readonly ExtJsonString2 Type1 = ExtJsonString2.Parse("\"1\"");
            /// <summary>
            /// Gets "1" as a UTF8 string.
            /// </summary>
            public static ReadOnlySpan<byte> Type1Utf8 => "1"u8;

            /// <summary>
            /// Gets "1" as a JSON value.
            /// </summary>
            internal static readonly ExtJsonString2 Item0 = ExtJsonString2.Parse("\"1\"");
        }
    }
}
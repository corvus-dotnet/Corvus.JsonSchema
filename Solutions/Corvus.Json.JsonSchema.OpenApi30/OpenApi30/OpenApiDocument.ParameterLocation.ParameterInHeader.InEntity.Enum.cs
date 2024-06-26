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

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    public readonly partial struct ParameterLocation
    {
        public readonly partial struct ParameterInHeader
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct InEntity
            {
                /// <summary>
                /// Matches the value against each of the enumeration values, and returns the result of calling the provided match function for the first match found.
                /// </summary>
                /// <param name = "context">The context to pass to the match function.</param>
                /// <param name = "matchHeader">The function to call if the value matches the JSON value "\"header\"".</param>
                /// <param name = "defaultMatch">The fallback match.</param>
                public TOut Match<TIn, TOut>(in TIn context, Func<TIn, TOut> matchHeader, Func<TIn, TOut> defaultMatch)
                {
                    if (this.ValueKind == JsonValueKind.String)
                    {
                        if (this.HasJsonElementBacking)
                        {
                            if (this.jsonElementBacking.ValueEquals(EnumValues.HeaderUtf8))
                            {
                                return matchHeader(context);
                            }
                        }
                        else
                        {
                            switch (this.stringBacking)
                            {
                                case "header":
                                    return matchHeader(context);
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
                /// <param name = "matchHeader">The function to call if the value matches the JSON value "\"header\"".</param>
                /// <param name = "defaultMatch">The fallback match.</param>
                public TOut Match<TOut>(Func<TOut> matchHeader, Func<TOut> defaultMatch)
                {
                    if (this.ValueKind == JsonValueKind.String)
                    {
                        if (this.HasJsonElementBacking)
                        {
                            if (this.jsonElementBacking.ValueEquals(EnumValues.HeaderUtf8))
                            {
                                return matchHeader();
                            }
                        }
                        else
                        {
                            switch (this.stringBacking)
                            {
                                case "header":
                                    return matchHeader();
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
                    /// Gets "header" as a JSON value.
                    /// </summary>
                    public static readonly InEntity Header = InEntity.Parse("\"header\"");
                    /// <summary>
                    /// Gets "header" as a UTF8 string.
                    /// </summary>
                    public static ReadOnlySpan<byte> HeaderUtf8 => "header"u8;

                    /// <summary>
                    /// Gets "header" as a JSON value.
                    /// </summary>
                    internal static readonly InEntity Item0 = InEntity.Parse("\"header\"");
                }
            }
        }
    }
}
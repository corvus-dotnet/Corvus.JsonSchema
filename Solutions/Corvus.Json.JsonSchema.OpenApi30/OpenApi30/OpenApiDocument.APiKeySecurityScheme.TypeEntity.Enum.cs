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
    public readonly partial struct APiKeySecurityScheme
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct TypeEntity
        {
            /// <summary>
            /// Matches the value against each of the enumeration values, and returns the result of calling the provided match function for the first match found.
            /// </summary>
            /// <param name = "context">The context to pass to the match function.</param>
            /// <param name = "matchApiKey">The function to call if the value matches the JSON value "\"apiKey\"".</param>
            /// <param name = "defaultMatch">The fallback match.</param>
            public TOut Match<TIn, TOut>(in TIn context, Func<TIn, TOut> matchApiKey, Func<TIn, TOut> defaultMatch)
            {
                if (this.ValueKind == JsonValueKind.String)
                {
                    if (this.HasJsonElementBacking)
                    {
                        if (this.jsonElementBacking.ValueEquals(EnumValues.ApiKeyUtf8))
                        {
                            return matchApiKey(context);
                        }
                    }
                    else
                    {
                        switch (this.stringBacking)
                        {
                            case "apiKey":
                                return matchApiKey(context);
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
            /// <param name = "matchApiKey">The function to call if the value matches the JSON value "\"apiKey\"".</param>
            /// <param name = "defaultMatch">The fallback match.</param>
            public TOut Match<TOut>(Func<TOut> matchApiKey, Func<TOut> defaultMatch)
            {
                if (this.ValueKind == JsonValueKind.String)
                {
                    if (this.HasJsonElementBacking)
                    {
                        if (this.jsonElementBacking.ValueEquals(EnumValues.ApiKeyUtf8))
                        {
                            return matchApiKey();
                        }
                    }
                    else
                    {
                        switch (this.stringBacking)
                        {
                            case "apiKey":
                                return matchApiKey();
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
                /// Gets "apiKey" as a JSON value.
                /// </summary>
                public static readonly TypeEntity ApiKey = TypeEntity.Parse("\"apiKey\"");
                /// <summary>
                /// Gets "apiKey" as a UTF8 string.
                /// </summary>
                public static ReadOnlySpan<byte> ApiKeyUtf8 => "apiKey"u8;

                /// <summary>
                /// Gets "apiKey" as a JSON value.
                /// </summary>
                internal static readonly TypeEntity Item0 = TypeEntity.Parse("\"apiKey\"");
            }
        }
    }
}
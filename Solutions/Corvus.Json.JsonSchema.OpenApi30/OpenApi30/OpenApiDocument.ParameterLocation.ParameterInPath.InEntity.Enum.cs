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
        public readonly partial struct ParameterInPath
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
                /// <param name = "matchPath">The function to call if the value matches the JSON value "\"path\"".</param>
                /// <param name = "defaultMatch">The fallback match.</param>
                public TOut Match<TIn, TOut>(in TIn context, Func<TIn, TOut> matchPath, Func<TIn, TOut> defaultMatch)
                {
                    if (this.ValueKind == JsonValueKind.String)
                    {
                        if (this.HasJsonElementBacking)
                        {
                            if (this.jsonElementBacking.ValueEquals(EnumValues.PathUtf8))
                            {
                                return matchPath(context);
                            }
                        }
                        else
                        {
                            switch (this.stringBacking)
                            {
                                case "path":
                                    return matchPath(context);
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
                /// <param name = "matchPath">The function to call if the value matches the JSON value "\"path\"".</param>
                /// <param name = "defaultMatch">The fallback match.</param>
                public TOut Match<TOut>(Func<TOut> matchPath, Func<TOut> defaultMatch)
                {
                    if (this.ValueKind == JsonValueKind.String)
                    {
                        if (this.HasJsonElementBacking)
                        {
                            if (this.jsonElementBacking.ValueEquals(EnumValues.PathUtf8))
                            {
                                return matchPath();
                            }
                        }
                        else
                        {
                            switch (this.stringBacking)
                            {
                                case "path":
                                    return matchPath();
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
                    /// Gets "path" as a JSON value.
                    /// </summary>
                    public static readonly InEntity Path = InEntity.Parse("\"path\"");
                    /// <summary>
                    /// Gets "path" as a UTF8 string.
                    /// </summary>
                    public static ReadOnlySpan<byte> PathUtf8 => "path"u8;

                    /// <summary>
                    /// Gets "path" as a JSON value.
                    /// </summary>
                    internal static readonly InEntity Item0 = InEntity.Parse("\"path\"");
                }
            }
        }
    }
}
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
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    public readonly partial struct SecurityScheme
    {
        public readonly partial struct TypeHttpBearerEntity
        {
            public readonly partial struct RequiredTypeAndScheme
            {
                /// <summary>
                /// Generated from JSON Schema.
                /// </summary>
                public readonly partial struct SchemeEntity
                {
                    private static Regex __CorvusPatternExpression => new Regex("^[Bb][Ee][Aa][Rr][Ee][Rr]$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Text.Json;
using Corvus.Json;

namespace Corvus.Json.JsonSchema.Draft201909;
public readonly partial struct Core
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct IdEntity
    {
        private ValidationContext ValidateFormat(JsonValueKind valueKind, in ValidationContext result, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.String)
            {
                return result;
            }

            return Corvus.Json.Validate.TypeUriReference(this, result, level);
        }
    }
}
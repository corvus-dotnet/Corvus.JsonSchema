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

namespace Model.V3;
public readonly partial struct Basictypes
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct ExtJsonUInt64
    {
        private ValidationContext ValidateFormat(JsonValueKind valueKind, in ValidationContext result, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Number)
            {
                return result;
            }

            return Corvus.Json.Validate.TypeUInt64(this, result, level);
        }
    }
}
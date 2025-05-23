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

namespace Corvus.Json.Benchmarking.Models.V3;
/// <summary>
/// A component of a person's name.
/// </summary>
/// <remarks>
/// <para>
/// This is an array of strings, each of which is a component of a person's name.
/// </para>
/// </remarks>
public readonly partial struct PersonNameElementArray
{
    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
    {
        ValidationContext result = validationContext;
        if (level > ValidationLevel.Flag)
        {
            result = result.UsingResults();
        }

        if (level > ValidationLevel.Basic)
        {
            result = result.UsingStack();
            result = result.PushSchemaLocation("person-schema.json#/$defs/PersonNameElementArray");
        }

        JsonValueKind valueKind = this.ValueKind;
        result = this.ValidateType(valueKind, result, level);
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        result = this.ValidateArray(valueKind, result, level);
        if (level == ValidationLevel.Flag && !result.IsValid)
        {
            return result;
        }

        if (level != ValidationLevel.Flag)
        {
            result = result.PopLocation();
        }

        return result;
    }
}
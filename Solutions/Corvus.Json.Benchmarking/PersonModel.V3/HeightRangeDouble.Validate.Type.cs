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
/// A numeric representation of a person's height in meters.
/// </summary>
public readonly partial struct HeightRangeDouble
{
    private ValidationContext ValidateType(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;
        bool isValid = false;
        ValidationContext localResultNumber = Corvus.Json.Validate.TypeNumber(valueKind, result.CreateChildContext(), level);
        if (level == ValidationLevel.Flag && localResultNumber.IsValid)
        {
            return validationContext;
        }

        if (localResultNumber.IsValid)
        {
            isValid = true;
        }

        result = result.MergeResults(isValid, level, localResultNumber);
        return result;
    }
}
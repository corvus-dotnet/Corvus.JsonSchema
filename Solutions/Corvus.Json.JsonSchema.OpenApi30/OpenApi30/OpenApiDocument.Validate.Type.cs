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

namespace Corvus.Json.JsonSchema.OpenApi30;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// The description of OpenAPI v3.0.x documents, as defined by https://spec.openapis.org/oas/v3.0.3
/// </para>
/// </remarks>
public readonly partial struct OpenApiDocument
{
    private ValidationContext ValidateType(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;
        bool isValid = false;
        ValidationContext localResultObject = Corvus.Json.Validate.TypeObject(valueKind, result.CreateChildContext(), level);
        if (level == ValidationLevel.Flag && localResultObject.IsValid)
        {
            return validationContext;
        }

        if (localResultObject.IsValid)
        {
            isValid = true;
        }

        result = result.MergeResults(isValid, level, localResultObject);
        return result;
    }
}
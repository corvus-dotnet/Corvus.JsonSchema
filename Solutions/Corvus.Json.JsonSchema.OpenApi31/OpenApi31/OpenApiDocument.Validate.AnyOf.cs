//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using Corvus.Json;

namespace Corvus.Json.JsonSchema.OpenApi31;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// The description of OpenAPI v3.1.x documents without schema validation, as defined by https://spec.openapis.org/oas/v3.1.0
/// </para>
/// </remarks>
public readonly partial struct OpenApiDocument
{
    private ValidationContext ValidateAnyOf(in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;
        if (level > ValidationLevel.Basic)
        {
            result = result.PushValidationLocationProperty("anyOf");
        }

        ValidationContext childContextBase = result;
        bool foundValid = false;
        ValidationContext childContext0 = childContextBase;
        if (level > ValidationLevel.Basic)
        {
            childContext0 = childContext0.PushValidationLocationArrayIndex(0);
        }

        ValidationContext anyOfResult0 = this.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.RequiredPaths>().Validate(childContext0.CreateChildContext(), level);
        if (anyOfResult0.IsValid)
        {
            result = result.MergeChildContext(anyOfResult0, level >= ValidationLevel.Verbose);
            foundValid = true;
        }
        else
        {
            if (level >= ValidationLevel.Verbose)
            {
                result = result.MergeResults(result.IsValid, level, anyOfResult0);
            }
        }

        ValidationContext childContext1 = childContextBase;
        if (level > ValidationLevel.Basic)
        {
            childContext1 = childContext1.PushValidationLocationArrayIndex(1);
        }

        ValidationContext anyOfResult1 = this.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.RequiredComponents>().Validate(childContext1.CreateChildContext(), level);
        if (anyOfResult1.IsValid)
        {
            result = result.MergeChildContext(anyOfResult1, level >= ValidationLevel.Verbose);
            foundValid = true;
        }
        else
        {
            if (level >= ValidationLevel.Verbose)
            {
                result = result.MergeResults(result.IsValid, level, anyOfResult1);
            }
        }

        ValidationContext childContext2 = childContextBase;
        if (level > ValidationLevel.Basic)
        {
            childContext2 = childContext2.PushValidationLocationArrayIndex(2);
        }

        ValidationContext anyOfResult2 = this.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.RequiredWebhooks>().Validate(childContext2.CreateChildContext(), level);
        if (anyOfResult2.IsValid)
        {
            result = result.MergeChildContext(anyOfResult2, level >= ValidationLevel.Verbose);
            foundValid = true;
        }
        else
        {
            if (level >= ValidationLevel.Verbose)
            {
                result = result.MergeResults(result.IsValid, level, anyOfResult2);
            }
        }

        if (foundValid)
        {
            if (level >= ValidationLevel.Verbose)
            {
                result = result.WithResult(isValid: true, "Validation 10.2.1.2. anyOf - validated against the anyOf schema.");
            }
        }
        else
        {
            if (level >= ValidationLevel.Detailed)
            {
                result = result.WithResult(isValid: false, "Validation 10.2.1.2. anyOf - failed to validate against the anyOf schema.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, "Validation 10.2.1.2. anyOf - failed to validate against the anyOf schema.");
            }
            else
            {
                result = result.WithResult(isValid: false);
            }
        }

        if (level > ValidationLevel.Basic)
        {
            result = result.PopLocation(); // anyOf
        }

        return result;
    }
}
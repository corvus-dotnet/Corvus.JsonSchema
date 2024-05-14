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

namespace JsonSchemaSample.Api;
public readonly partial struct PatchOperation
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Copy operation. "from" is a JSON Pointer.
    /// </para>
    /// </remarks>
    public readonly partial struct CopyOperation
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
                result = result.PushSchemaLocation("patch-operation.json#/$defs/CopyOperation");
            }

            result = this.ValidateRef(result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            JsonValueKind valueKind = this.ValueKind;
            result = this.ValidateObject(valueKind, result, level);
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
}
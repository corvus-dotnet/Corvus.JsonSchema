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

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct MediaType
    {
        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples AsExampleXOrExamples
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples"/>.
        /// </summary>
        public bool IsExampleXOrExamples
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsExampleXOrExamples(out Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.ExampleXOrExamples)this;
            return result.IsValid();
        }
    }
}
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
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct Link
    {
        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef AsRequiredOperationRef
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef"/>.
        /// </summary>
        public bool IsRequiredOperationRef
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsRequiredOperationRef(out Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationRef)this;
            return result.IsValid();
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId AsRequiredOperationId
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId"/>.
        /// </summary>
        public bool IsRequiredOperationId
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsRequiredOperationId(out Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Link.RequiredOperationId)this;
            return result.IsValid();
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions AsSpecificationExtensions
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        public bool IsSpecificationExtensions
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsSpecificationExtensions(out Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions)this;
            return result.IsValid();
        }
    }
}
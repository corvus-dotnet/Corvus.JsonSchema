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
    /// <remarks>
    /// <para>
    /// Schema and content are mutually exclusive, at least one is required
    /// </para>
    /// </remarks>
    public readonly partial struct SchemaXOrContent
    {
        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity AsOneOf0Entity
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity"/>.
        /// </summary>
        public bool IsOneOf0Entity
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsOneOf0Entity(out Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.OneOf0Entity)this;
            return result.IsValid();
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent"/>.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent AsSomePropertiesAreNotAllowedIfContentIsPresent
        {
            get
            {
                return (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent)this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent"/>.
        /// </summary>
        public bool IsSomePropertiesAreNotAllowedIfContentIsPresent
        {
            get
            {
                return ((Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent)this).IsValid();
            }
        }

        /// <summary>
        /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent"/>.
        /// </summary>
        /// <param name = "result">The result of the conversion.</param>
        /// <returns><c>True</c> if the conversion was valid.</returns>
        public bool TryGetAsSomePropertiesAreNotAllowedIfContentIsPresent(out Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent result)
        {
            result = (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.SchemaXOrContent.SomePropertiesAreNotAllowedIfContentIsPresent)this;
            return result.IsValid();
        }
    }
}
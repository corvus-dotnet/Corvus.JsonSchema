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
    public readonly partial struct Response
    {
        public readonly partial struct LinksEntity
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct AdditionalPropertiesEntity
            {
                /// <summary>
                /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link"/>.
                /// </summary>
                public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link AsLink
                {
                    get
                    {
                        return (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link)this;
                    }
                }

                /// <summary>
                /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link"/>.
                /// </summary>
                public bool IsLink
                {
                    get
                    {
                        return ((Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link)this).IsValid();
                    }
                }

                /// <summary>
                /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link"/>.
                /// </summary>
                /// <param name = "result">The result of the conversion.</param>
                /// <returns><c>True</c> if the conversion was valid.</returns>
                public bool TryGetAsLink(out Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link result)
                {
                    result = (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Link)this;
                    return result.IsValid();
                }

                /// <summary>
                /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference"/>.
                /// </summary>
                public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference AsReference
                {
                    get
                    {
                        return (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference)this;
                    }
                }

                /// <summary>
                /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference"/>.
                /// </summary>
                public bool IsReference
                {
                    get
                    {
                        return ((Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference)this).IsValid();
                    }
                }

                /// <summary>
                /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference"/>.
                /// </summary>
                /// <param name = "result">The result of the conversion.</param>
                /// <returns><c>True</c> if the conversion was valid.</returns>
                public bool TryGetAsReference(out Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference result)
                {
                    result = (Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference)this;
                    return result.IsValid();
                }
            }
        }
    }
}
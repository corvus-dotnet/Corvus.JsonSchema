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
    public readonly partial struct SecurityScheme
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct TypeOidcEntity
        {
            /// <summary>
            /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl"/>.
            /// </summary>
            public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl AsRequiredOpenIdConnectUrl
            {
                get
                {
                    return (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl)this;
                }
            }

            /// <summary>
            /// Gets a value indicating whether this is a valid <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl"/>.
            /// </summary>
            public bool IsRequiredOpenIdConnectUrl
            {
                get
                {
                    return ((Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl)this).IsValid();
                }
            }

            /// <summary>
            /// Gets the value as a <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl"/>.
            /// </summary>
            /// <param name = "result">The result of the conversion.</param>
            /// <returns><c>True</c> if the conversion was valid.</returns>
            public bool TryGetAsRequiredOpenIdConnectUrl(out Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl result)
            {
                result = (Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl)this;
                return result.IsValid();
            }
        }
    }
}
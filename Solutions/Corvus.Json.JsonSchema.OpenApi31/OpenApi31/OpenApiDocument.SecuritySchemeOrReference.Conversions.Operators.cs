//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct SecuritySchemeOrReference
    {
        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Reference"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Reference(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Reference.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Reference"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Reference value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SpecificationExtensions value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeApikeyEntity.RequiredNameAndIn value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.RequiredScheme"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.RequiredScheme(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.RequiredScheme.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.RequiredScheme"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpEntity.RequiredScheme value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.ThenEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.ThenEntity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.ThenEntity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.ThenEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeHttpBearerEntity.ThenEntity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.RequiredFlows"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.RequiredFlows(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.RequiredFlows.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.RequiredFlows"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOauth2Entity.RequiredFlows value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }

        /// <summary>
        /// Conversion to <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl(SecuritySchemeOrReference value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return new(value.AsJsonElement);
            }

            if ((value.backing & Backing.Object) != 0)
            {
                return new(value.objectBacking);
            }

            return Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl.Undefined;
        }

        /// <summary>
        /// Conversion from <see cref = "Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl"/>.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static explicit operator SecuritySchemeOrReference(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.SecurityScheme.TypeOidcEntity.RequiredOpenIdConnectUrl value)
        {
            if (value.HasJsonElementBacking)
            {
                return new(value.AsJsonElement);
            }

            return value.ValueKind switch
            {
                JsonValueKind.Object => new(value.AsPropertyBacking()),
                _ => Undefined
            };
        }
    }
}
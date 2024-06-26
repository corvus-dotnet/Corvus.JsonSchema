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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi31;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct ResponseOrReference
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "Content"/>.
            /// </summary>
            public static ReadOnlySpan<byte> ContentUtf8 => "content"u8;

            /// <summary>
            /// JSON property name for <see cref = "Content"/>.
            /// </summary>
            public const string Content = "content";
            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public const string Description = "description";
            /// <summary>
            /// JSON property name for <see cref = "Headers"/>.
            /// </summary>
            public static ReadOnlySpan<byte> HeadersUtf8 => "headers"u8;

            /// <summary>
            /// JSON property name for <see cref = "Headers"/>.
            /// </summary>
            public const string Headers = "headers";
            /// <summary>
            /// JSON property name for <see cref = "Links"/>.
            /// </summary>
            public static ReadOnlySpan<byte> LinksUtf8 => "links"u8;

            /// <summary>
            /// JSON property name for <see cref = "Links"/>.
            /// </summary>
            public const string Links = "links";
            /// <summary>
            /// JSON property name for <see cref = "Ref"/>.
            /// </summary>
            public static ReadOnlySpan<byte> RefUtf8 => "$ref"u8;

            /// <summary>
            /// JSON property name for <see cref = "Ref"/>.
            /// </summary>
            public const string Ref = "$ref";
            /// <summary>
            /// JSON property name for <see cref = "Summary"/>.
            /// </summary>
            public static ReadOnlySpan<byte> SummaryUtf8 => "summary"u8;

            /// <summary>
            /// JSON property name for <see cref = "Summary"/>.
            /// </summary>
            public const string Summary = "summary";
        }

        /// <summary>
        /// Gets the (optional) <c>content</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content Content
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ContentUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Content, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>description</c> property.
        /// </summary>
        public Corvus.Json.JsonString Description
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DescriptionUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Description, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>headers</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity Headers
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.HeadersUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Headers, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>links</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity Links
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.LinksUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Links, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>$ref</c> property.
        /// </summary>
        public Corvus.Json.JsonUriReference Ref
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.RefUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonUriReference(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Ref, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonUriReference>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>summary</c> property.
        /// </summary>
        public Corvus.Json.JsonString Summary
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.SummaryUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Summary, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "ResponseOrReference"/>.
        /// </summary>
        public static ResponseOrReference Create(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content? content = null, Corvus.Json.JsonString? description = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity? headers = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity? links = null, Corvus.Json.JsonUriReference? @ref = null, Corvus.Json.JsonString? summary = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            if (content is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content content__)
            {
                builder.Add(JsonPropertyNames.Content, content__.AsAny);
            }

            if (description is Corvus.Json.JsonString description__)
            {
                builder.Add(JsonPropertyNames.Description, description__.AsAny);
            }

            if (headers is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity headers__)
            {
                builder.Add(JsonPropertyNames.Headers, headers__.AsAny);
            }

            if (links is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity links__)
            {
                builder.Add(JsonPropertyNames.Links, links__.AsAny);
            }

            if (@ref is Corvus.Json.JsonUriReference @ref__)
            {
                builder.Add(JsonPropertyNames.Ref, @ref__.AsAny);
            }

            if (summary is Corvus.Json.JsonString summary__)
            {
                builder.Add(JsonPropertyNames.Summary, summary__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets content.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithContent(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Content value)
        {
            return this.SetProperty(JsonPropertyNames.Content, value);
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets headers.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithHeaders(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.HeadersEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Headers, value);
        }

        /// <summary>
        /// Sets links.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithLinks(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Response.LinksEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Links, value);
        }

        /// <summary>
        /// Sets $ref.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithRef(in Corvus.Json.JsonUriReference value)
        {
            return this.SetProperty(JsonPropertyNames.Ref, value);
        }

        /// <summary>
        /// Sets summary.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ResponseOrReference WithSummary(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Summary, value);
        }

        /// <summary>
        /// Tries to get the validator for the given property.
        /// </summary>
        /// <param name = "property">The property for which to get the validator.</param>
        /// <param name = "hasJsonElementBacking"><c>True</c> if the object containing the property has a JsonElement backing.</param>
        /// <param name = "propertyValidator">The validator for the property, if provided by this schema.</param>
        /// <returns><c>True</c> if the validator was found.</returns>
        private bool __TryGetCorvusLocalPropertiesValidator(in JsonObjectProperty property, bool hasJsonElementBacking, [NotNullWhen(true)] out ObjectPropertyValidator? propertyValidator)
        {
            if (hasJsonElementBacking)
            {
            }
            else
            {
            }

            propertyValidator = null;
            return false;
        }
    }
}
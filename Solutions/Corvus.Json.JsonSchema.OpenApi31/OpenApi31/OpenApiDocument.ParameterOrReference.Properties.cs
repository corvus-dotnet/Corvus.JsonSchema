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
    public readonly partial struct ParameterOrReference
    {
        /// <summary>
        /// The well-known property names in the JSON object.
        /// </summary>
        public static class JsonPropertyNames
        {
            /// <summary>
            /// JSON property name for <see cref = "AllowEmptyValue"/>.
            /// </summary>
            public static ReadOnlySpan<byte> AllowEmptyValueUtf8 => "allowEmptyValue"u8;

            /// <summary>
            /// JSON property name for <see cref = "AllowEmptyValue"/>.
            /// </summary>
            public const string AllowEmptyValue = "allowEmptyValue";
            /// <summary>
            /// JSON property name for <see cref = "AllowReserved"/>.
            /// </summary>
            public static ReadOnlySpan<byte> AllowReservedUtf8 => "allowReserved"u8;

            /// <summary>
            /// JSON property name for <see cref = "AllowReserved"/>.
            /// </summary>
            public const string AllowReserved = "allowReserved";
            /// <summary>
            /// JSON property name for <see cref = "Content"/>.
            /// </summary>
            public static ReadOnlySpan<byte> ContentUtf8 => "content"u8;

            /// <summary>
            /// JSON property name for <see cref = "Content"/>.
            /// </summary>
            public const string Content = "content";
            /// <summary>
            /// JSON property name for <see cref = "Deprecated"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DeprecatedUtf8 => "deprecated"u8;

            /// <summary>
            /// JSON property name for <see cref = "Deprecated"/>.
            /// </summary>
            public const string Deprecated = "deprecated";
            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

            /// <summary>
            /// JSON property name for <see cref = "Description"/>.
            /// </summary>
            public const string Description = "description";
            /// <summary>
            /// JSON property name for <see cref = "Example"/>.
            /// </summary>
            public static ReadOnlySpan<byte> ExampleUtf8 => "example"u8;

            /// <summary>
            /// JSON property name for <see cref = "Example"/>.
            /// </summary>
            public const string Example = "example";
            /// <summary>
            /// JSON property name for <see cref = "Examples"/>.
            /// </summary>
            public static ReadOnlySpan<byte> ExamplesUtf8 => "examples"u8;

            /// <summary>
            /// JSON property name for <see cref = "Examples"/>.
            /// </summary>
            public const string Examples = "examples";
            /// <summary>
            /// JSON property name for <see cref = "Explode"/>.
            /// </summary>
            public static ReadOnlySpan<byte> ExplodeUtf8 => "explode"u8;

            /// <summary>
            /// JSON property name for <see cref = "Explode"/>.
            /// </summary>
            public const string Explode = "explode";
            /// <summary>
            /// JSON property name for <see cref = "In"/>.
            /// </summary>
            public static ReadOnlySpan<byte> InUtf8 => "in"u8;

            /// <summary>
            /// JSON property name for <see cref = "In"/>.
            /// </summary>
            public const string In = "in";
            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public static ReadOnlySpan<byte> NameUtf8 => "name"u8;

            /// <summary>
            /// JSON property name for <see cref = "Name"/>.
            /// </summary>
            public const string Name = "name";
            /// <summary>
            /// JSON property name for <see cref = "Ref"/>.
            /// </summary>
            public static ReadOnlySpan<byte> RefUtf8 => "$ref"u8;

            /// <summary>
            /// JSON property name for <see cref = "Ref"/>.
            /// </summary>
            public const string Ref = "$ref";
            /// <summary>
            /// JSON property name for <see cref = "Required"/>.
            /// </summary>
            public static ReadOnlySpan<byte> RequiredUtf8 => "required"u8;

            /// <summary>
            /// JSON property name for <see cref = "Required"/>.
            /// </summary>
            public const string Required = "required";
            /// <summary>
            /// JSON property name for <see cref = "Schema"/>.
            /// </summary>
            public static ReadOnlySpan<byte> SchemaUtf8 => "schema"u8;

            /// <summary>
            /// JSON property name for <see cref = "Schema"/>.
            /// </summary>
            public const string Schema = "schema";
            /// <summary>
            /// JSON property name for <see cref = "Style"/>.
            /// </summary>
            public static ReadOnlySpan<byte> StyleUtf8 => "style"u8;

            /// <summary>
            /// JSON property name for <see cref = "Style"/>.
            /// </summary>
            public const string Style = "style";
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
        /// Gets the (optional) <c>allowEmptyValue</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity AllowEmptyValue
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.AllowEmptyValueUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowEmptyValue, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>allowReserved</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity AllowReserved
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.AllowReservedUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowReserved, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>content</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity Content
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Content, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>deprecated</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity Deprecated
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DeprecatedUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Deprecated, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity>();
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
        /// Gets the (optional) <c>example</c> property.
        /// </summary>
        public Corvus.Json.JsonAny Example
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ExampleUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonAny(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Example, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonAny>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>examples</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity Examples
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ExamplesUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Examples, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>explode</c> property.
        /// </summary>
        public Corvus.Json.JsonBoolean Explode
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.ExplodeUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonBoolean(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Explode, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonBoolean>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>in</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity In
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.InUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.In, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>name</c> property.
        /// </summary>
        public Corvus.Json.JsonString Name
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.NameUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Name, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
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
        /// Gets the (optional) <c>required</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity Required
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.RequiredUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Required, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>schema</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema Schema
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.SchemaUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Schema, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>style</c> property.
        /// </summary>
        public Corvus.Json.JsonString Style
        {
            get
            {
                if ((this.backing & Backing.JsonElement) != 0)
                {
                    if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.StyleUtf8, out JsonElement result))
                    {
                        return new Corvus.Json.JsonString(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Style, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonString>();
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
        /// Creates an instance of a <see cref = "ParameterOrReference"/>.
        /// </summary>
        public static ParameterOrReference Create(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity? allowEmptyValue = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity? allowReserved = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity? content = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity? deprecated = null, Corvus.Json.JsonString? description = null, Corvus.Json.JsonAny? example = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity? examples = null, Corvus.Json.JsonBoolean? explode = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity? @in = null, Corvus.Json.JsonString? name = null, Corvus.Json.JsonUriReference? @ref = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity? required = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema? schema = null, Corvus.Json.JsonString? style = null, Corvus.Json.JsonString? summary = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            if (allowEmptyValue is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity allowEmptyValue__)
            {
                builder.Add(JsonPropertyNames.AllowEmptyValue, allowEmptyValue__.AsAny);
            }

            if (allowReserved is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity allowReserved__)
            {
                builder.Add(JsonPropertyNames.AllowReserved, allowReserved__.AsAny);
            }

            if (content is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity content__)
            {
                builder.Add(JsonPropertyNames.Content, content__.AsAny);
            }

            if (deprecated is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity deprecated__)
            {
                builder.Add(JsonPropertyNames.Deprecated, deprecated__.AsAny);
            }

            if (description is Corvus.Json.JsonString description__)
            {
                builder.Add(JsonPropertyNames.Description, description__.AsAny);
            }

            if (example is Corvus.Json.JsonAny example__)
            {
                builder.Add(JsonPropertyNames.Example, example__.AsAny);
            }

            if (examples is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity examples__)
            {
                builder.Add(JsonPropertyNames.Examples, examples__.AsAny);
            }

            if (explode is Corvus.Json.JsonBoolean explode__)
            {
                builder.Add(JsonPropertyNames.Explode, explode__.AsAny);
            }

            if (@in is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity @in__)
            {
                builder.Add(JsonPropertyNames.In, @in__.AsAny);
            }

            if (name is Corvus.Json.JsonString name__)
            {
                builder.Add(JsonPropertyNames.Name, name__.AsAny);
            }

            if (@ref is Corvus.Json.JsonUriReference @ref__)
            {
                builder.Add(JsonPropertyNames.Ref, @ref__.AsAny);
            }

            if (required is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity required__)
            {
                builder.Add(JsonPropertyNames.Required, required__.AsAny);
            }

            if (schema is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema schema__)
            {
                builder.Add(JsonPropertyNames.Schema, schema__.AsAny);
            }

            if (style is Corvus.Json.JsonString style__)
            {
                builder.Add(JsonPropertyNames.Style, style__.AsAny);
            }

            if (summary is Corvus.Json.JsonString summary__)
            {
                builder.Add(JsonPropertyNames.Summary, summary__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets allowEmptyValue.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithAllowEmptyValue(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ThenEntity.AllowEmptyValueEntity value)
        {
            return this.SetProperty(JsonPropertyNames.AllowEmptyValue, value);
        }

        /// <summary>
        /// Sets allowReserved.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithAllowReserved(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.SchemaEntity.StylesForQueryEntity.ThenEntity.AllowReservedEntity value)
        {
            return this.SetProperty(JsonPropertyNames.AllowReserved, value);
        }

        /// <summary>
        /// Sets content.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithContent(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.ContentEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Content, value);
        }

        /// <summary>
        /// Sets deprecated.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithDeprecated(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.DeprecatedEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Deprecated, value);
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets example.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithExample(in Corvus.Json.JsonAny value)
        {
            return this.SetProperty(JsonPropertyNames.Example, value);
        }

        /// <summary>
        /// Sets examples.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithExamples(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Examples, value);
        }

        /// <summary>
        /// Sets explode.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithExplode(in Corvus.Json.JsonBoolean value)
        {
            return this.SetProperty(JsonPropertyNames.Explode, value);
        }

        /// <summary>
        /// Sets in.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithIn(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.InEntity value)
        {
            return this.SetProperty(JsonPropertyNames.In, value);
        }

        /// <summary>
        /// Sets name.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithName(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Name, value);
        }

        /// <summary>
        /// Sets $ref.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithRef(in Corvus.Json.JsonUriReference value)
        {
            return this.SetProperty(JsonPropertyNames.Ref, value);
        }

        /// <summary>
        /// Sets required.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithRequired(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Parameter.RequiredEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Required, value);
        }

        /// <summary>
        /// Sets schema.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithSchema(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema value)
        {
            return this.SetProperty(JsonPropertyNames.Schema, value);
        }

        /// <summary>
        /// Sets style.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithStyle(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Style, value);
        }

        /// <summary>
        /// Sets summary.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public ParameterOrReference WithSummary(in Corvus.Json.JsonString value)
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
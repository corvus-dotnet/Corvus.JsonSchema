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
    public readonly partial struct Header
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
        }

        /// <summary>
        /// Gets the (optional) <c>content</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity Content
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Content, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>deprecated</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity Deprecated
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Deprecated, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity>();
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
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity Explode
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Explode, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>required</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity Required
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Required, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity>();
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
        public Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.StyleEntity Style
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
                        return new Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.StyleEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Style, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.StyleEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "Header"/>.
        /// </summary>
        public static Header Create(Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity? content = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity? deprecated = null, Corvus.Json.JsonString? description = null, Corvus.Json.JsonAny? example = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity? examples = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity? explode = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity? required = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema? schema = null, Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.StyleEntity? style = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            if (content is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity content__)
            {
                builder.Add(JsonPropertyNames.Content, content__.AsAny);
            }

            if (deprecated is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity deprecated__)
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

            if (explode is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity explode__)
            {
                builder.Add(JsonPropertyNames.Explode, explode__.AsAny);
            }

            if (required is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity required__)
            {
                builder.Add(JsonPropertyNames.Required, required__.AsAny);
            }

            if (schema is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema schema__)
            {
                builder.Add(JsonPropertyNames.Schema, schema__.AsAny);
            }

            if (style is Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.StyleEntity style__)
            {
                builder.Add(JsonPropertyNames.Style, style__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets content.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithContent(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Content, value);
        }

        /// <summary>
        /// Sets deprecated.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithDeprecated(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Deprecated, value);
        }

        /// <summary>
        /// Sets description.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithDescription(in Corvus.Json.JsonString value)
        {
            return this.SetProperty(JsonPropertyNames.Description, value);
        }

        /// <summary>
        /// Sets example.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithExample(in Corvus.Json.JsonAny value)
        {
            return this.SetProperty(JsonPropertyNames.Example, value);
        }

        /// <summary>
        /// Sets examples.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithExamples(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Examples.ExamplesEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Examples, value);
        }

        /// <summary>
        /// Sets explode.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithExplode(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.WithStyleSimple.ExplodeEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Explode, value);
        }

        /// <summary>
        /// Sets required.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithRequired(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Required, value);
        }

        /// <summary>
        /// Sets schema.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithSchema(in Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema value)
        {
            return this.SetProperty(JsonPropertyNames.Schema, value);
        }

        private static ValidationContext __CorvusValidateDescription(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateRequired(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.RequiredEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateDeprecated(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.DeprecatedEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateSchema(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Schema>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateContent(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi31.OpenApiDocument.Header.ContentEntity>().Validate(validationContext, level);
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
                if (property.NameEquals(JsonPropertyNames.DescriptionUtf8))
                {
                    propertyValidator = __CorvusValidateDescription;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.RequiredUtf8))
                {
                    propertyValidator = __CorvusValidateRequired;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.DeprecatedUtf8))
                {
                    propertyValidator = __CorvusValidateDeprecated;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.SchemaUtf8))
                {
                    propertyValidator = __CorvusValidateSchema;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.ContentUtf8))
                {
                    propertyValidator = __CorvusValidateContent;
                    return true;
                }
            }
            else
            {
                if (property.NameEquals(JsonPropertyNames.Description))
                {
                    propertyValidator = __CorvusValidateDescription;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Required))
                {
                    propertyValidator = __CorvusValidateRequired;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Deprecated))
                {
                    propertyValidator = __CorvusValidateDeprecated;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Schema))
                {
                    propertyValidator = __CorvusValidateSchema;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Content))
                {
                    propertyValidator = __CorvusValidateContent;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }
    }
}
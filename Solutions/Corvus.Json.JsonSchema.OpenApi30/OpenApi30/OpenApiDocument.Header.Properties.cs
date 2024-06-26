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

namespace Corvus.Json.JsonSchema.OpenApi30;
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
        /// Gets the (optional) <c>allowEmptyValue</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity AllowEmptyValue
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowEmptyValue, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>allowReserved</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity AllowReserved
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.AllowReserved, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>content</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity Content
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Content, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>deprecated</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity Deprecated
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Deprecated, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity>();
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
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity Examples
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Examples, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity>();
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
        /// Gets the (optional) <c>required</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity Required
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Required, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>schema</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity Schema
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Schema, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the (optional) <c>style</c> property.
        /// </summary>
        public Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity Style
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
                        return new Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity(result);
                    }
                }

                if ((this.backing & Backing.Object) != 0)
                {
                    if (this.objectBacking.TryGetValue(JsonPropertyNames.Style, out JsonAny result))
                    {
                        return result.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity>();
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Creates an instance of a <see cref = "Header"/>.
        /// </summary>
        public static Header Create(Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity? allowEmptyValue = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity? allowReserved = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity? content = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity? deprecated = null, Corvus.Json.JsonString? description = null, Corvus.Json.JsonAny? example = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity? examples = null, Corvus.Json.JsonBoolean? explode = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity? required = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity? schema = null, Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity? style = null)
        {
            var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
            if (allowEmptyValue is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity allowEmptyValue__)
            {
                builder.Add(JsonPropertyNames.AllowEmptyValue, allowEmptyValue__.AsAny);
            }

            if (allowReserved is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity allowReserved__)
            {
                builder.Add(JsonPropertyNames.AllowReserved, allowReserved__.AsAny);
            }

            if (content is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity content__)
            {
                builder.Add(JsonPropertyNames.Content, content__.AsAny);
            }

            if (deprecated is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity deprecated__)
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

            if (examples is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity examples__)
            {
                builder.Add(JsonPropertyNames.Examples, examples__.AsAny);
            }

            if (explode is Corvus.Json.JsonBoolean explode__)
            {
                builder.Add(JsonPropertyNames.Explode, explode__.AsAny);
            }

            if (required is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity required__)
            {
                builder.Add(JsonPropertyNames.Required, required__.AsAny);
            }

            if (schema is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity schema__)
            {
                builder.Add(JsonPropertyNames.Schema, schema__.AsAny);
            }

            if (style is Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity style__)
            {
                builder.Add(JsonPropertyNames.Style, style__.AsAny);
            }

            return new(builder.ToImmutable());
        }

        /// <summary>
        /// Sets allowEmptyValue.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithAllowEmptyValue(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity value)
        {
            return this.SetProperty(JsonPropertyNames.AllowEmptyValue, value);
        }

        /// <summary>
        /// Sets allowReserved.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithAllowReserved(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity value)
        {
            return this.SetProperty(JsonPropertyNames.AllowReserved, value);
        }

        /// <summary>
        /// Sets content.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithContent(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Content, value);
        }

        /// <summary>
        /// Sets deprecated.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithDeprecated(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity value)
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
        public Header WithExamples(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Examples, value);
        }

        /// <summary>
        /// Sets explode.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithExplode(in Corvus.Json.JsonBoolean value)
        {
            return this.SetProperty(JsonPropertyNames.Explode, value);
        }

        /// <summary>
        /// Sets required.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithRequired(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Required, value);
        }

        /// <summary>
        /// Sets schema.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithSchema(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Schema, value);
        }

        /// <summary>
        /// Sets style.
        /// </summary>
        /// <param name = "value">The value to set.</param>
        /// <returns>The entity with the updated property.</returns>
        public Header WithStyle(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity value)
        {
            return this.SetProperty(JsonPropertyNames.Style, value);
        }

        private static ValidationContext __CorvusValidateDescription(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateRequired(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.RequiredEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateDeprecated(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.DeprecatedEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateAllowEmptyValue(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowEmptyValueEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateStyle(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.StyleEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateExplode(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonBoolean>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateAllowReserved(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.AllowReservedEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateSchema(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.SchemaEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateContent(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ContentEntity>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateExample(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonAny>().Validate(validationContext, level);
        }

        private static ValidationContext __CorvusValidateExamples(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
        {
            return property.ValueAs<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Header.ExamplesEntity>().Validate(validationContext, level);
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
                else if (property.NameEquals(JsonPropertyNames.AllowEmptyValueUtf8))
                {
                    propertyValidator = __CorvusValidateAllowEmptyValue;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.StyleUtf8))
                {
                    propertyValidator = __CorvusValidateStyle;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.ExplodeUtf8))
                {
                    propertyValidator = __CorvusValidateExplode;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.AllowReservedUtf8))
                {
                    propertyValidator = __CorvusValidateAllowReserved;
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
                else if (property.NameEquals(JsonPropertyNames.ExampleUtf8))
                {
                    propertyValidator = __CorvusValidateExample;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.ExamplesUtf8))
                {
                    propertyValidator = __CorvusValidateExamples;
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
                else if (property.NameEquals(JsonPropertyNames.AllowEmptyValue))
                {
                    propertyValidator = __CorvusValidateAllowEmptyValue;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Style))
                {
                    propertyValidator = __CorvusValidateStyle;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Explode))
                {
                    propertyValidator = __CorvusValidateExplode;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.AllowReserved))
                {
                    propertyValidator = __CorvusValidateAllowReserved;
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
                else if (property.NameEquals(JsonPropertyNames.Example))
                {
                    propertyValidator = __CorvusValidateExample;
                    return true;
                }
                else if (property.NameEquals(JsonPropertyNames.Examples))
                {
                    propertyValidator = __CorvusValidateExamples;
                    return true;
                }
            }

            propertyValidator = null;
            return false;
        }
    }
}
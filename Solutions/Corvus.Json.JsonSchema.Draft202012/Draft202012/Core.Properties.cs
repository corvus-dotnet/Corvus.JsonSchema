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

namespace Corvus.Json.JsonSchema.Draft202012;
/// <summary>
/// Core vocabulary meta-schema
/// </summary>
public readonly partial struct Core
{
    /// <summary>
    /// The well-known property names in the JSON object.
    /// </summary>
    public static class JsonPropertyNames
    {
        /// <summary>
        /// JSON property name for <see cref = "Anchor"/>.
        /// </summary>
        public static ReadOnlySpan<byte> AnchorUtf8 => "$anchor"u8;

        /// <summary>
        /// JSON property name for <see cref = "Anchor"/>.
        /// </summary>
        public const string Anchor = "$anchor";
        /// <summary>
        /// JSON property name for <see cref = "Comment"/>.
        /// </summary>
        public static ReadOnlySpan<byte> CommentUtf8 => "$comment"u8;

        /// <summary>
        /// JSON property name for <see cref = "Comment"/>.
        /// </summary>
        public const string Comment = "$comment";
        /// <summary>
        /// JSON property name for <see cref = "Defs"/>.
        /// </summary>
        public static ReadOnlySpan<byte> DefsUtf8 => "$defs"u8;

        /// <summary>
        /// JSON property name for <see cref = "Defs"/>.
        /// </summary>
        public const string Defs = "$defs";
        /// <summary>
        /// JSON property name for <see cref = "DynamicAnchor"/>.
        /// </summary>
        public static ReadOnlySpan<byte> DynamicAnchorUtf8 => "$dynamicAnchor"u8;

        /// <summary>
        /// JSON property name for <see cref = "DynamicAnchor"/>.
        /// </summary>
        public const string DynamicAnchor = "$dynamicAnchor";
        /// <summary>
        /// JSON property name for <see cref = "DynamicRef"/>.
        /// </summary>
        public static ReadOnlySpan<byte> DynamicRefUtf8 => "$dynamicRef"u8;

        /// <summary>
        /// JSON property name for <see cref = "DynamicRef"/>.
        /// </summary>
        public const string DynamicRef = "$dynamicRef";
        /// <summary>
        /// JSON property name for <see cref = "Id"/>.
        /// </summary>
        public static ReadOnlySpan<byte> IdUtf8 => "$id"u8;

        /// <summary>
        /// JSON property name for <see cref = "Id"/>.
        /// </summary>
        public const string Id = "$id";
        /// <summary>
        /// JSON property name for <see cref = "Ref"/>.
        /// </summary>
        public static ReadOnlySpan<byte> RefUtf8 => "$ref"u8;

        /// <summary>
        /// JSON property name for <see cref = "Ref"/>.
        /// </summary>
        public const string Ref = "$ref";
        /// <summary>
        /// JSON property name for <see cref = "Schema"/>.
        /// </summary>
        public static ReadOnlySpan<byte> SchemaUtf8 => "$schema"u8;

        /// <summary>
        /// JSON property name for <see cref = "Schema"/>.
        /// </summary>
        public const string Schema = "$schema";
        /// <summary>
        /// JSON property name for <see cref = "Vocabulary"/>.
        /// </summary>
        public static ReadOnlySpan<byte> VocabularyUtf8 => "$vocabulary"u8;

        /// <summary>
        /// JSON property name for <see cref = "Vocabulary"/>.
        /// </summary>
        public const string Vocabulary = "$vocabulary";
    }

    /// <summary>
    /// Gets the (optional) <c>$anchor</c> property.
    /// </summary>
    public Corvus.Json.JsonSchema.Draft202012.Core.AnchorString Anchor
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.AnchorUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonSchema.Draft202012.Core.AnchorString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Anchor, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonSchema.Draft202012.Core.AnchorString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$comment</c> property.
    /// </summary>
    public Corvus.Json.JsonString Comment
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.CommentUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Comment, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$defs</c> property.
    /// </summary>
    public Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity Defs
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DefsUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Defs, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$dynamicAnchor</c> property.
    /// </summary>
    public Corvus.Json.JsonSchema.Draft202012.Core.AnchorString DynamicAnchor
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DynamicAnchorUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonSchema.Draft202012.Core.AnchorString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.DynamicAnchor, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonSchema.Draft202012.Core.AnchorString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$dynamicRef</c> property.
    /// </summary>
    public Corvus.Json.JsonUriReference DynamicRef
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.DynamicRefUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonUriReference(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.DynamicRef, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonUriReference>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$id</c> property.
    /// </summary>
    public Corvus.Json.JsonSchema.Draft202012.Core.IdEntity Id
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.IdUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonSchema.Draft202012.Core.IdEntity(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Id, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonSchema.Draft202012.Core.IdEntity>();
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
    /// Gets the (optional) <c>$schema</c> property.
    /// </summary>
    public Corvus.Json.JsonUri Schema
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
                    return new Corvus.Json.JsonUri(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Schema, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonUri>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>$vocabulary</c> property.
    /// </summary>
    public Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity Vocabulary
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.VocabularyUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Vocabulary, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Creates an instance of a <see cref = "Core"/>.
    /// </summary>
    public static Core Create(Corvus.Json.JsonSchema.Draft202012.Core.AnchorString? anchor = null, Corvus.Json.JsonString? comment = null, Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity? defs = null, Corvus.Json.JsonSchema.Draft202012.Core.AnchorString? dynamicAnchor = null, Corvus.Json.JsonUriReference? dynamicRef = null, Corvus.Json.JsonSchema.Draft202012.Core.IdEntity? id = null, Corvus.Json.JsonUriReference? @ref = null, Corvus.Json.JsonUri? schema = null, Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity? vocabulary = null)
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        if (anchor is Corvus.Json.JsonSchema.Draft202012.Core.AnchorString anchor__)
        {
            builder.Add(JsonPropertyNames.Anchor, anchor__.AsAny);
        }

        if (comment is Corvus.Json.JsonString comment__)
        {
            builder.Add(JsonPropertyNames.Comment, comment__.AsAny);
        }

        if (defs is Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity defs__)
        {
            builder.Add(JsonPropertyNames.Defs, defs__.AsAny);
        }

        if (dynamicAnchor is Corvus.Json.JsonSchema.Draft202012.Core.AnchorString dynamicAnchor__)
        {
            builder.Add(JsonPropertyNames.DynamicAnchor, dynamicAnchor__.AsAny);
        }

        if (dynamicRef is Corvus.Json.JsonUriReference dynamicRef__)
        {
            builder.Add(JsonPropertyNames.DynamicRef, dynamicRef__.AsAny);
        }

        if (id is Corvus.Json.JsonSchema.Draft202012.Core.IdEntity id__)
        {
            builder.Add(JsonPropertyNames.Id, id__.AsAny);
        }

        if (@ref is Corvus.Json.JsonUriReference @ref__)
        {
            builder.Add(JsonPropertyNames.Ref, @ref__.AsAny);
        }

        if (schema is Corvus.Json.JsonUri schema__)
        {
            builder.Add(JsonPropertyNames.Schema, schema__.AsAny);
        }

        if (vocabulary is Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity vocabulary__)
        {
            builder.Add(JsonPropertyNames.Vocabulary, vocabulary__.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Sets $anchor.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithAnchor(in Corvus.Json.JsonSchema.Draft202012.Core.AnchorString value)
    {
        return this.SetProperty(JsonPropertyNames.Anchor, value);
    }

    /// <summary>
    /// Sets $comment.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithComment(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.Comment, value);
    }

    /// <summary>
    /// Sets $defs.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithDefs(in Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity value)
    {
        return this.SetProperty(JsonPropertyNames.Defs, value);
    }

    /// <summary>
    /// Sets $dynamicAnchor.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithDynamicAnchor(in Corvus.Json.JsonSchema.Draft202012.Core.AnchorString value)
    {
        return this.SetProperty(JsonPropertyNames.DynamicAnchor, value);
    }

    /// <summary>
    /// Sets $dynamicRef.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithDynamicRef(in Corvus.Json.JsonUriReference value)
    {
        return this.SetProperty(JsonPropertyNames.DynamicRef, value);
    }

    /// <summary>
    /// Sets $id.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithId(in Corvus.Json.JsonSchema.Draft202012.Core.IdEntity value)
    {
        return this.SetProperty(JsonPropertyNames.Id, value);
    }

    /// <summary>
    /// Sets $ref.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithRef(in Corvus.Json.JsonUriReference value)
    {
        return this.SetProperty(JsonPropertyNames.Ref, value);
    }

    /// <summary>
    /// Sets $schema.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithSchema(in Corvus.Json.JsonUri value)
    {
        return this.SetProperty(JsonPropertyNames.Schema, value);
    }

    /// <summary>
    /// Sets $vocabulary.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Core WithVocabulary(in Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity value)
    {
        return this.SetProperty(JsonPropertyNames.Vocabulary, value);
    }

    private static ValidationContext __CorvusValidateId(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonSchema.Draft202012.Core.IdEntity>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateSchema(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonUri>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateRef(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonUriReference>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateAnchor(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonSchema.Draft202012.Core.AnchorString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateDynamicRef(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonUriReference>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateDynamicAnchor(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonSchema.Draft202012.Core.AnchorString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateVocabulary(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonSchema.Draft202012.Core.VocabularyEntity>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateComment(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateDefs(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonSchema.Draft202012.Core.DefsEntity>().Validate(validationContext, level);
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
            if (property.NameEquals(JsonPropertyNames.IdUtf8))
            {
                propertyValidator = __CorvusValidateId;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.SchemaUtf8))
            {
                propertyValidator = __CorvusValidateSchema;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.RefUtf8))
            {
                propertyValidator = __CorvusValidateRef;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.AnchorUtf8))
            {
                propertyValidator = __CorvusValidateAnchor;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.DynamicRefUtf8))
            {
                propertyValidator = __CorvusValidateDynamicRef;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.DynamicAnchorUtf8))
            {
                propertyValidator = __CorvusValidateDynamicAnchor;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.VocabularyUtf8))
            {
                propertyValidator = __CorvusValidateVocabulary;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.CommentUtf8))
            {
                propertyValidator = __CorvusValidateComment;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.DefsUtf8))
            {
                propertyValidator = __CorvusValidateDefs;
                return true;
            }
        }
        else
        {
            if (property.NameEquals(JsonPropertyNames.Id))
            {
                propertyValidator = __CorvusValidateId;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Schema))
            {
                propertyValidator = __CorvusValidateSchema;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Ref))
            {
                propertyValidator = __CorvusValidateRef;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Anchor))
            {
                propertyValidator = __CorvusValidateAnchor;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.DynamicRef))
            {
                propertyValidator = __CorvusValidateDynamicRef;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.DynamicAnchor))
            {
                propertyValidator = __CorvusValidateDynamicAnchor;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Vocabulary))
            {
                propertyValidator = __CorvusValidateVocabulary;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Comment))
            {
                propertyValidator = __CorvusValidateComment;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Defs))
            {
                propertyValidator = __CorvusValidateDefs;
                return true;
            }
        }

        propertyValidator = null;
        return false;
    }
}
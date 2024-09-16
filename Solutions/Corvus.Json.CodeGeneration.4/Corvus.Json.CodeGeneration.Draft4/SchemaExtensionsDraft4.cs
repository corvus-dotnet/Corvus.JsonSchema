// <copyright file="SchemaExtensionsDraft4.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.Draft4;

namespace Corvus.Json.CodeGeneration.Draft4;

/// <summary>
/// Extension methods for Draft4-related schema types.
/// </summary>
public static class SchemaExtensionsDraft4
{
    /// <summary>
    /// Gets the given type declaration's schema as a draft-6 schema instance.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the schema.</param>
    /// <returns>The schema as a draft-6 instance.</returns>
    public static Schema Schema(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.LocatedSchema.Schema.As<Schema>();
    }

    /// <summary>
    /// Format the documentation for the type.
    /// </summary>
    /// <param name="typeDeclaration">The type for which to format documentation.</param>
    /// <returns>The class-level documentation for the type.</returns>
    public static string FormatTypeDocumentation(this TypeDeclaration typeDeclaration)
    {
        StringBuilder documentation = new();
        Schema schema = typeDeclaration.Schema();
        documentation.AppendLine("/// <summary>");

        if (schema.Title.IsNotNullOrUndefined())
        {
            documentation.Append("/// ");
            documentation.AppendLine(Formatting.FormatLiteralOrNull(schema.Title.GetString(), false));
        }
        else
        {
            documentation.AppendLine("/// Generated from JSON Schema.");
        }

        documentation.AppendLine("/// </summary>");

        if (schema.Description.IsNotNullOrUndefined())
        {
            documentation.AppendLine("/// <remarks>");

            if (schema.Description.IsNotNullOrUndefined())
            {
                // Unescaped new lines in the string value.
#if NET8_0_OR_GREATER
                string[]? lines = schema.Description.GetString()?.Split("\n");
#else
                string[]? lines = schema.Description.GetString()?.Split('\n');
#endif
                if (lines is string[] l)
                {
                    foreach (string line in l)
                    {
                        documentation.AppendLine("/// <para>");
                        documentation.Append("/// ");
                        documentation.AppendLine(Formatting.FormatLiteralOrNull(line, false));
                        documentation.AppendLine("/// </para>");
                    }
                }
            }

            documentation.AppendLine("/// </remarks>");
        }

        return documentation.ToString();
    }

    /// <summary>
    /// Determines if this schema is empty of known items, but contains unknown extensions.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema draft4Schema)
    {
        return draft4Schema.ValueKind == JsonValueKind.Object && IsEmpty(draft4Schema) && draft4Schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Array);
    }

    /// <summary>
    /// Gets the content media type for the draft 6 schema.
    /// </summary>
    /// <param name="draft4Schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentMediaType(this Schema draft4Schema)
    {
        if (draft4Schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
        {
            if (media.AsObject.TryGetProperty("type", out JsonAny type) && type.ValueKind == JsonValueKind.String)
            {
                return type.AsString;
            }
        }

        return JsonString.Undefined;
    }

    /// <summary>
    /// Gets the content encoding for the draft 6 schema.
    /// </summary>
    /// <param name="draft4Schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentEncoding(this Schema draft4Schema)
    {
        if (draft4Schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
        {
            if (media.AsObject.TryGetProperty("binaryEncoding", out JsonAny encoding) && encoding.ValueKind == JsonValueKind.String)
            {
                return encoding.AsString;
            }
        }

        return JsonString.Undefined;
    }

    /// <summary>
    /// Determines if this is an explicit object type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && (draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Number) || draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit null type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitNullType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Null);
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitObjectType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Object))) || draft4Schema.Properties.IsNotUndefined() || draft4Schema.Required.IsNotUndefined() || draft4Schema.AdditionalProperties.IsNotUndefined() || draft4Schema.MaxProperties.IsNotUndefined() || draft4Schema.MinProperties.IsNotUndefined() || draft4Schema.PatternProperties.IsNotUndefined() || draft4Schema.Dependencies.IsNotUndefined() || draft4Schema.HasObjectEnum() || draft4Schema.HasObjectConst();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitArrayType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Array))) || draft4Schema.AdditionalItems.IsNotUndefined() || draft4Schema.Items.IsNotUndefined() || draft4Schema.MaxItems.IsNotUndefined() || draft4Schema.MinItems.IsNotUndefined() || draft4Schema.UniqueItems.IsNotUndefined() || draft4Schema.HasArrayEnum() || draft4Schema.HasArrayConst();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitNumberType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Number) || type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Integer))) || draft4Schema.Minimum.IsNotUndefined() || draft4Schema.Maximum.IsNotUndefined() || draft4Schema.ExclusiveMaximum.IsNotUndefined() || draft4Schema.ExclusiveMinimum.IsNotUndefined() || draft4Schema.MultipleOf.IsNotUndefined() || draft4Schema.HasNumberEnum() || draft4Schema.HasNumberConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitBooleanType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Boolean))) || draft4Schema.HasBooleanEnum() || draft4Schema.HasBooleanConst();
    }

    /// <summary>
    /// Determines if this can be a null type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNullType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitNullType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Null))) || draft4Schema.HasNullEnum() || draft4Schema.HasNullConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema draft4Schema)
    {
        return
            draft4Schema.IsExplicitStringType() || (draft4Schema.Type.IsSimpleTypesArray && draft4Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.String))) || draft4Schema.MinLength.IsNotUndefined() || draft4Schema.MaxLength.IsNotUndefined() || draft4Schema.Pattern.IsNotUndefined() || draft4Schema.HasStringEnum() || draft4Schema.HasStringConst();
    }

    /// <summary>
    /// Determines if this is an integer type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsJsonInteger(this Schema draft4Schema)
    {
        return
            draft4Schema.Type.IsSimpleTypes && (
                draft4Schema.Type.Equals(JsonSchema.Draft4.Schema.SimpleTypes.EnumValues.Integer) ||
                (draft4Schema.Format.IsNullOrUndefined() && draft4Schema.Format == "integer"));
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an null enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNullEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema draft4Schema)
    {
        return draft4Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft4Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Gets a value indicating whether is has an object const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasObjectConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether is has an array const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasArrayConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether is has a number const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNumberConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether is has a null const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNullConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean const type.
    /// </summary>
    /// <param name="draft4Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasBooleanConst(this Schema draft4Schema)
    {
        return false;
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema draft4Schema)
    {
        return draft4Schema.Type.IsSimpleTypes;
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema draft4Schema)
    {
        return
            draft4Schema.ValueKind == JsonValueKind.True ||
            draft4Schema.ValueKind == JsonValueKind.False ||
            draft4Schema.IsEmpty() ||
            (draft4Schema.IsSimpleType() &&
            draft4Schema.AdditionalItems.IsUndefined() &&
            draft4Schema.AdditionalProperties.IsUndefined() &&
            draft4Schema.AllOf.IsUndefined() &&
            draft4Schema.AnyOf.IsUndefined() &&
            draft4Schema.Default.IsUndefined() &&
            draft4Schema.Dependencies.IsUndefined() &&
            draft4Schema.Enum.IsUndefined() &&
            draft4Schema.ExclusiveMaximum.IsUndefined() &&
            draft4Schema.ExclusiveMinimum.IsUndefined() &&
            draft4Schema.Items.IsUndefined() &&
            draft4Schema.Maximum.IsUndefined() &&
            draft4Schema.MaxItems.IsUndefined() &&
            draft4Schema.MaxLength.IsUndefined() &&
            draft4Schema.MaxProperties.IsUndefined() &&
            draft4Schema.Minimum.IsUndefined() &&
            draft4Schema.MinItems.IsUndefined() &&
            draft4Schema.MinLength.IsUndefined() &&
            draft4Schema.MinProperties.IsUndefined() &&
            draft4Schema.MultipleOf.IsUndefined() &&
            draft4Schema.Not.IsUndefined() &&
            draft4Schema.OneOf.IsUndefined() &&
            draft4Schema.Pattern.IsUndefined() &&
            draft4Schema.PatternProperties.IsUndefined() &&
            draft4Schema.Properties.IsUndefined() &&
            draft4Schema.Required.IsUndefined() &&
            draft4Schema.UniqueItems.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is a primitive type.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value and no format value.</returns>
    public static bool IsBuiltInPrimitiveType(this Schema draft4Schema)
    {
        return draft4Schema.IsBuiltInType() && draft4Schema.Format.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema draft4Schema)
    {
        return
            draft4Schema.AdditionalItems.IsUndefined() &&
            draft4Schema.AdditionalProperties.IsUndefined() &&
            draft4Schema.AllOf.IsUndefined() &&
            draft4Schema.AnyOf.IsUndefined() &&
            draft4Schema.GetContentEncoding().IsUndefined() &&
            draft4Schema.GetContentMediaType().IsUndefined() &&
            draft4Schema.Default.IsUndefined() &&
            draft4Schema.Definitions.IsUndefined() &&
            draft4Schema.Dependencies.IsUndefined() &&
            draft4Schema.Description.IsUndefined() &&
            draft4Schema.Enum.IsUndefined() &&
            draft4Schema.ExclusiveMaximum.IsUndefined() &&
            draft4Schema.ExclusiveMinimum.IsUndefined() &&
            draft4Schema.Format.IsUndefined() &&
            draft4Schema.Items.IsUndefined() &&
            draft4Schema.Maximum.IsUndefined() &&
            draft4Schema.MaxItems.IsUndefined() &&
            draft4Schema.MaxLength.IsUndefined() &&
            draft4Schema.MaxProperties.IsUndefined() &&
            draft4Schema.Minimum.IsUndefined() &&
            draft4Schema.MinItems.IsUndefined() &&
            draft4Schema.MinLength.IsUndefined() &&
            draft4Schema.MinProperties.IsUndefined() &&
            draft4Schema.MultipleOf.IsUndefined() &&
            draft4Schema.Not.IsUndefined() &&
            draft4Schema.OneOf.IsUndefined() &&
            draft4Schema.Pattern.IsUndefined() &&
            draft4Schema.PatternProperties.IsUndefined() &&
            draft4Schema.Properties.IsUndefined() &&
            draft4Schema.Required.IsUndefined() &&
            draft4Schema.Title.IsUndefined() &&
            draft4Schema.Type.IsUndefined() &&
            draft4Schema.UniqueItems.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked oneOf.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a oneOf and no other substantive properties.</returns>
    public static bool IsNakedOneOf(this Schema draft4Schema)
    {
        return
            draft4Schema.OneOf.IsNotUndefined() &&
            draft4Schema.AdditionalItems.IsUndefined() &&
            draft4Schema.AdditionalProperties.IsUndefined() &&
            draft4Schema.AllOf.IsUndefined() &&
            draft4Schema.AnyOf.IsUndefined() &&
            draft4Schema.GetContentEncoding().IsUndefined() &&
            draft4Schema.GetContentMediaType().IsUndefined() &&
            draft4Schema.Default.IsUndefined() &&
            draft4Schema.Definitions.IsUndefined() &&
            draft4Schema.Dependencies.IsUndefined() &&
            draft4Schema.Description.IsUndefined() &&
            draft4Schema.Enum.IsUndefined() &&
            draft4Schema.ExclusiveMaximum.IsUndefined() &&
            draft4Schema.ExclusiveMinimum.IsUndefined() &&
            draft4Schema.Format.IsUndefined() &&
            draft4Schema.Items.IsUndefined() &&
            draft4Schema.Maximum.IsUndefined() &&
            draft4Schema.MaxItems.IsUndefined() &&
            draft4Schema.MaxLength.IsUndefined() &&
            draft4Schema.MaxProperties.IsUndefined() &&
            draft4Schema.Minimum.IsUndefined() &&
            draft4Schema.MinItems.IsUndefined() &&
            draft4Schema.MinLength.IsUndefined() &&
            draft4Schema.MinProperties.IsUndefined() &&
            draft4Schema.MultipleOf.IsUndefined() &&
            draft4Schema.Not.IsUndefined() &&
            draft4Schema.Pattern.IsUndefined() &&
            draft4Schema.PatternProperties.IsUndefined() &&
            draft4Schema.Properties.IsUndefined() &&
            draft4Schema.Required.IsUndefined() &&
            draft4Schema.Title.IsUndefined() &&
            draft4Schema.Type.IsUndefined() &&
            draft4Schema.UniqueItems.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked reference.
    /// </summary>
    /// <param name="draft4Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $ref and no other substantive properties.</returns>
    public static bool IsNakedReference(this Schema draft4Schema)
    {
        // If we have a reference, we are always naked.
        return
            true;
    }
}
// <copyright file="SchemaExtensionsDraft6.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.Draft6;

namespace Corvus.Json.CodeGeneration.Draft6;

/// <summary>
/// Extension methods for Draft6-related schema types.
/// </summary>
public static class SchemaExtensionsDraft6
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

        if (schema.Description.IsNotNullOrUndefined() || schema.Examples.IsNotNullOrUndefined())
        {
            documentation.AppendLine("/// <remarks>");

            if (schema.Description.IsNotNullOrUndefined())
            {
                // Unescaped new lines in the string value.
                string[]? lines = schema.Description.GetString()?.Split("\n");
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

            if (schema.Examples.IsNotNullOrUndefined())
            {
                documentation.AppendLine("/// <para>");
                documentation.AppendLine("/// Examples:");
                foreach (JsonAny example in schema.Examples.EnumerateArray())
                {
                    documentation.AppendLine("/// <example>");
                    documentation.AppendLine("/// <code>");
                    string[] lines = example.ToString().Split("\\n");
                    foreach (string line in lines)
                    {
                        documentation.Append("/// ");
                        documentation.AppendLine(Formatting.FormatLiteralOrNull(line, false));
                    }

                    documentation.AppendLine("/// </code>");
                    documentation.AppendLine("/// </example>");
                }

                documentation.AppendLine("/// </para>");
            }

            documentation.AppendLine("/// </remarks>");
        }

        return documentation.ToString();
    }

    /// <summary>
    /// Determines if this schema is empty of known items, but contains unknown extensions.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema draft6Schema)
    {
        return draft6Schema.ValueKind == JsonValueKind.Object && IsEmpty(draft6Schema) && draft6Schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Array);
    }

    /// <summary>
    /// Gets the content media type for the draft 6 schema.
    /// </summary>
    /// <param name="draft6Schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentMediaType(this Schema draft6Schema)
    {
        if (draft6Schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
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
    /// <param name="draft6Schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentEncoding(this Schema draft6Schema)
    {
        if (draft6Schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
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
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && (draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Number) || draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit null type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitNullType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Null);
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitObjectType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Object))) || draft6Schema.Properties.IsNotUndefined() || draft6Schema.Required.IsNotUndefined() || draft6Schema.AdditionalProperties.IsNotUndefined() || draft6Schema.MaxProperties.IsNotUndefined() || draft6Schema.MinProperties.IsNotUndefined() || draft6Schema.PatternProperties.IsNotUndefined() || draft6Schema.PropertyNames.IsNotUndefined() || draft6Schema.Dependencies.IsNotUndefined() || draft6Schema.HasObjectEnum() || draft6Schema.HasObjectConst();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitArrayType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Array))) || draft6Schema.AdditionalItems.IsNotUndefined() || draft6Schema.Contains.IsNotUndefined() || draft6Schema.Items.IsNotUndefined() || draft6Schema.MaxItems.IsNotUndefined() || draft6Schema.MinItems.IsNotUndefined() || draft6Schema.UniqueItems.IsNotUndefined() || draft6Schema.HasArrayEnum() || draft6Schema.HasArrayConst();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitNumberType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Number) || type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Integer))) || draft6Schema.Minimum.IsNotUndefined() || draft6Schema.Maximum.IsNotUndefined() || draft6Schema.ExclusiveMaximum.IsNotUndefined() || draft6Schema.ExclusiveMinimum.IsNotUndefined() || draft6Schema.MultipleOf.IsNotUndefined() || draft6Schema.HasNumberEnum() || draft6Schema.HasNumberConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitBooleanType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Boolean))) || draft6Schema.HasBooleanEnum() || draft6Schema.HasBooleanConst();
    }

    /// <summary>
    /// Determines if this can be a null type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNullType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitNullType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Null))) || draft6Schema.HasNullEnum() || draft6Schema.HasNullConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema draft6Schema)
    {
        return
            draft6Schema.IsExplicitStringType() || (draft6Schema.Type.IsSimpleTypesArray && draft6Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.String))) || draft6Schema.MinLength.IsNotUndefined() || draft6Schema.MaxLength.IsNotUndefined() || draft6Schema.Pattern.IsNotUndefined() || draft6Schema.HasStringEnum() || draft6Schema.HasStringConst();
    }

    /// <summary>
    /// Determines if this is an integer type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsJsonInteger(this Schema draft6Schema)
    {
        return
            draft6Schema.Type.IsSimpleTypes && (
                draft6Schema.Type.Equals(JsonSchema.Draft6.Schema.SimpleTypes.EnumValues.Integer) ||
                (draft6Schema.Format.IsNullOrUndefined() && draft6Schema.Format == "integer"));
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an null enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNullEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema draft6Schema)
    {
        return draft6Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft6Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Gets a value indicating whether is has an object const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasObjectConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Gets a value indicating whether is has an array const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasArrayConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.String;
    }

    /// <summary>
    /// Gets a value indicating whether is has a number const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNumberConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.Number;
    }

    /// <summary>
    /// Gets a value indicating whether is has a null const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNullConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean const type.
    /// </summary>
    /// <param name="draft6Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasBooleanConst(this Schema draft6Schema)
    {
        return draft6Schema.Const.ValueKind == JsonValueKind.True || draft6Schema.Const.ValueKind == JsonValueKind.False;
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema draft6Schema)
    {
        return draft6Schema.Type.IsSimpleTypes;
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema draft6Schema)
    {
        return
            draft6Schema.ValueKind == JsonValueKind.True ||
            draft6Schema.ValueKind == JsonValueKind.False ||
            draft6Schema.IsEmpty() ||
            (draft6Schema.IsSimpleType() &&
            draft6Schema.AdditionalItems.IsUndefined() &&
            draft6Schema.AdditionalProperties.IsUndefined() &&
            draft6Schema.AllOf.IsUndefined() &&
            draft6Schema.AnyOf.IsUndefined() &&
            draft6Schema.Const.IsUndefined() &&
            draft6Schema.Contains.IsUndefined() &&
            draft6Schema.Default.IsUndefined() &&
            draft6Schema.Dependencies.IsUndefined() &&
            draft6Schema.Enum.IsUndefined() &&
            draft6Schema.ExclusiveMaximum.IsUndefined() &&
            draft6Schema.ExclusiveMinimum.IsUndefined() &&
            draft6Schema.Items.IsUndefined() &&
            draft6Schema.Maximum.IsUndefined() &&
            draft6Schema.MaxItems.IsUndefined() &&
            draft6Schema.MaxLength.IsUndefined() &&
            draft6Schema.MaxProperties.IsUndefined() &&
            draft6Schema.Minimum.IsUndefined() &&
            draft6Schema.MinItems.IsUndefined() &&
            draft6Schema.MinLength.IsUndefined() &&
            draft6Schema.MinProperties.IsUndefined() &&
            draft6Schema.MultipleOf.IsUndefined() &&
            draft6Schema.Not.IsUndefined() &&
            draft6Schema.OneOf.IsUndefined() &&
            draft6Schema.Pattern.IsUndefined() &&
            draft6Schema.PatternProperties.IsUndefined() &&
            draft6Schema.Properties.IsUndefined() &&
            draft6Schema.PropertyNames.IsUndefined() &&
            draft6Schema.Ref.IsUndefined() &&
            draft6Schema.Required.IsUndefined() &&
            draft6Schema.UniqueItems.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is a primitive type.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value and no format value.</returns>
    public static bool IsBuiltInPrimitiveType(this Schema draft6Schema)
    {
        return draft6Schema.IsBuiltInType() && draft6Schema.Format.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema draft6Schema)
    {
        return
            draft6Schema.AdditionalItems.IsUndefined() &&
            draft6Schema.AdditionalProperties.IsUndefined() &&
            draft6Schema.AllOf.IsUndefined() &&
            draft6Schema.AnyOf.IsUndefined() &&
            draft6Schema.Const.IsUndefined() &&
            draft6Schema.Contains.IsUndefined() &&
            draft6Schema.GetContentEncoding().IsUndefined() &&
            draft6Schema.GetContentMediaType().IsUndefined() &&
            draft6Schema.Default.IsUndefined() &&
            draft6Schema.Definitions.IsUndefined() &&
            draft6Schema.Dependencies.IsUndefined() &&
            draft6Schema.Description.IsUndefined() &&
            draft6Schema.Examples.IsUndefined() &&
            draft6Schema.Enum.IsUndefined() &&
            draft6Schema.ExclusiveMaximum.IsUndefined() &&
            draft6Schema.ExclusiveMinimum.IsUndefined() &&
            draft6Schema.Format.IsUndefined() &&
            draft6Schema.Items.IsUndefined() &&
            draft6Schema.Maximum.IsUndefined() &&
            draft6Schema.MaxItems.IsUndefined() &&
            draft6Schema.MaxLength.IsUndefined() &&
            draft6Schema.MaxProperties.IsUndefined() &&
            draft6Schema.Minimum.IsUndefined() &&
            draft6Schema.MinItems.IsUndefined() &&
            draft6Schema.MinLength.IsUndefined() &&
            draft6Schema.MinProperties.IsUndefined() &&
            draft6Schema.MultipleOf.IsUndefined() &&
            draft6Schema.Not.IsUndefined() &&
            draft6Schema.OneOf.IsUndefined() &&
            draft6Schema.Pattern.IsUndefined() &&
            draft6Schema.PatternProperties.IsUndefined() &&
            draft6Schema.Properties.IsUndefined() &&
            draft6Schema.PropertyNames.IsUndefined() &&
            draft6Schema.Ref.IsUndefined() &&
            draft6Schema.Required.IsUndefined() &&
            draft6Schema.Title.IsUndefined() &&
            draft6Schema.Type.IsUndefined() &&
            draft6Schema.UniqueItems.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked reference.
    /// </summary>
    /// <param name="draft6Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $ref and no other substantive properties.</returns>
    public static bool IsNakedReference(this Schema draft6Schema)
    {
        // If we have a reference, we are always naked.
        return
            draft6Schema.Ref.IsNotUndefined();
    }
}
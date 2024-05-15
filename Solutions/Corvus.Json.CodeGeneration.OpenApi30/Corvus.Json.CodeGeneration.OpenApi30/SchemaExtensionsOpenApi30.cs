// <copyright file="SchemaExtensionsOpenApi30.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.OpenApi30;

namespace Corvus.Json.CodeGeneration.OpenApi30;

/// <summary>
/// Extension methods for OpenApi30-related schema types.
/// </summary>
public static class SchemaExtensionsOpenApi30
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

        if (schema.Description.IsNotNullOrUndefined() || schema.Example.IsNotNullOrUndefined())
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

            if (schema.Example.IsNotNullOrUndefined())
            {
                documentation.AppendLine("/// <para>");
                documentation.AppendLine("/// Examples:");
                if (schema.Example.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonAny example in schema.Example.AsArray.EnumerateArray())
                    {
                        AppendExample(documentation, example);
                    }
                }
                else if (schema.Example.ValueKind == JsonValueKind.String)
                {
                    AppendExample(documentation, schema.Example);
                }

                documentation.AppendLine("/// </para>");
            }

            documentation.AppendLine("/// </remarks>");
        }

        return documentation.ToString();

        static void AppendExample(StringBuilder documentation, JsonAny example)
        {
            documentation.AppendLine("/// <example>");
            documentation.AppendLine("/// <code>");
#if NET8_0_OR_GREATER
            string[] lines = example.ToString().Split("\\n");
#else
            string[] lines = example.ToString().Split(["\\n"], StringSplitOptions.None);
#endif
            foreach (string line in lines)
            {
                documentation.Append("/// ");
                documentation.AppendLine(Formatting.FormatLiteralOrNull(line, false));
            }

            documentation.AppendLine("/// </code>");
            documentation.AppendLine("/// </example>");
        }
    }

    /// <summary>
    /// Determines if this schema is empty of known items, but contains unknown extensions.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema schema)
    {
        return schema.ValueKind == JsonValueKind.Object && IsEmpty(schema) && schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Array);
    }

    /// <summary>
    /// Gets the content media type for the draft 6 schema.
    /// </summary>
    /// <param name="schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentMediaType(this Schema schema)
    {
        if (schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
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
    /// <param name="schema">The schema for which to get the value.</param>
    /// <returns>The value, which may be undefined.</returns>
    public static JsonString GetContentEncoding(this Schema schema)
    {
        if (schema.TryGetProperty("media", out JsonAny media) && media.ValueKind == JsonValueKind.Object)
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
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && (schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Number) || schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit null type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitNullType(this Schema schema)
    {
        return
            schema.Nullable.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema schema)
    {
        return
            schema.IsExplicitObjectType() || schema.Properties.IsNotUndefined() || schema.Required.IsNotUndefined() || schema.AdditionalProperties.IsNotUndefined() || schema.MaxProperties.IsNotUndefined() || schema.MinProperties.IsNotUndefined() || schema.HasObjectEnum();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema schema)
    {
        return
            schema.IsExplicitArrayType() || schema.Items.IsNotUndefined() || schema.MaxItems.IsNotUndefined() || schema.MinItems.IsNotUndefined() || schema.UniqueItems.IsNotUndefined() || schema.HasArrayEnum();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema schema)
    {
        return
            schema.IsExplicitNumberType() || schema.Minimum.IsNotUndefined() || schema.Maximum.IsNotUndefined() || schema.ExclusiveMaximum.IsNotUndefined() || schema.ExclusiveMinimum.IsNotUndefined() || schema.MultipleOf.IsNotUndefined() || schema.HasNumberEnum();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema schema)
    {
        return
            schema.IsExplicitBooleanType() || schema.HasBooleanEnum();
    }

    /// <summary>
    /// Determines if this can be a null type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNullType(this Schema schema)
    {
        return
            schema.IsExplicitNullType() || schema.HasNullEnum();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema schema)
    {
        return
            schema.IsExplicitStringType() || schema.MinLength.IsNotUndefined() || schema.MaxLength.IsNotUndefined() || schema.Pattern.IsNotUndefined() || schema.HasStringEnum();
    }

    /// <summary>
    /// Determines if this is an integer type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsJsonInteger(this Schema schema)
    {
        return
            schema.Type.IsNotUndefined() && (
                schema.Type.Equals(JsonSchema.OpenApi30.Schema.TypeEntity.EnumValues.Integer) ||
                (schema.Format.IsNullOrUndefined() && schema.Format == "integer"));
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an null enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNullEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema schema)
    {
        return schema.Enum.ValueKind == JsonValueKind.Array &&
            schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema schema)
    {
        return schema.Type.IsNotUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema schema)
    {
        return
            schema.ValueKind == JsonValueKind.True ||
            schema.ValueKind == JsonValueKind.False ||
            schema.IsEmpty() ||
            schema.IsNakedNullType() ||
            (schema.IsSimpleType() &&
            schema.AdditionalProperties.IsUndefined() &&
            schema.AllOf.IsUndefined() &&
            schema.AnyOf.IsUndefined() &&
            schema.Default.IsUndefined() &&
            schema.Enum.IsUndefined() &&
            schema.ExclusiveMaximum.IsUndefined() &&
            schema.ExclusiveMinimum.IsUndefined() &&
            schema.Items.IsUndefined() &&
            schema.Maximum.IsUndefined() &&
            schema.MaxItems.IsUndefined() &&
            schema.MaxLength.IsUndefined() &&
            schema.MaxProperties.IsUndefined() &&
            schema.Minimum.IsUndefined() &&
            schema.MinItems.IsUndefined() &&
            schema.MinLength.IsUndefined() &&
            schema.MinProperties.IsUndefined() &&
            schema.MultipleOf.IsUndefined() &&
            schema.Not.IsUndefined() &&
            schema.OneOf.IsUndefined() &&
            schema.Pattern.IsUndefined() &&
            schema.Properties.IsUndefined() &&
            schema.Required.IsUndefined() &&
            schema.UniqueItems.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is a primitive type.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value and no format value.</returns>
    public static bool IsBuiltInPrimitiveType(this Schema schema)
    {
        return schema.IsBuiltInType() && schema.Format.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema schema)
    {
        return
            schema.Nullable.IsUndefined() &&
            schema.AdditionalProperties.IsUndefined() &&
            schema.AllOf.IsUndefined() &&
            schema.AnyOf.IsUndefined() &&
            schema.GetContentEncoding().IsUndefined() &&
            schema.GetContentMediaType().IsUndefined() &&
            schema.Default.IsUndefined() &&
            schema.Description.IsUndefined() &&
            schema.Enum.IsUndefined() &&
            schema.ExclusiveMaximum.IsUndefined() &&
            schema.ExclusiveMinimum.IsUndefined() &&
            schema.Format.IsUndefined() &&
            schema.Items.IsUndefined() &&
            schema.Maximum.IsUndefined() &&
            schema.MaxItems.IsUndefined() &&
            schema.MaxLength.IsUndefined() &&
            schema.MaxProperties.IsUndefined() &&
            schema.Minimum.IsUndefined() &&
            schema.MinItems.IsUndefined() &&
            schema.MinLength.IsUndefined() &&
            schema.MinProperties.IsUndefined() &&
            schema.MultipleOf.IsUndefined() &&
            schema.Not.IsUndefined() &&
            schema.OneOf.IsUndefined() &&
            schema.Pattern.IsUndefined() &&
            schema.Properties.IsUndefined() &&
            schema.Required.IsUndefined() &&
            schema.Title.IsUndefined() &&
            schema.Type.IsUndefined() &&
            schema.UniqueItems.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsNakedNullType(this Schema schema)
    {
        return
            schema.Nullable.ValueKind == JsonValueKind.True &&
            schema.AdditionalProperties.IsUndefined() &&
            schema.AllOf.IsUndefined() &&
            schema.AnyOf.IsUndefined() &&
            schema.GetContentEncoding().IsUndefined() &&
            schema.GetContentMediaType().IsUndefined() &&
            schema.Default.IsUndefined() &&
            schema.Description.IsUndefined() &&
            schema.Enum.IsUndefined() &&
            schema.ExclusiveMaximum.IsUndefined() &&
            schema.ExclusiveMinimum.IsUndefined() &&
            schema.Format.IsUndefined() &&
            schema.Items.IsUndefined() &&
            schema.Maximum.IsUndefined() &&
            schema.MaxItems.IsUndefined() &&
            schema.MaxLength.IsUndefined() &&
            schema.MaxProperties.IsUndefined() &&
            schema.Minimum.IsUndefined() &&
            schema.MinItems.IsUndefined() &&
            schema.MinLength.IsUndefined() &&
            schema.MinProperties.IsUndefined() &&
            schema.MultipleOf.IsUndefined() &&
            schema.Not.IsUndefined() &&
            schema.OneOf.IsUndefined() &&
            schema.Pattern.IsUndefined() &&
            schema.Properties.IsUndefined() &&
            schema.Required.IsUndefined() &&
            schema.Title.IsUndefined() &&
            schema.Type.IsUndefined() &&
            schema.UniqueItems.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked oneOf.
    /// </summary>
    /// <param name="schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a oneOf and no other substantive properties.</returns>
    public static bool IsNakedOneOf(this Schema schema)
    {
        return
            schema.OneOf.IsNotUndefined() &&
            schema.AdditionalProperties.IsUndefined() &&
            schema.AllOf.IsUndefined() &&
            schema.AnyOf.IsUndefined() &&
            schema.GetContentEncoding().IsUndefined() &&
            schema.GetContentMediaType().IsUndefined() &&
            schema.Default.IsUndefined() &&
            schema.Description.IsUndefined() &&
            schema.Enum.IsUndefined() &&
            schema.ExclusiveMaximum.IsUndefined() &&
            schema.ExclusiveMinimum.IsUndefined() &&
            schema.Format.IsUndefined() &&
            schema.Items.IsUndefined() &&
            schema.Maximum.IsUndefined() &&
            schema.MaxItems.IsUndefined() &&
            schema.MaxLength.IsUndefined() &&
            schema.MaxProperties.IsUndefined() &&
            schema.Minimum.IsUndefined() &&
            schema.MinItems.IsUndefined() &&
            schema.MinLength.IsUndefined() &&
            schema.MinProperties.IsUndefined() &&
            schema.MultipleOf.IsUndefined() &&
            schema.Not.IsUndefined() &&
            schema.Pattern.IsUndefined() &&
            schema.Properties.IsUndefined() &&
            schema.Required.IsUndefined() &&
            schema.Title.IsUndefined() &&
            schema.Type.IsUndefined() &&
            schema.UniqueItems.IsUndefined();
    }
}
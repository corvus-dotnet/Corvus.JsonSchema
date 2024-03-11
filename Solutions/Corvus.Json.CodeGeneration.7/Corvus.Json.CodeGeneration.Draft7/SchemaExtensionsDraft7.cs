// <copyright file="SchemaExtensionsDraft7.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.Draft7;

namespace Corvus.Json.CodeGeneration.Draft7;

/// <summary>
/// Extension methods for Draft7-related schema types.
/// </summary>
public static class SchemaExtensionsDraft7
{
    /// <summary>
    /// Gets the given type declaration's schema as a draft-7 schema instance.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the schema.</param>
    /// <returns>The schema as a draft-7 instance.</returns>
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
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema draft7Schema)
    {
        return draft7Schema.ValueKind == JsonValueKind.Object && IsEmpty(draft7Schema) && draft7Schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Array);
    }

    /// <summary>
    /// Determines if this is an explicit object type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && (draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Number) || draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit null type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitNullType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Null);
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitObjectType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Object))) || draft7Schema.Properties.IsNotUndefined() || draft7Schema.Required.IsNotUndefined() || draft7Schema.AdditionalProperties.IsNotUndefined() || draft7Schema.MaxProperties.IsNotUndefined() || draft7Schema.MinProperties.IsNotUndefined() || draft7Schema.PatternProperties.IsNotUndefined() || draft7Schema.PropertyNames.IsNotUndefined() || draft7Schema.Dependencies.IsNotUndefined() || draft7Schema.HasObjectEnum() || draft7Schema.HasObjectConst();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitArrayType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Array))) || draft7Schema.AdditionalItems.IsNotUndefined() || draft7Schema.Contains.IsNotUndefined() || draft7Schema.Items.IsNotUndefined() || draft7Schema.MaxItems.IsNotUndefined() || draft7Schema.MinItems.IsNotUndefined() || draft7Schema.UniqueItems.IsNotUndefined() || draft7Schema.HasArrayEnum() || draft7Schema.HasArrayConst();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitNumberType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Number) || type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Integer))) || draft7Schema.Minimum.IsNotUndefined() || draft7Schema.Maximum.IsNotUndefined() || draft7Schema.ExclusiveMaximum.IsNotUndefined() || draft7Schema.ExclusiveMinimum.IsNotUndefined() || draft7Schema.MultipleOf.IsNotUndefined() || draft7Schema.HasNumberEnum() || draft7Schema.HasNumberConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitBooleanType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Boolean))) || draft7Schema.HasBooleanEnum() || draft7Schema.HasBooleanConst();
    }

    /// <summary>
    /// Determines if this can be a null type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNullType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitNullType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Null))) || draft7Schema.HasNullEnum() || draft7Schema.HasNullConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema draft7Schema)
    {
        return
            draft7Schema.IsExplicitStringType() || (draft7Schema.Type.IsSimpleTypesArray && draft7Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.String))) || draft7Schema.MinLength.IsNotUndefined() || draft7Schema.MaxLength.IsNotUndefined() || draft7Schema.Pattern.IsNotUndefined() || draft7Schema.HasStringEnum() || draft7Schema.HasStringConst();
    }

    /// <summary>
    /// Determines if this is an integer type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsJsonInteger(this Schema draft7Schema)
    {
        return
            draft7Schema.Type.IsSimpleTypes && (
                draft7Schema.Type.Equals(JsonSchema.Draft7.Schema.SimpleTypes.EnumValues.Integer) ||
                (draft7Schema.Format.IsNullOrUndefined() && draft7Schema.Format == "integer"));
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an null enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNullEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema draft7Schema)
    {
        return draft7Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft7Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Gets a value indicating whether is has an object const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasObjectConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Gets a value indicating whether is has an array const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasArrayConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.String;
    }

    /// <summary>
    /// Gets a value indicating whether is has a number const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNumberConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.Number;
    }

    /// <summary>
    /// Gets a value indicating whether is has a null const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNullConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean const type.
    /// </summary>
    /// <param name="draft7Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasBooleanConst(this Schema draft7Schema)
    {
        return draft7Schema.Const.ValueKind == JsonValueKind.True || draft7Schema.Const.ValueKind == JsonValueKind.False;
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema draft7Schema)
    {
        return draft7Schema.Type.IsSimpleTypes;
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema draft7Schema)
    {
        return
            draft7Schema.ValueKind == JsonValueKind.True ||
            draft7Schema.ValueKind == JsonValueKind.False ||
            draft7Schema.IsEmpty() ||
            (draft7Schema.IsSimpleType() &&
            draft7Schema.AdditionalItems.IsUndefined() &&
            draft7Schema.AdditionalProperties.IsUndefined() &&
            draft7Schema.AllOf.IsUndefined() &&
            draft7Schema.AnyOf.IsUndefined() &&
            draft7Schema.Const.IsUndefined() &&
            draft7Schema.Contains.IsUndefined() &&
            draft7Schema.Default.IsUndefined() &&
            draft7Schema.Dependencies.IsUndefined() &&
            draft7Schema.Else.IsUndefined() &&
            draft7Schema.Enum.IsUndefined() &&
            draft7Schema.ExclusiveMaximum.IsUndefined() &&
            draft7Schema.ExclusiveMinimum.IsUndefined() &&
            draft7Schema.If.IsUndefined() &&
            draft7Schema.Items.IsUndefined() &&
            draft7Schema.Maximum.IsUndefined() &&
            draft7Schema.MaxItems.IsUndefined() &&
            draft7Schema.MaxLength.IsUndefined() &&
            draft7Schema.MaxProperties.IsUndefined() &&
            draft7Schema.Minimum.IsUndefined() &&
            draft7Schema.MinItems.IsUndefined() &&
            draft7Schema.MinLength.IsUndefined() &&
            draft7Schema.MinProperties.IsUndefined() &&
            draft7Schema.MultipleOf.IsUndefined() &&
            draft7Schema.Not.IsUndefined() &&
            draft7Schema.OneOf.IsUndefined() &&
            draft7Schema.Pattern.IsUndefined() &&
            draft7Schema.PatternProperties.IsUndefined() &&
            draft7Schema.Properties.IsUndefined() &&
            draft7Schema.PropertyNames.IsUndefined() &&
            draft7Schema.ReadOnly.IsUndefined() &&
            draft7Schema.Ref.IsUndefined() &&
            draft7Schema.Required.IsUndefined() &&
            draft7Schema.Then.IsUndefined() &&
            draft7Schema.UniqueItems.IsUndefined() &&
            draft7Schema.WriteOnly.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is a primitive type.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value and no format value.</returns>
    public static bool IsBuiltInPrimitiveType(this Schema draft7Schema)
    {
        return draft7Schema.IsBuiltInType() && draft7Schema.Format.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema draft7Schema)
    {
        return
            draft7Schema.Comment.IsUndefined() &&
            draft7Schema.AdditionalItems.IsUndefined() &&
            draft7Schema.AdditionalProperties.IsUndefined() &&
            draft7Schema.AllOf.IsUndefined() &&
            draft7Schema.AnyOf.IsUndefined() &&
            draft7Schema.Const.IsUndefined() &&
            draft7Schema.Contains.IsUndefined() &&
            draft7Schema.ContentEncoding.IsUndefined() &&
            draft7Schema.ContentMediaType.IsUndefined() &&
            draft7Schema.Default.IsUndefined() &&
            draft7Schema.Definitions.IsUndefined() &&
            draft7Schema.Dependencies.IsUndefined() &&
            draft7Schema.Description.IsUndefined() &&
            draft7Schema.Examples.IsUndefined() &&
            draft7Schema.Else.IsUndefined() &&
            draft7Schema.Enum.IsUndefined() &&
            draft7Schema.ExclusiveMaximum.IsUndefined() &&
            draft7Schema.ExclusiveMinimum.IsUndefined() &&
            draft7Schema.Format.IsUndefined() &&
            draft7Schema.If.IsUndefined() &&
            draft7Schema.Items.IsUndefined() &&
            draft7Schema.Maximum.IsUndefined() &&
            draft7Schema.MaxItems.IsUndefined() &&
            draft7Schema.MaxLength.IsUndefined() &&
            draft7Schema.MaxProperties.IsUndefined() &&
            draft7Schema.Minimum.IsUndefined() &&
            draft7Schema.MinItems.IsUndefined() &&
            draft7Schema.MinLength.IsUndefined() &&
            draft7Schema.MinProperties.IsUndefined() &&
            draft7Schema.MultipleOf.IsUndefined() &&
            draft7Schema.Not.IsUndefined() &&
            draft7Schema.OneOf.IsUndefined() &&
            draft7Schema.Pattern.IsUndefined() &&
            draft7Schema.PatternProperties.IsUndefined() &&
            draft7Schema.Properties.IsUndefined() &&
            draft7Schema.PropertyNames.IsUndefined() &&
            draft7Schema.ReadOnly.IsUndefined() &&
            draft7Schema.Ref.IsUndefined() &&
            draft7Schema.Required.IsUndefined() &&
            draft7Schema.Then.IsUndefined() &&
            draft7Schema.Title.IsUndefined() &&
            draft7Schema.Type.IsUndefined() &&
            draft7Schema.UniqueItems.IsUndefined() &&
            draft7Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked reference.
    /// </summary>
    /// <param name="draft7Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $ref and no other substantive properties.</returns>
    public static bool IsNakedReference(this Schema draft7Schema)
    {
        // If we have a reference, we are always naked.
        return
            draft7Schema.Ref.IsNotUndefined();
    }
}
// <copyright file="SchemaExtensionsDraft201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.JsonSchema.Draft201909;

namespace Corvus.Json.CodeGeneration.Draft201909;

/// <summary>
/// Extension methods for Draft201909-related schema types.
/// </summary>
public static class SchemaExtensionsDraft201909
{
    /// <summary>
    /// Gets the given type declaration's schema as a draft-2019-09 schema instance.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the schema.</param>
    /// <returns>The schema as a draft-2019-09 instance.</returns>
    public static Schema Schema(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.LocatedSchema.Schema.As<Schema>();
    }

    /// <summary>
    /// Determines if this schema is empty of known items, but contains unknown extensions.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema draft201909Schema)
    {
        return draft201909Schema.ValueKind == JsonValueKind.Object && IsEmpty(draft201909Schema) && draft201909Schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Array);
    }

    /// <summary>
    /// Determines if this is an explicit object type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && (draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Number) || draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit null type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitNullType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Null);
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Type.IsSimpleTypes && draft201909Schema.Type.Equals(Validation.SimpleTypes.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitObjectType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Object))) || draft201909Schema.Properties.IsNotUndefined() || draft201909Schema.Required.IsNotUndefined() || draft201909Schema.DependentRequired.IsNotUndefined() || draft201909Schema.AdditionalProperties.IsNotUndefined() || draft201909Schema.DependentSchemas.IsNotUndefined() || draft201909Schema.MaxProperties.IsNotUndefined() || draft201909Schema.MinProperties.IsNotUndefined() || draft201909Schema.PatternProperties.IsNotUndefined() || draft201909Schema.PropertyNames.IsNotUndefined() || draft201909Schema.UnevaluatedProperties.IsNotUndefined() || draft201909Schema.HasObjectEnum() || draft201909Schema.HasObjectConst();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitArrayType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Array))) || draft201909Schema.AdditionalItems.IsNotUndefined() || draft201909Schema.Contains.IsNotUndefined() || draft201909Schema.Items.IsNotUndefined() || draft201909Schema.MaxContains.IsNotUndefined() || draft201909Schema.MaxItems.IsNotUndefined() || draft201909Schema.MinContains.IsNotUndefined() || draft201909Schema.MinItems.IsNotUndefined() || draft201909Schema.UnevaluatedItems.IsNotUndefined() || draft201909Schema.UniqueItems.IsNotUndefined() || draft201909Schema.HasArrayEnum() || draft201909Schema.HasArrayConst();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitNumberType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Number) || type.Equals(Validation.SimpleTypes.EnumValues.Integer))) || draft201909Schema.Minimum.IsNotUndefined() || draft201909Schema.Maximum.IsNotUndefined() || draft201909Schema.ExclusiveMaximum.IsNotUndefined() || draft201909Schema.ExclusiveMinimum.IsNotUndefined() || draft201909Schema.MultipleOf.IsNotUndefined() || draft201909Schema.HasNumberEnum() || draft201909Schema.HasNumberConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitBooleanType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Boolean))) || draft201909Schema.HasBooleanEnum() || draft201909Schema.HasBooleanConst();
    }

    /// <summary>
    /// Determines if this can be a null type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNullType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitNullType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Null))) || draft201909Schema.HasNullEnum() || draft201909Schema.HasNullConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.IsExplicitStringType() || (draft201909Schema.Type.IsSimpleTypesArray && draft201909Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.String))) || draft201909Schema.MinLength.IsNotUndefined() || draft201909Schema.MaxLength.IsNotUndefined() || draft201909Schema.Pattern.IsNotUndefined() || draft201909Schema.HasStringEnum() || draft201909Schema.HasStringConst();
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an null enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNullEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema draft201909Schema)
    {
        return draft201909Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft201909Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Gets a value indicating whether is has an object const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasObjectConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Gets a value indicating whether is has an array const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasArrayConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.String;
    }

    /// <summary>
    /// Gets a value indicating whether is has a number const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNumberConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.Number;
    }

    /// <summary>
    /// Gets a value indicating whether is has a null const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNullConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean const type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasBooleanConst(this Schema draft201909Schema)
    {
        return draft201909Schema.Const.ValueKind == JsonValueKind.True || draft201909Schema.Const.ValueKind == JsonValueKind.False;
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema draft201909Schema)
    {
        return draft201909Schema.Type.IsSimpleTypes;
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema draft201909Schema)
    {
        return
            draft201909Schema.ValueKind == JsonValueKind.True ||
            draft201909Schema.ValueKind == JsonValueKind.False ||
            draft201909Schema.IsEmpty() ||
            (draft201909Schema.IsSimpleType() &&
            draft201909Schema.AdditionalItems.IsUndefined() &&
            draft201909Schema.AdditionalProperties.IsUndefined() &&
            draft201909Schema.AllOf.IsUndefined() &&
            draft201909Schema.AnyOf.IsUndefined() &&
            draft201909Schema.Const.IsUndefined() &&
            draft201909Schema.Contains.IsUndefined() &&
            draft201909Schema.ContentSchema.IsUndefined() &&
            draft201909Schema.Default.IsUndefined() &&
            draft201909Schema.Dependencies.IsUndefined() &&
            draft201909Schema.DependentRequired.IsUndefined() &&
            draft201909Schema.DependentSchemas.IsUndefined() &&
            draft201909Schema.Else.IsUndefined() &&
            draft201909Schema.Enum.IsUndefined() &&
            draft201909Schema.ExclusiveMaximum.IsUndefined() &&
            draft201909Schema.ExclusiveMinimum.IsUndefined() &&
            draft201909Schema.If.IsUndefined() &&
            draft201909Schema.Items.IsUndefined() &&
            draft201909Schema.MaxContains.IsUndefined() &&
            draft201909Schema.Maximum.IsUndefined() &&
            draft201909Schema.MaxItems.IsUndefined() &&
            draft201909Schema.MaxLength.IsUndefined() &&
            draft201909Schema.MaxProperties.IsUndefined() &&
            draft201909Schema.MinContains.IsUndefined() &&
            draft201909Schema.Minimum.IsUndefined() &&
            draft201909Schema.MinItems.IsUndefined() &&
            draft201909Schema.MinLength.IsUndefined() &&
            draft201909Schema.MinProperties.IsUndefined() &&
            draft201909Schema.MultipleOf.IsUndefined() &&
            draft201909Schema.Not.IsUndefined() &&
            draft201909Schema.OneOf.IsUndefined() &&
            draft201909Schema.Pattern.IsUndefined() &&
            draft201909Schema.PatternProperties.IsUndefined() &&
            draft201909Schema.Properties.IsUndefined() &&
            draft201909Schema.PropertyNames.IsUndefined() &&
            draft201909Schema.ReadOnly.IsUndefined() &&
            draft201909Schema.RecursiveAnchor.IsUndefined() &&
            draft201909Schema.RecursiveRef.IsUndefined() &&
            draft201909Schema.Ref.IsUndefined() &&
            draft201909Schema.Required.IsUndefined() &&
            draft201909Schema.Then.IsUndefined() &&
            draft201909Schema.UnevaluatedItems.IsUndefined() &&
            draft201909Schema.UnevaluatedProperties.IsUndefined() &&
            draft201909Schema.UniqueItems.IsUndefined() &&
            draft201909Schema.WriteOnly.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Comment.IsUndefined() &&
            draft201909Schema.AdditionalItems.IsUndefined() &&
            draft201909Schema.AdditionalProperties.IsUndefined() &&
            draft201909Schema.AllOf.IsUndefined() &&
            draft201909Schema.Anchor.IsUndefined() &&
            draft201909Schema.AnyOf.IsUndefined() &&
            draft201909Schema.Const.IsUndefined() &&
            draft201909Schema.Contains.IsUndefined() &&
            draft201909Schema.ContentEncoding.IsUndefined() &&
            draft201909Schema.ContentMediaType.IsUndefined() &&
            draft201909Schema.ContentSchema.IsUndefined() &&
            draft201909Schema.Default.IsUndefined() &&
            draft201909Schema.Definitions.IsUndefined() &&
            draft201909Schema.Defs.IsUndefined() &&
            draft201909Schema.Deprecated.IsUndefined() &&
            draft201909Schema.Dependencies.IsUndefined() &&
            draft201909Schema.DependentRequired.IsUndefined() &&
            draft201909Schema.DependentSchemas.IsUndefined() &&
            draft201909Schema.Description.IsUndefined() &&
            draft201909Schema.Examples.IsUndefined() &&
            draft201909Schema.Else.IsUndefined() &&
            draft201909Schema.Enum.IsUndefined() &&
            draft201909Schema.ExclusiveMaximum.IsUndefined() &&
            draft201909Schema.ExclusiveMinimum.IsUndefined() &&
            draft201909Schema.Format.IsUndefined() &&
            draft201909Schema.If.IsUndefined() &&
            draft201909Schema.Items.IsUndefined() &&
            draft201909Schema.MaxContains.IsUndefined() &&
            draft201909Schema.Maximum.IsUndefined() &&
            draft201909Schema.MaxItems.IsUndefined() &&
            draft201909Schema.MaxLength.IsUndefined() &&
            draft201909Schema.MaxProperties.IsUndefined() &&
            draft201909Schema.MinContains.IsUndefined() &&
            draft201909Schema.Minimum.IsUndefined() &&
            draft201909Schema.MinItems.IsUndefined() &&
            draft201909Schema.MinLength.IsUndefined() &&
            draft201909Schema.MinProperties.IsUndefined() &&
            draft201909Schema.MultipleOf.IsUndefined() &&
            draft201909Schema.Not.IsUndefined() &&
            draft201909Schema.OneOf.IsUndefined() &&
            draft201909Schema.Pattern.IsUndefined() &&
            draft201909Schema.PatternProperties.IsUndefined() &&
            draft201909Schema.Properties.IsUndefined() &&
            draft201909Schema.PropertyNames.IsUndefined() &&
            draft201909Schema.ReadOnly.IsUndefined() &&
            draft201909Schema.RecursiveAnchor.IsUndefined() &&
            draft201909Schema.RecursiveRef.IsUndefined() &&
            draft201909Schema.Ref.IsUndefined() &&
            draft201909Schema.Required.IsUndefined() &&
            draft201909Schema.Then.IsUndefined() &&
            draft201909Schema.Title.IsUndefined() &&
            draft201909Schema.Type.IsUndefined() &&
            draft201909Schema.UnevaluatedItems.IsUndefined() &&
            draft201909Schema.UnevaluatedProperties.IsUndefined() &&
            draft201909Schema.UniqueItems.IsUndefined() &&
            draft201909Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked reference.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $ref and no other substantive properties.</returns>
    public static bool IsNakedReference(this Schema draft201909Schema)
    {
        return
            draft201909Schema.Ref.IsNotUndefined() &&
            draft201909Schema.AdditionalItems.IsUndefined() &&
            draft201909Schema.AdditionalProperties.IsUndefined() &&
            draft201909Schema.AllOf.IsUndefined() &&
            draft201909Schema.Anchor.IsUndefined() &&
            draft201909Schema.AnyOf.IsUndefined() &&
            draft201909Schema.Const.IsUndefined() &&
            draft201909Schema.Contains.IsUndefined() &&
            draft201909Schema.ContentEncoding.IsUndefined() &&
            draft201909Schema.ContentMediaType.IsUndefined() &&
            draft201909Schema.ContentSchema.IsUndefined() &&
            draft201909Schema.Default.IsUndefined() &&
            draft201909Schema.Dependencies.IsUndefined() &&
            draft201909Schema.DependentRequired.IsUndefined() &&
            draft201909Schema.DependentSchemas.IsUndefined() &&
            draft201909Schema.Else.IsUndefined() &&
            draft201909Schema.Enum.IsUndefined() &&
            draft201909Schema.ExclusiveMaximum.IsUndefined() &&
            draft201909Schema.ExclusiveMinimum.IsUndefined() &&
            draft201909Schema.Format.IsUndefined() &&
            draft201909Schema.If.IsUndefined() &&
            draft201909Schema.Items.IsUndefined() &&
            draft201909Schema.MaxContains.IsUndefined() &&
            draft201909Schema.Maximum.IsUndefined() &&
            draft201909Schema.MaxItems.IsUndefined() &&
            draft201909Schema.MaxLength.IsUndefined() &&
            draft201909Schema.MaxProperties.IsUndefined() &&
            draft201909Schema.MinContains.IsUndefined() &&
            draft201909Schema.Minimum.IsUndefined() &&
            draft201909Schema.MinItems.IsUndefined() &&
            draft201909Schema.MinLength.IsUndefined() &&
            draft201909Schema.MinProperties.IsUndefined() &&
            draft201909Schema.MultipleOf.IsUndefined() &&
            draft201909Schema.Not.IsUndefined() &&
            draft201909Schema.OneOf.IsUndefined() &&
            draft201909Schema.Pattern.IsUndefined() &&
            draft201909Schema.PatternProperties.IsUndefined() &&
            draft201909Schema.Properties.IsUndefined() &&
            draft201909Schema.PropertyNames.IsUndefined() &&
            draft201909Schema.ReadOnly.IsUndefined() &&
            draft201909Schema.RecursiveRef.IsUndefined() &&
            draft201909Schema.Required.IsUndefined() &&
            draft201909Schema.Then.IsUndefined() &&
            draft201909Schema.Type.IsUndefined() &&
            draft201909Schema.UnevaluatedItems.IsUndefined() &&
            draft201909Schema.UnevaluatedProperties.IsUndefined() &&
            draft201909Schema.UniqueItems.IsUndefined() &&
            draft201909Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked recursive reference.
    /// </summary>
    /// <param name="draft201909Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $recursiveRef and no other substantive properties.</returns>
    public static bool IsNakedRecursiveReference(this Schema draft201909Schema)
    {
        return
            draft201909Schema.RecursiveRef.IsNotUndefined() &&
            draft201909Schema.AdditionalItems.IsUndefined() &&
            draft201909Schema.AdditionalProperties.IsUndefined() &&
            draft201909Schema.AllOf.IsUndefined() &&
            draft201909Schema.Anchor.IsUndefined() &&
            draft201909Schema.AnyOf.IsUndefined() &&
            draft201909Schema.Const.IsUndefined() &&
            draft201909Schema.Contains.IsUndefined() &&
            draft201909Schema.ContentEncoding.IsUndefined() &&
            draft201909Schema.ContentMediaType.IsUndefined() &&
            draft201909Schema.ContentSchema.IsUndefined() &&
            draft201909Schema.Default.IsUndefined() &&
            draft201909Schema.Dependencies.IsUndefined() &&
            draft201909Schema.DependentRequired.IsUndefined() &&
            draft201909Schema.DependentSchemas.IsUndefined() &&
            draft201909Schema.Else.IsUndefined() &&
            draft201909Schema.Enum.IsUndefined() &&
            draft201909Schema.ExclusiveMaximum.IsUndefined() &&
            draft201909Schema.ExclusiveMinimum.IsUndefined() &&
            draft201909Schema.Format.IsUndefined() &&
            draft201909Schema.If.IsUndefined() &&
            draft201909Schema.Items.IsUndefined() &&
            draft201909Schema.MaxContains.IsUndefined() &&
            draft201909Schema.Maximum.IsUndefined() &&
            draft201909Schema.MaxItems.IsUndefined() &&
            draft201909Schema.MaxLength.IsUndefined() &&
            draft201909Schema.MaxProperties.IsUndefined() &&
            draft201909Schema.MinContains.IsUndefined() &&
            draft201909Schema.Minimum.IsUndefined() &&
            draft201909Schema.MinItems.IsUndefined() &&
            draft201909Schema.MinLength.IsUndefined() &&
            draft201909Schema.MinProperties.IsUndefined() &&
            draft201909Schema.MultipleOf.IsUndefined() &&
            draft201909Schema.Not.IsUndefined() &&
            draft201909Schema.OneOf.IsUndefined() &&
            draft201909Schema.Pattern.IsUndefined() &&
            draft201909Schema.PatternProperties.IsUndefined() &&
            draft201909Schema.Properties.IsUndefined() &&
            draft201909Schema.PropertyNames.IsUndefined() &&
            draft201909Schema.ReadOnly.IsUndefined() &&
            draft201909Schema.Ref.IsUndefined() &&
            draft201909Schema.Required.IsUndefined() &&
            draft201909Schema.Then.IsUndefined() &&
            draft201909Schema.Type.IsUndefined() &&
            draft201909Schema.UnevaluatedItems.IsUndefined() &&
            draft201909Schema.UnevaluatedProperties.IsUndefined() &&
            draft201909Schema.UniqueItems.IsUndefined() &&
            draft201909Schema.WriteOnly.IsUndefined();
    }
}
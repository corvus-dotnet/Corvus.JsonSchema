// <copyright file="SchemaExtensionsDraft202012.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.JsonSchema.Draft202012;

namespace Corvus.Json.CodeGeneration.Draft202012;

/// <summary>
/// Extension methods for Draft201909-related schema types.
/// </summary>
public static class SchemaExtensionsDraft202012
{
    /// <summary>
    /// Gets the given type declaration's schema as a draft-2020-12 schema instance.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the schema.</param>
    /// <returns>The schema as a draft-2020-12 instance.</returns>
    public static Schema Schema(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.LocatedSchema.Schema.As<Schema>();
    }

    /// <summary>
    /// Determines if this schema is empty of known items, but contains unknown extensions.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the known items are empty, but there are additional properties on the JsonElement.</returns>
    public static bool EmptyButWithUnknownExtensions(this Schema draft202012Schema)
    {
        return draft202012Schema.ValueKind == JsonValueKind.Object && IsEmpty(draft202012Schema) && draft202012Schema.EnumerateObject().MoveNext();
    }

    /// <summary>
    /// Determines if this is an explicit array type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitArrayType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Type.IsSimpleTypes && draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Array);
    }

    /// <summary>
    /// Determines if this is an explicit object type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitObjectType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Type.IsSimpleTypes && draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Object);
    }

    /// <summary>
    /// Determines if this is an explicit number type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitNumberType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Type.IsSimpleTypes && (draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Number) || draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Integer));
    }

    /// <summary>
    /// Determines if this is an explicit boolean type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value.</returns>
    public static bool IsExplicitBooleanType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Type.IsSimpleTypes && draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.Boolean);
    }

    /// <summary>
    /// Determines if this is an explicit string type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsExplicitStringType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Type.IsSimpleTypes && draft202012Schema.Type.Equals(Validation.SimpleTypes.EnumValues.String);
    }

    /// <summary>
    /// Determines if this can be an object type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsObjectType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.IsExplicitObjectType() || (draft202012Schema.Type.IsSimpleTypesArray && draft202012Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Object))) || draft202012Schema.Properties.IsNotUndefined() || draft202012Schema.Required.IsNotUndefined() || draft202012Schema.DependentRequired.IsNotUndefined() || draft202012Schema.AdditionalProperties.IsNotUndefined() || draft202012Schema.DependentSchemas.IsNotUndefined() || draft202012Schema.MaxProperties.IsNotUndefined() || draft202012Schema.MinProperties.IsNotUndefined() || draft202012Schema.PatternProperties.IsNotUndefined() || draft202012Schema.PropertyNames.IsNotUndefined() || draft202012Schema.UnevaluatedProperties.IsNotUndefined() || draft202012Schema.HasObjectEnum() || draft202012Schema.HasObjectConst();
    }

    /// <summary>
    /// Determines if this can be an array type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsArrayType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.IsExplicitArrayType() || (draft202012Schema.Type.IsSimpleTypesArray && draft202012Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Array))) || draft202012Schema.PrefixItems.IsNotUndefined() || draft202012Schema.Contains.IsNotUndefined() || draft202012Schema.Items.IsNotUndefined() || draft202012Schema.MaxContains.IsNotUndefined() || draft202012Schema.MaxItems.IsNotUndefined() || draft202012Schema.MinContains.IsNotUndefined() || draft202012Schema.MinItems.IsNotUndefined() || draft202012Schema.UnevaluatedItems.IsNotUndefined() || draft202012Schema.UniqueItems.IsNotUndefined() || draft202012Schema.HasArrayEnum() || draft202012Schema.HasArrayConst();
    }

    /// <summary>
    /// Determines if this can be a number type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsNumberType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.IsExplicitNumberType() || (draft202012Schema.Type.IsSimpleTypesArray && draft202012Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Number) || type.Equals(Validation.SimpleTypes.EnumValues.Integer))) || draft202012Schema.Minimum.IsNotUndefined() || draft202012Schema.Maximum.IsNotUndefined() || draft202012Schema.ExclusiveMaximum.IsNotUndefined() || draft202012Schema.ExclusiveMinimum.IsNotUndefined() || draft202012Schema.MultipleOf.IsNotUndefined() || draft202012Schema.HasNumberEnum() || draft202012Schema.HasNumberConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsBooleanType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.IsExplicitBooleanType() || (draft202012Schema.Type.IsSimpleTypesArray && draft202012Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.Boolean))) || draft202012Schema.HasBooleanEnum() || draft202012Schema.HasBooleanConst();
    }

    /// <summary>
    /// Determines if this can be a boolean type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsStringType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.IsExplicitStringType() || (draft202012Schema.Type.IsSimpleTypesArray && draft202012Schema.Type.AsSimpleTypesArray.EnumerateArray().Any(type => type.Equals(Validation.SimpleTypes.EnumValues.String))) || draft202012Schema.MinLength.IsNotUndefined() || draft202012Schema.MaxLength.IsNotUndefined() || draft202012Schema.Pattern.IsNotUndefined() || draft202012Schema.HasStringEnum() || draft202012Schema.HasStringConst() || draft202012Schema.HasStringFormat();
    }

    /// <summary>
    /// Gets a value indicating whether is has an object enum type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasObjectEnum(this Schema draft202012Schema)
    {
        return draft202012Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft202012Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasArrayEnum(this Schema draft202012Schema)
    {
        return draft202012Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft202012Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Array);
    }

    /// <summary>
    /// Gets a value indicating whether is has an number enum type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasNumberEnum(this Schema draft202012Schema)
    {
        return draft202012Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft202012Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Number);
    }

    /// <summary>
    /// Gets a value indicating whether is has an array enum type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasStringEnum(this Schema draft202012Schema)
    {
        return draft202012Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft202012Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean enum type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has at least one enum value of the correct type.</returns>
    public static bool HasBooleanEnum(this Schema draft202012Schema)
    {
        return draft202012Schema.Enum.ValueKind == JsonValueKind.Array &&
            draft202012Schema.Enum.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False);
    }

    /// <summary>
    /// Gets a value indicating whether is has an object const type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasObjectConst(this Schema draft202012Schema)
    {
        return draft202012Schema.Const.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Gets a value indicating whether is has an array const type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasArrayConst(this Schema draft202012Schema)
    {
        return draft202012Schema.Const.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string const type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringConst(this Schema draft202012Schema)
    {
        return draft202012Schema.Const.ValueKind == JsonValueKind.String;
    }

    /// <summary>
    /// Gets a value indicating whether is has a string format type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasStringFormat(this Schema draft202012Schema)
    {
        return (draft202012Schema.Format.ValueKind == JsonValueKind.String) && BuiltInTypes.IsStringFormat((string)draft202012Schema.Format);
    }

    /// <summary>
    /// Gets a value indicating whether is has a number const type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasNumberConst(this Schema draft202012Schema)
    {
        return draft202012Schema.Const.ValueKind == JsonValueKind.Number;
    }

    /// <summary>
    /// Gets a value indicating whether is has a boolean const type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to validate.</param>
    /// <returns>True if the schema has a const value of the correct type.</returns>
    public static bool HasBooleanConst(this Schema draft202012Schema)
    {
        return draft202012Schema.Const.ValueKind == JsonValueKind.True || draft202012Schema.Const.ValueKind == JsonValueKind.False;
    }

    /// <summary>
    /// Determines if this schema is a simple type.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a single type value, or no type value but a format value.</returns>
    public static bool IsSimpleType(this Schema draft202012Schema)
    {
        return draft202012Schema.Type.IsSimpleTypes;
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsBuiltInType(this Schema draft202012Schema)
    {
        return
            draft202012Schema.ValueKind == JsonValueKind.True ||
            draft202012Schema.ValueKind == JsonValueKind.False ||
            draft202012Schema.IsEmpty() ||
            (draft202012Schema.IsSimpleType() &&
            draft202012Schema.PrefixItems.IsUndefined() &&
            draft202012Schema.AdditionalProperties.IsUndefined() &&
            draft202012Schema.AllOf.IsUndefined() &&
            draft202012Schema.AnyOf.IsUndefined() &&
            draft202012Schema.Const.IsUndefined() &&
            draft202012Schema.Contains.IsUndefined() &&
            draft202012Schema.ContentSchema.IsUndefined() &&
            draft202012Schema.Default.IsUndefined() &&
            draft202012Schema.Dependencies.IsUndefined() &&
            draft202012Schema.DependentRequired.IsUndefined() &&
            draft202012Schema.DependentSchemas.IsUndefined() &&
            draft202012Schema.Else.IsUndefined() &&
            draft202012Schema.Enum.IsUndefined() &&
            draft202012Schema.ExclusiveMaximum.IsUndefined() &&
            draft202012Schema.ExclusiveMinimum.IsUndefined() &&
            draft202012Schema.If.IsUndefined() &&
            draft202012Schema.Items.IsUndefined() &&
            draft202012Schema.MaxContains.IsUndefined() &&
            draft202012Schema.Maximum.IsUndefined() &&
            draft202012Schema.MaxItems.IsUndefined() &&
            draft202012Schema.MaxLength.IsUndefined() &&
            draft202012Schema.MaxProperties.IsUndefined() &&
            draft202012Schema.MinContains.IsUndefined() &&
            draft202012Schema.Minimum.IsUndefined() &&
            draft202012Schema.MinItems.IsUndefined() &&
            draft202012Schema.MinLength.IsUndefined() &&
            draft202012Schema.MinProperties.IsUndefined() &&
            draft202012Schema.MultipleOf.IsUndefined() &&
            draft202012Schema.Not.IsUndefined() &&
            draft202012Schema.OneOf.IsUndefined() &&
            draft202012Schema.Pattern.IsUndefined() &&
            draft202012Schema.PatternProperties.IsUndefined() &&
            draft202012Schema.Properties.IsUndefined() &&
            draft202012Schema.PropertyNames.IsUndefined() &&
            draft202012Schema.ReadOnly.IsUndefined() &&
            draft202012Schema.RecursiveAnchor.IsUndefined() &&
            draft202012Schema.RecursiveRef.IsUndefined() &&
            draft202012Schema.DynamicRef.IsUndefined() &&
            draft202012Schema.Ref.IsUndefined() &&
            draft202012Schema.Required.IsUndefined() &&
            draft202012Schema.Then.IsUndefined() &&
            draft202012Schema.UnevaluatedItems.IsUndefined() &&
            draft202012Schema.UnevaluatedProperties.IsUndefined() &&
            draft202012Schema.UniqueItems.IsUndefined() &&
            draft202012Schema.WriteOnly.IsUndefined());
    }

    /// <summary>
    /// Determines if this schema is empty of non-extension items.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if all the non-extension items are empty.</returns>
    public static bool IsEmpty(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Comment.IsUndefined() &&
            draft202012Schema.PrefixItems.IsUndefined() &&
            draft202012Schema.AdditionalProperties.IsUndefined() &&
            draft202012Schema.AllOf.IsUndefined() &&
            draft202012Schema.Anchor.IsUndefined() &&
            draft202012Schema.AnyOf.IsUndefined() &&
            draft202012Schema.Const.IsUndefined() &&
            draft202012Schema.Contains.IsUndefined() &&
            draft202012Schema.ContentEncoding.IsUndefined() &&
            draft202012Schema.ContentMediaType.IsUndefined() &&
            draft202012Schema.ContentSchema.IsUndefined() &&
            draft202012Schema.Default.IsUndefined() &&
            draft202012Schema.Definitions.IsUndefined() &&
            draft202012Schema.Defs.IsUndefined() &&
            draft202012Schema.Deprecated.IsUndefined() &&
            draft202012Schema.Dependencies.IsUndefined() &&
            draft202012Schema.DependentRequired.IsUndefined() &&
            draft202012Schema.DependentSchemas.IsUndefined() &&
            draft202012Schema.Description.IsUndefined() &&
            draft202012Schema.Examples.IsUndefined() &&
            draft202012Schema.Else.IsUndefined() &&
            draft202012Schema.Enum.IsUndefined() &&
            draft202012Schema.ExclusiveMaximum.IsUndefined() &&
            draft202012Schema.ExclusiveMinimum.IsUndefined() &&
            draft202012Schema.Format.IsUndefined() &&
            draft202012Schema.Id.IsUndefined() &&
            draft202012Schema.If.IsUndefined() &&
            draft202012Schema.Items.IsUndefined() &&
            draft202012Schema.MaxContains.IsUndefined() &&
            draft202012Schema.Maximum.IsUndefined() &&
            draft202012Schema.MaxItems.IsUndefined() &&
            draft202012Schema.MaxLength.IsUndefined() &&
            draft202012Schema.MaxProperties.IsUndefined() &&
            draft202012Schema.MinContains.IsUndefined() &&
            draft202012Schema.Minimum.IsUndefined() &&
            draft202012Schema.MinItems.IsUndefined() &&
            draft202012Schema.MinLength.IsUndefined() &&
            draft202012Schema.MinProperties.IsUndefined() &&
            draft202012Schema.MultipleOf.IsUndefined() &&
            draft202012Schema.Not.IsUndefined() &&
            draft202012Schema.OneOf.IsUndefined() &&
            draft202012Schema.Pattern.IsUndefined() &&
            draft202012Schema.PatternProperties.IsUndefined() &&
            draft202012Schema.Properties.IsUndefined() &&
            draft202012Schema.PropertyNames.IsUndefined() &&
            draft202012Schema.ReadOnly.IsUndefined() &&
            draft202012Schema.RecursiveAnchor.IsUndefined() &&
            draft202012Schema.DynamicAnchor.IsUndefined() &&
            draft202012Schema.DynamicRef.IsUndefined() &&
            draft202012Schema.RecursiveRef.IsUndefined() &&
            draft202012Schema.Ref.IsUndefined() &&
            draft202012Schema.Required.IsUndefined() &&
            draft202012Schema.Then.IsUndefined() &&
            draft202012Schema.Title.IsUndefined() &&
            draft202012Schema.Type.IsUndefined() &&
            draft202012Schema.UnevaluatedItems.IsUndefined() &&
            draft202012Schema.UnevaluatedProperties.IsUndefined() &&
            draft202012Schema.UniqueItems.IsUndefined() &&
            draft202012Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked reference.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $ref and no other substantive properties.</returns>
    public static bool IsNakedReference(this Schema draft202012Schema)
    {
        return
            draft202012Schema.Ref.IsNotUndefined() &&
            draft202012Schema.PrefixItems.IsUndefined() &&
            draft202012Schema.AdditionalProperties.IsUndefined() &&
            draft202012Schema.AllOf.IsUndefined() &&
            draft202012Schema.DynamicAnchor.IsUndefined() &&
            draft202012Schema.Anchor.IsUndefined() &&
            draft202012Schema.AnyOf.IsUndefined() &&
            draft202012Schema.Const.IsUndefined() &&
            draft202012Schema.Contains.IsUndefined() &&
            draft202012Schema.ContentEncoding.IsUndefined() &&
            draft202012Schema.ContentMediaType.IsUndefined() &&
            draft202012Schema.ContentSchema.IsUndefined() &&
            draft202012Schema.Default.IsUndefined() &&
            draft202012Schema.Dependencies.IsUndefined() &&
            draft202012Schema.DependentRequired.IsUndefined() &&
            draft202012Schema.DependentSchemas.IsUndefined() &&
            draft202012Schema.Else.IsUndefined() &&
            draft202012Schema.Enum.IsUndefined() &&
            draft202012Schema.ExclusiveMaximum.IsUndefined() &&
            draft202012Schema.ExclusiveMinimum.IsUndefined() &&
            draft202012Schema.Format.IsUndefined() &&
            draft202012Schema.Id.IsUndefined() &&
            draft202012Schema.If.IsUndefined() &&
            draft202012Schema.Items.IsUndefined() &&
            draft202012Schema.MaxContains.IsUndefined() &&
            draft202012Schema.Maximum.IsUndefined() &&
            draft202012Schema.MaxItems.IsUndefined() &&
            draft202012Schema.MaxLength.IsUndefined() &&
            draft202012Schema.MaxProperties.IsUndefined() &&
            draft202012Schema.MinContains.IsUndefined() &&
            draft202012Schema.Minimum.IsUndefined() &&
            draft202012Schema.MinItems.IsUndefined() &&
            draft202012Schema.MinLength.IsUndefined() &&
            draft202012Schema.MinProperties.IsUndefined() &&
            draft202012Schema.MultipleOf.IsUndefined() &&
            draft202012Schema.Not.IsUndefined() &&
            draft202012Schema.OneOf.IsUndefined() &&
            draft202012Schema.Pattern.IsUndefined() &&
            draft202012Schema.PatternProperties.IsUndefined() &&
            draft202012Schema.Properties.IsUndefined() &&
            draft202012Schema.PropertyNames.IsUndefined() &&
            draft202012Schema.ReadOnly.IsUndefined() &&
            draft202012Schema.DynamicRef.IsUndefined() &&
            draft202012Schema.RecursiveRef.IsUndefined() &&
            draft202012Schema.Required.IsUndefined() &&
            draft202012Schema.Then.IsUndefined() &&
            draft202012Schema.Type.IsUndefined() &&
            draft202012Schema.UnevaluatedItems.IsUndefined() &&
            draft202012Schema.UnevaluatedProperties.IsUndefined() &&
            draft202012Schema.UniqueItems.IsUndefined() &&
            draft202012Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked recursive reference.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $recursiveRef and no other substantive properties.</returns>
    public static bool IsNakedRecursiveReference(this Schema draft202012Schema)
    {
        return
            draft202012Schema.RecursiveRef.IsNotUndefined() &&
            draft202012Schema.PrefixItems.IsUndefined() &&
            draft202012Schema.AdditionalProperties.IsUndefined() &&
            draft202012Schema.AllOf.IsUndefined() &&
            draft202012Schema.Anchor.IsUndefined() &&
            draft202012Schema.DynamicAnchor.IsUndefined() &&
            draft202012Schema.AnyOf.IsUndefined() &&
            draft202012Schema.Const.IsUndefined() &&
            draft202012Schema.Contains.IsUndefined() &&
            draft202012Schema.ContentEncoding.IsUndefined() &&
            draft202012Schema.ContentMediaType.IsUndefined() &&
            draft202012Schema.ContentSchema.IsUndefined() &&
            draft202012Schema.Default.IsUndefined() &&
            draft202012Schema.Dependencies.IsUndefined() &&
            draft202012Schema.DependentRequired.IsUndefined() &&
            draft202012Schema.DependentSchemas.IsUndefined() &&
            draft202012Schema.Else.IsUndefined() &&
            draft202012Schema.Enum.IsUndefined() &&
            draft202012Schema.ExclusiveMaximum.IsUndefined() &&
            draft202012Schema.ExclusiveMinimum.IsUndefined() &&
            draft202012Schema.Format.IsUndefined() &&
            draft202012Schema.Id.IsUndefined() &&
            draft202012Schema.If.IsUndefined() &&
            draft202012Schema.Items.IsUndefined() &&
            draft202012Schema.MaxContains.IsUndefined() &&
            draft202012Schema.Maximum.IsUndefined() &&
            draft202012Schema.MaxItems.IsUndefined() &&
            draft202012Schema.MaxLength.IsUndefined() &&
            draft202012Schema.MaxProperties.IsUndefined() &&
            draft202012Schema.MinContains.IsUndefined() &&
            draft202012Schema.Minimum.IsUndefined() &&
            draft202012Schema.MinItems.IsUndefined() &&
            draft202012Schema.MinLength.IsUndefined() &&
            draft202012Schema.MinProperties.IsUndefined() &&
            draft202012Schema.MultipleOf.IsUndefined() &&
            draft202012Schema.Not.IsUndefined() &&
            draft202012Schema.OneOf.IsUndefined() &&
            draft202012Schema.Pattern.IsUndefined() &&
            draft202012Schema.PatternProperties.IsUndefined() &&
            draft202012Schema.Properties.IsUndefined() &&
            draft202012Schema.PropertyNames.IsUndefined() &&
            draft202012Schema.ReadOnly.IsUndefined() &&
            draft202012Schema.Ref.IsUndefined() &&
            draft202012Schema.DynamicRef.IsUndefined() &&
            draft202012Schema.Required.IsUndefined() &&
            draft202012Schema.Then.IsUndefined() &&
            draft202012Schema.Type.IsUndefined() &&
            draft202012Schema.UnevaluatedItems.IsUndefined() &&
            draft202012Schema.UnevaluatedProperties.IsUndefined() &&
            draft202012Schema.UniqueItems.IsUndefined() &&
            draft202012Schema.WriteOnly.IsUndefined();
    }

    /// <summary>
    /// Determines if this schema is a naked recursive reference.
    /// </summary>
    /// <param name="draft202012Schema">The schema to test.</param>
    /// <returns><c>True</c> if the schema has a $recursiveRef and no other substantive properties.</returns>
    public static bool IsNakedDynamicReference(this Schema draft202012Schema)
    {
        return
            draft202012Schema.DynamicRef.IsNotUndefined() &&
            draft202012Schema.DynamicAnchor.IsUndefined() &&
            draft202012Schema.PrefixItems.IsUndefined() &&
            draft202012Schema.AdditionalProperties.IsUndefined() &&
            draft202012Schema.AllOf.IsUndefined() &&
            draft202012Schema.Anchor.IsUndefined() &&
            draft202012Schema.AnyOf.IsUndefined() &&
            draft202012Schema.Const.IsUndefined() &&
            draft202012Schema.Contains.IsUndefined() &&
            draft202012Schema.ContentEncoding.IsUndefined() &&
            draft202012Schema.ContentMediaType.IsUndefined() &&
            draft202012Schema.ContentSchema.IsUndefined() &&
            draft202012Schema.Default.IsUndefined() &&
            draft202012Schema.Dependencies.IsUndefined() &&
            draft202012Schema.DependentRequired.IsUndefined() &&
            draft202012Schema.DependentSchemas.IsUndefined() &&
            draft202012Schema.Else.IsUndefined() &&
            draft202012Schema.Enum.IsUndefined() &&
            draft202012Schema.ExclusiveMaximum.IsUndefined() &&
            draft202012Schema.ExclusiveMinimum.IsUndefined() &&
            draft202012Schema.Format.IsUndefined() &&
            draft202012Schema.Id.IsUndefined() &&
            draft202012Schema.If.IsUndefined() &&
            draft202012Schema.Items.IsUndefined() &&
            draft202012Schema.MaxContains.IsUndefined() &&
            draft202012Schema.Maximum.IsUndefined() &&
            draft202012Schema.MaxItems.IsUndefined() &&
            draft202012Schema.MaxLength.IsUndefined() &&
            draft202012Schema.MaxProperties.IsUndefined() &&
            draft202012Schema.MinContains.IsUndefined() &&
            draft202012Schema.Minimum.IsUndefined() &&
            draft202012Schema.MinItems.IsUndefined() &&
            draft202012Schema.MinLength.IsUndefined() &&
            draft202012Schema.MinProperties.IsUndefined() &&
            draft202012Schema.MultipleOf.IsUndefined() &&
            draft202012Schema.Not.IsUndefined() &&
            draft202012Schema.OneOf.IsUndefined() &&
            draft202012Schema.Pattern.IsUndefined() &&
            draft202012Schema.PatternProperties.IsUndefined() &&
            draft202012Schema.Properties.IsUndefined() &&
            draft202012Schema.PropertyNames.IsUndefined() &&
            draft202012Schema.ReadOnly.IsUndefined() &&
            draft202012Schema.Ref.IsUndefined() &&
            draft202012Schema.RecursiveRef.IsUndefined() &&
            draft202012Schema.Required.IsUndefined() &&
            draft202012Schema.Then.IsUndefined() &&
            draft202012Schema.Type.IsUndefined() &&
            draft202012Schema.UnevaluatedItems.IsUndefined() &&
            draft202012Schema.UnevaluatedProperties.IsUndefined() &&
            draft202012Schema.UniqueItems.IsUndefined() &&
            draft202012Schema.WriteOnly.IsUndefined();
    }
}
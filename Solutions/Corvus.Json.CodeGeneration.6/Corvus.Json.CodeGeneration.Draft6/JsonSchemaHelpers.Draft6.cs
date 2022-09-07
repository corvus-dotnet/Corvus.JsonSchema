// <copyright file="JsonSchemaHelpers.Draft6.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Draft6;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft Draft6 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseDraft6(this JsonSchemaTypeBuilder builder)
    {
        builder.ValidatingAs = ValidationSemantics.Draft6;
        builder.AnchorKeywords = CreateDraft6AnchorKeywords();
        builder.IdKeyword = CreateDraft6IdKeyword();
        builder.ItemsKeyword = CreateDraft6ItemsKeyword();
        builder.SchemaKeyword = CreateDraft6SchemaKeyword();
        builder.IrreducibleKeywords = CreateDraft6IrreducibleKeywords();
        builder.RefKeywords = CreateDraft6RefKeywords();
        builder.RefResolvableKeywords = CreateDraft6RefResolvableKeywords();
        builder.ValidateSchema = CreateDraft6ValidateSchema();
        builder.GetBuiltInTypeName = CreateDraft6GetBuiltInTypeNameFunction();
        builder.IsExplicitArrayType = CreateDraft6IsExplicitArrayType();
        builder.IsSimpleType = CreateDraft6IsSimpleType();
        builder.FindAndBuildPropertiesAdapter = CreateDraft6FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the draft7 items keyword.
    /// </summary>
    /// <returns>The items keyword.</returns>
    private static string CreateDraft6ItemsKeyword()
    {
        return "items";
    }

    /// <summary>
    /// Creates the list of draft7 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    private static ImmutableArray<AnchorKeyword> CreateDraft6AnchorKeywords()
    {
        return ImmutableArray<AnchorKeyword>.Empty;
    }

    /// <summary>
    /// Gets the draft7 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    private static string CreateDraft6IdKeyword()
    {
        return "$id";
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    private static string CreateDraft6SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the draft7 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    private static ImmutableHashSet<string> CreateDraft6DefsKeywords()
    {
        return ImmutableHashSet.Create("definitions");
    }

    /// <summary>
    /// Gets a hashset of keywords that have semantic effect for type declarations.
    /// </summary>
    /// <returns>
    /// A list of the keywords that, if applied alongside a reference
    /// keyword, mean that the type cannot by reduced to the referenced type.
    /// </returns>
    private static ImmutableHashSet<string> CreateDraft6IrreducibleKeywords()
    {
        // $ref always reduces in draft7.
        return ImmutableHashSet<string>.Empty;
    }

    /// <summary>
    /// Creates the draft2019-09 keywords that are resolvable to a schema.
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    private static ImmutableArray<RefResolvableKeyword> CreateDraft6RefResolvableKeywords()
    {
        return ImmutableArray.Create<RefResolvableKeyword>(
            new("definitions", RefResolvablePropertyKind.MapOfSchema),
            new("items", RefResolvablePropertyKind.SchemaOrArrayOfSchema),
            new("contains", RefResolvablePropertyKind.Schema),
            new("prefixItems", RefResolvablePropertyKind.ArrayOfSchema),
            new("patternProperties", RefResolvablePropertyKind.MapOfSchema),
            new("properties", RefResolvablePropertyKind.MapOfSchema),
            new("additionalProperties", RefResolvablePropertyKind.Schema),
            new("additionalItems", RefResolvablePropertyKind.Schema),
            new("dependencies", RefResolvablePropertyKind.MapOfSchemaIfValueIsSchemaLike),
            new("propertyNames", RefResolvablePropertyKind.Schema),
            new("allOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("anyOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("oneOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("not", RefResolvablePropertyKind.Schema),
            new("contentSchema", RefResolvablePropertyKind.Schema),
            new("unevaluatedItems", RefResolvablePropertyKind.Schema),
            new("unevaluatedProperties", RefResolvablePropertyKind.Schema));
    }

    /// <summary>
    /// Creates the draft7 reference keywords.
    /// </summary>
    /// <returns>An array of <see cref="RefKeyword"/> instances.</returns>
    private static ImmutableArray<RefKeyword> CreateDraft6RefKeywords()
    {
        return ImmutableArray.Create(
            new RefKeyword("$ref", RefKind.Ref));
    }

    /// <summary>
    /// Creates the predicate that validates a schema against draft 7 metaschema.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a valid draft 7 schema.</returns>
    private static Predicate<JsonAny> CreateDraft6ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft6.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is an explicit array type.</returns>
    private static Predicate<JsonAny> CreateDraft6IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft6.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determiens whether this schema represents a simple type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a simple type.</returns>
    private static Predicate<JsonAny> CreateDraft6IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft6.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>The dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    private static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft6GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.Draft6.Schema schema = schemaAny.As<JsonSchema.Draft6.Schema>();

            if (!schema.IsBuiltInType())
            {
                return null;
            }

            if (schema.ValueKind == JsonValueKind.True)
            {
                return BuiltInTypes.AnyTypeDeclaration;
            }
            else if (schema.ValueKind == JsonValueKind.False)
            {
                return BuiltInTypes.NotAnyTypeDeclaration;
            }
            else if (schema.IsEmpty())
            {
                return BuiltInTypes.AnyTypeDeclaration;
            }

            return BuiltInTypes.GetTypeNameFor(
                schema.Type.AsSimpleTypesEntity,
                schema.Format.AsOptionalString(),
                schema.GetContentEncoding(),
                schema.GetContentMediaType(),
                (validateAs & ValidationSemantics.Draft201909) != 0);
        };
    }

    /// <summary>
    /// Creates the function that builds the dotnet properties for the type declaration.
    /// </summary>
    /// <returns>An action that adds the properties to the given type declaration.</returns>
    private static Action<JsonSchemaTypeBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft6FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.Draft6.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.Draft6.Schema>();

            // First we add the 'required' properties as JsonAny; they will be overridden if we have explicit implementations
            // elsewhere
            if (schema.Required.IsNotUndefined())
            {
                foreach (JsonString requiredName in schema.Required.EnumerateArray())
                {
                    target.AddOrReplaceProperty(new PropertyDeclaration(builder.AnyTypeDeclarationInstance, requiredName, !treatRequiredAsOptional, source == target, false, null));
                }
            }

            if (schema.AllOf.IsNotUndefined())
            {
                foreach (TypeDeclaration allOfTypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/allOf")).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(allOfTypeDeclaration, target, typesVisited, treatRequiredAsOptional);
                }
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/then", out TypeDeclaration? thenTypeDeclaration))
            {
                builder.FindAndBuildProperties(thenTypeDeclaration, target, typesVisited, true);
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/else", out TypeDeclaration? elseTypeDeclaration))
            {
                builder.FindAndBuildProperties(elseTypeDeclaration, target, typesVisited, true);
            }

            if (schema.Dependencies.IsNotUndefined())
            {
                foreach (TypeDeclaration dependentypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/dependencies")).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(dependentypeDeclaration, target, typesVisited, true);
                }
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/$ref", out TypeDeclaration? refTypeDeclaration))
            {
                builder.FindAndBuildProperties(refTypeDeclaration, target, typesVisited, false);
            }

            // Then we add our own properties.
            if (schema.Properties.IsNotUndefined())
            {
                JsonReference propertyRef = new("#/properties");

                foreach (JsonObjectProperty property in schema.Properties.EnumerateObject())
                {
                    JsonPropertyName propertyName = property.Name;
                    bool isRequired = false;

                    if (schema.Required.IsNotUndefined())
                    {
                        if (schema.Required.EnumerateArray().Any(r => propertyName == Uri.UnescapeDataString(r)))
                        {
                            isRequired = !treatRequiredAsOptional;
                        }
                    }

                    if (source.RefResolvablePropertyDeclarations.TryGetValue(propertyRef.AppendUnencodedPropertyNameToFragment(property.Name), out TypeDeclaration? propertyTypeDeclaration))
                    {
                        target.AddOrReplaceProperty(new PropertyDeclaration(propertyTypeDeclaration, propertyName, isRequired, source == target, propertyTypeDeclaration.Schema().Default.IsNotUndefined(), propertyTypeDeclaration.Schema().Default is JsonAny def ? def.ToString() : default));
                    }
                }
            }
        };
    }
}
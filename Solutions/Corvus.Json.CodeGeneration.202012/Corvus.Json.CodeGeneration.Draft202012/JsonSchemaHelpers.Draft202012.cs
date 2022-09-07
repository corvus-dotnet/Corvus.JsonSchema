// <copyright file="JsonSchemaHelpers.Draft202012.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Draft202012;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft 2020-12 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseDraft202012(this JsonSchemaTypeBuilder builder)
    {
        builder.ValidatingAs = ValidationSemantics.Draft202012;
        builder.AnchorKeywords = CreateDraft202012AnchorKeywords();
        builder.IdKeyword = CreateDraft202012IdKeyword();
        builder.ItemsKeyword = CreateDraft202012ItemsKeyword();
        builder.SchemaKeyword = CreateDraft202012SchemaKeyword();
        builder.DefinitionKeywords = CreateDraft202012DefsKeywords();
        builder.IrreducibleKeywords = CreateDraft202012IrreducibleKeywords();
        builder.RefKeywords = CreateDraft202012RefKeywords();
        builder.RefResolvableKeywords = CreateDraft202012RefResolvableKeywords();
        builder.ValidateSchema = CreateDraft202012ValidateSchema();
        builder.GetBuiltInTypeName = CreateDraft202012GetBuiltInTypeNameFunction();
        builder.IsExplicitArrayType = CreateDraft202012IsExplicitArrayType();
        builder.IsSimpleType = CreateDraft202012IsSimpleType();
        builder.FindAndBuildPropertiesAdapter = CreateDraft202012FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the draft2020-12 items keyword.
    /// </summary>
    /// <returns>The items keyword.</returns>
    private static string CreateDraft202012ItemsKeyword()
    {
        return "items";
    }

    /// <summary>
    /// Creates the list of draft2020-12 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    private static ImmutableArray<AnchorKeyword> CreateDraft202012AnchorKeywords()
    {
        return ImmutableArray.Create(
            new AnchorKeyword(Name: "$anchor", IsDynamic: false, IsRecursive: false),
            new AnchorKeyword(Name: "$dynamicAnchor", IsDynamic: true, IsRecursive: false));
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    private static string CreateDraft202012IdKeyword()
    {
        return "$id";
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    private static ImmutableHashSet<string> CreateDraft202012DefsKeywords()
    {
        return ImmutableHashSet.Create("$defs");
    }

    /// <summary>
    /// Gets a hashset of keywords that have semantic effect for type declarations.
    /// </summary>
    /// <returns>
    /// A list of the keywords that, if applied alongside a reference
    /// keyword, mean that the type cannot by reduced to the referenced type.
    /// </returns>
    private static ImmutableHashSet<string> CreateDraft202012IrreducibleKeywords()
    {
        return ImmutableHashSet.Create(
            "additionalProperties",
            "allOf",
            "anyOf",
            "const",
            "contains",
            "contentEncoding",
            "contentMediaType",
            "contentSchema",
            "default",
            "dependentRequired",
            "dependentSchemas",
            "else",
            "enum",
            "exclusiveMaximum",
            "exclusiveMinimum",
            "format",
            "if",
            "items",
            "maxContains",
            "minContains",
            "maxItems",
            "maxLength",
            "maxProperties",
            "minContains",
            "minimum",
            "minItems",
            "minLength",
            "minProperties",
            "multipleOf",
            "not",
            "oneOf",
            "pattern",
            "patternProperties",
            "prefixItems",
            "properties",
            "propertyNames",
            "required",
            "then",
            "type",
            "unevaluatedItems",
            "unevaluatedProperties",
            "uniqueItems");
    }

    /// <summary>
    /// Creates the draft2020-12 keywords that are resolvable to a Schema().
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    private static ImmutableArray<RefResolvableKeyword> CreateDraft202012RefResolvableKeywords()
    {
        return ImmutableArray.Create<RefResolvableKeyword>(
            new("$defs", RefResolvablePropertyKind.MapOfSchema),
            new("items", RefResolvablePropertyKind.SchemaOrArrayOfSchema),
            new("contains", RefResolvablePropertyKind.Schema),
            new("if", RefResolvablePropertyKind.Schema),
            new("prefixItems", RefResolvablePropertyKind.ArrayOfSchema),
            new("patternProperties", RefResolvablePropertyKind.MapOfSchema),
            new("properties", RefResolvablePropertyKind.MapOfSchema),
            new("additionalProperties", RefResolvablePropertyKind.Schema),
            new("dependentSchemas", RefResolvablePropertyKind.MapOfSchema),
            new("else", RefResolvablePropertyKind.Schema),
            new("then", RefResolvablePropertyKind.Schema),
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
    /// Creates the draft2020-12 reference keywords.
    /// </summary>
    /// <returns>An array of <see cref="RefKeyword"/> instances.</returns>
    private static ImmutableArray<RefKeyword> CreateDraft202012RefKeywords()
    {
        return ImmutableArray.Create(
            new RefKeyword("$ref", RefKind.Ref),
            new RefKeyword("$recursiveRef", RefKind.RecursiveRef),
            new RefKeyword("$dynamicRef", RefKind.DynamicRef));
    }

    /// <summary>
    /// Creates the predicate that validates a Schema() against draft 2020-12 metaSchema().
    /// </summary>
    /// <returns><see langword="true"/> if the Schema() is a valid draft 2020-12 Schema().</returns>
    private static Predicate<JsonAny> CreateDraft202012ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft202012.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this Schema() represents an explicit array type.
    /// </summary>
    /// <returns><see langword="true"/> if the Schema() is an explicit array type.</returns>
    private static Predicate<JsonAny> CreateDraft202012IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft202012.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determiens whether this Schema() represents a simple type.
    /// </summary>
    /// <returns><see langword="true"/> if the Schema() is a simple type.</returns>
    private static Predicate<JsonAny> CreateDraft202012IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft202012.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>The dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    private static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft202012GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.Draft202012.Schema schema = schemaAny.As<JsonSchema.Draft202012.Schema>();

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
                schema.ContentEncoding.AsOptionalString(),
                schema.ContentMediaType.AsOptionalString(),
                (validateAs & ValidationSemantics.Draft201909) != 0);
        };
    }

    /// <summary>
    /// Creates the function that builds the dotnet properties for the type declaration.
    /// </summary>
    /// <returns>An action that adds the properties to the given type declaration.</returns>
    private static Action<JsonSchemaTypeBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft202012FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.Draft202012.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.Draft202012.Schema>();

            // First we add the 'required' properties as JsonAny; they will be overridden if we have explicit implementations
            // elsewhere
            if (schema.Required.IsNotUndefined())
            {
                foreach (JsonString requiredName in schema.Required.EnumerateArray())
                {
                    target.AddOrReplaceProperty(new PropertyDeclaration(builder.AnyTypeDeclarationInstance, Uri.UnescapeDataString(requiredName), !treatRequiredAsOptional, source == target, false, null));
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

            if (schema.DependentSchemas.IsNotUndefined())
            {
                foreach (TypeDeclaration dependentypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/dependentSchemas")).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(dependentypeDeclaration, target, typesVisited, true);
                }
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/$ref", out TypeDeclaration? refTypeDeclaration))
            {
                builder.FindAndBuildProperties(refTypeDeclaration, target, typesVisited, false);
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/$recursiveRef", out TypeDeclaration? recursiveRefTypeDeclaration))
            {
                builder.FindAndBuildProperties(recursiveRefTypeDeclaration, target, typesVisited, false);
            }

            if (source.RefResolvablePropertyDeclarations.TryGetValue("#/$dynamicRef", out TypeDeclaration? dynamicRefTypeDeclaration))
            {
                builder.FindAndBuildProperties(dynamicRefTypeDeclaration, target, typesVisited, false);
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

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    private static string CreateDraft202012SchemaKeyword()
    {
        return "$schema";
    }
}
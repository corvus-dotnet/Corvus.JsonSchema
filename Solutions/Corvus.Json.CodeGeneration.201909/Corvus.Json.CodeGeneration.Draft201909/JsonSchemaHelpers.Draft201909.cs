// <copyright file="JsonSchemaHelpers.Draft201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Draft201909;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft 2019-09 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseDraft201909(this JsonSchemaTypeBuilder builder)
    {
        builder.ValidatingAs = ValidationSemantics.Draft201909;
        builder.AnchorKeywords = CreateDraft201909AnchorKeywords();
        builder.IdKeyword = CreateDraft201909IdKeyword();
        builder.SchemaKeyword = CreateDraft201909SchemaKeyword();
        builder.IrreducibleKeywords = CreateDraft201909IrreducibleKeywords();
        builder.RefKeywords = CreateDraft201909RefKeywords();
        builder.RefResolvableKeywords = CreateDraft201909RefResolvableKeywords();
        builder.ValidateSchema = CreateDraft201909ValidateSchema();
        builder.GetBuiltInTypeName = CreateDraft201909GetBuiltInTypeNameFunction();
        builder.IsExplicitArrayType = CreateDraft201909IsExplicitArrayType();
        builder.IsSimpleType = CreateDraft201909IsSimpleType();
        builder.FindAndBuildPropertiesAdapter = CreateDraft201909FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the list of draft2020-12 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    internal static ImmutableArray<AnchorKeyword> CreateDraft201909AnchorKeywords()
    {
        return ImmutableArray.Create(
            new AnchorKeyword(Name: "$anchor", IsDynamic: false, IsRecursive: false),
            new AnchorKeyword(Name: "recursiveAnchor", IsDynamic: true, IsRecursive: false));
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    internal static string CreateDraft201909IdKeyword()
    {
        return "$id";
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    internal static string CreateDraft201909SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    internal static ImmutableHashSet<string> CreateDraft201909DefsKeywords()
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
    internal static ImmutableHashSet<string> CreateDraft201909IrreducibleKeywords()
    {
        return ImmutableHashSet.Create(
            "additionalProperties",
            "additionalItems",
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
    /// Creates the draft2020-12 keywords that are resolvable to a schema.
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    internal static ImmutableArray<RefResolvableKeyword> CreateDraft201909RefResolvableKeywords()
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
            new("additionalItems", RefResolvablePropertyKind.Schema),
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
    internal static ImmutableArray<RefKeyword> CreateDraft201909RefKeywords()
    {
        return ImmutableArray.Create(
            new RefKeyword("$ref", RefKind.Ref),
            new RefKeyword("$recursiveRef", RefKind.RecursiveRef));
    }

    /// <summary>
    /// Creates the predicate that validates a schema against draft 2019-09 metaschema.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is a valid draft 2019-09 schema.</returns>
    internal static Predicate<JsonAny> CreateDraft201909ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is an explicit array type.</returns>
    internal static Predicate<JsonAny> CreateDraft201909IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determiens whether this schema represents a simple type.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is a simple type.</returns>
    internal static Predicate<JsonAny> CreateDraft201909IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>A function that provides the dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    internal static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft201909GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.Draft201909.Schema schema = schemaAny.As<JsonSchema.Draft201909.Schema>();

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
    internal static Action<JsonSchemaTypeBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft201909FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.Draft201909.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.Draft201909.Schema>();

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
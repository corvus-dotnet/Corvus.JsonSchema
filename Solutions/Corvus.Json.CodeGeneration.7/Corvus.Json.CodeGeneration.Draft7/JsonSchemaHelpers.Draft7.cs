// <copyright file="JsonSchemaHelpers.Draft7.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Draft7;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft Draft7 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseDraft7(this JsonSchemaTypeBuilder builder)
    {
        builder.ValidatingAs = ValidationSemantics.Draft7;
        builder.AnchorKeywords = CreateDraft7AnchorKeywords();
        builder.IdKeyword = CreateDraft7IdKeyword();
        builder.SchemaKeyword = CreateDraft7SchemaKeyword();
        builder.IrreducibleKeywords = CreateDraft7IrreducibleKeywords();
        builder.RefKeywords = CreateDraft7RefKeywords();
        builder.RefResolvableKeywords = CreateDraft7RefResolvableKeywords();
        builder.ValidateSchema = CreateDraft7ValidateSchema();
        builder.GetBuiltInTypeName = CreateDraft7GetBuiltInTypeNameFunction();
        builder.IsExplicitArrayType = CreateDraft7IsExplicitArrayType();
        builder.IsSimpleType = CreateDraft7IsSimpleType();
        builder.FindAndBuildPropertiesAdapter = CreateDraft7FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the list of draft2020-12 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    internal static ImmutableArray<AnchorKeyword> CreateDraft7AnchorKeywords()
    {
        return ImmutableArray.Create(
            new AnchorKeyword(Name: "$anchor", IsDynamic: false, IsRecursive: false));
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    internal static string CreateDraft7IdKeyword()
    {
        return "$id";
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    internal static string CreateDraft7SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the draft2020-12 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    internal static ImmutableHashSet<string> CreateDraft7DefsKeywords()
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
    internal static ImmutableHashSet<string> CreateDraft7IrreducibleKeywords()
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
    /// Creates the draft2020-12 keywords that are resolvable to a schema.
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    internal static ImmutableArray<RefResolvableKeyword> CreateDraft7RefResolvableKeywords()
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
    internal static ImmutableArray<RefKeyword> CreateDraft7RefKeywords()
    {
        return ImmutableArray.Create(
            new RefKeyword("$ref", RefKind.Ref),
            new RefKeyword("$recursiveRef", RefKind.RecursiveRef),
            new RefKeyword("$dynamicRef", RefKind.DynamicRef));
    }

    /// <summary>
    /// Creates the predicate that validates a schema against draft 7 metaschema.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a valid draft 7 schema.</returns>
    internal static Predicate<JsonAny> CreateDraft7ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft7.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is an explicit array type.</returns>
    internal static Predicate<JsonAny> CreateDraft7IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft7.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determiens whether this schema represents a simple type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a simple type.</returns>
    internal static Predicate<JsonAny> CreateDraft7IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft7.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>The dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    internal static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft7GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.Draft7.Schema schema = schemaAny.As<JsonSchema.Draft7.Schema>();

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
    internal static Action<JsonSchemaTypeBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft7FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.Draft7.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.Draft7.Schema>();

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
// <copyright file="JsonSchemaHelpers.Draft201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.Draft201909;

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
        JsonSchemaConfiguration configuration = builder.JsonSchemaConfiguration;
        configuration.ValidatingAs = ValidationSemantics.Draft201909;
        configuration.AnchorKeywords = CreateDraft201909AnchorKeywords();
        configuration.IdKeyword = CreateDraft201909IdKeyword();
        configuration.DefinitionKeywords = CreateDraft201909DefsKeywords();
        configuration.ItemsKeyword = CreateDraft201909ItemsKeyword();
        configuration.SchemaKeyword = CreateDraft201909SchemaKeyword();
        configuration.IrreducibleKeywords = CreateDraft201909IrreducibleKeywords();
        configuration.GeneratorReservedWords = CreateDraft201909GeneratorReservedWords();
        configuration.RefKeywords = CreateDraft201909RefKeywords();
        configuration.RefResolvableKeywords = CreateDraft201909RefResolvableKeywords();
        configuration.ValidateSchema = CreateDraft201909ValidateSchema();
        configuration.GetBuiltInTypeName = CreateDraft201909GetBuiltInTypeNameFunction();
        configuration.IsExplicitArrayType = CreateDraft201909IsExplicitArrayType();
        configuration.IsSimpleType = CreateDraft201909IsSimpleType();
        configuration.FindAndBuildPropertiesAdapter = CreateDraft201909FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the draft2019-09 items keyword.
    /// </summary>
    /// <returns>The items keyword.</returns>
    private static string CreateDraft201909ItemsKeyword()
    {
        return "items";
    }

    /// <summary>
    /// Creates the list of draft2019-09 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    private static ImmutableArray<AnchorKeyword> CreateDraft201909AnchorKeywords()
    {
        return
        [
#if NET8_0_OR_GREATER
            new AnchorKeyword(Name: "$anchor", IsDynamic: false, IsRecursive: false),
            new AnchorKeyword(Name: "$recursiveAnchor", IsDynamic: false, IsRecursive: true),
#else
            new AnchorKeyword(name: "$anchor", isDynamic: false, isRecursive: false),
            new AnchorKeyword(name: "$recursiveAnchor", isDynamic: false, isRecursive: true),
#endif
        ];
    }

    /// <summary>
    /// Gets the draft2019-09 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    private static string CreateDraft201909IdKeyword()
    {
        return "$id";
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    private static string CreateDraft201909SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the draft2019-09 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    private static ImmutableHashSet<string> CreateDraft201909DefsKeywords()
    {
        return
        [
            "$defs",
        ];
    }

    /// <summary>
    /// Gets a hashset of keywords that have semantic effect for type declarations.
    /// </summary>
    /// <returns>
    /// A list of the keywords that, if applied alongside a reference
    /// keyword, mean that the type cannot by reduced to the referenced type.
    /// </returns>
    private static ImmutableHashSet<string> CreateDraft201909IrreducibleKeywords()
    {
        return
        [
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
            "dependencies",
            "else",
            "enum",
            "exclusiveMaximum",
            "exclusiveMinimum",
            "format",
            "$id",
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
            "uniqueItems",
        ];
    }

    /// <summary>
    /// These are the words used in the code generator that cannot be used for types.
    /// </summary>
    /// <returns>The immutable set of reserved words.</returns>
    private static ImmutableHashSet<string> CreateDraft201909GeneratorReservedWords()
    {
        return
        [
            "SchemaLocation",
            "Item",
            "Add",
            "AddRange",
            "Insert",
            "InsertRange",
            "Replace",
            "SetItem",
            "Remove",
            "RemoveAt",
            "RemoveRange",
            "EmptyArray",
            "FromItems",
            "From",
            "FromRange",
            "AsImmutableList",
            "AsImmutableListBuilder",
            "GetArrayLength",
            "EnumerateArray",
            "GetImmutableList",
            "GetImmutableListBuilder",
            "GetImmutableListSetting",
            "GetImmutableListReplacing",
            "GetImmutableListWithout",
            "GetImmutableListWithoutRange",
            "GetImmutableListWith",
            "__CorvusConstValue",
            "__Corvus_Minimum",
            "__Corvus_Maximum",
            "__Corvus_ExclusiveMaximum",
            "__Corvus_ExclusiveMinimum",
            "__Corvus_MultipleOf",
            "ConstInstance",
            "__CorvusDefaults",
            "TryGetDefault",
            "HasDefault",
            "BuildDefaults",
            "__CorvusDependentRequired",
            "__CorvusDependency",
            "__TryGetCorvusDependentSchemaValidator",
            "Encoding",
            "EnumValues",
            "FromProperties",
            "EnumerateObject",
            "HasProperties",
            "HasProperty",
            "TryGetProperty",
            "SetProperty",
            "RemoveProperty",
            "JsonPropertyNames",
            "GetPropertyBacking",
            "GetPropertyBackingWithout",
            "GetPropertyBackingWith",
            "GetPropertyBackingBuilder",
            "GetPropertyBackingBuilderWithout",
            "__CorvusPatternExpression",
            "__CorvusPatternProperties",
            "CreatePatternPropertiesValidators",
            "__TryGetCorvusLocalPropertiesValidator",
            "Create",
            "TryGetString",
            "AsSpan",
            "AsOptionalString",
            "EqualsUtf8Bytes",
            "EqualsString",
            "Null",
            "Undefined",
            "AsAny",
            "AsJsonElement",
            "AsString",
            "AsBoolean",
            "AsNumber",
            "AsObject",
            "AsArray",
            "HasJsonElementBacking",
            "HasDotnetBacking",
            "ValueKind",
            "FromAny",
            "FromJson",
            "FromBoolean",
            "FromString",
            "FromNumber",
            "FromArray",
            "FromObject",
            "Match",
            "Parse",
            "As",
            "Equals",
            "WriteTo",
            "GetHashCode",
            "ToString",
            "ValidateAllOf",
            "ValidateAnyOf",
            "ValidateArray",
            "ValidateFormat",
            "ValidateIfThenElse",
            "ValidateMediaTypeAndEncoding",
            "ValidateNot",
            "ValidateObject",
            "ValidateOneOf",
            "ValidateRef",
            "Validate",
            "ValidateType",
        ];
    }

    /// <summary>
    /// Creates the draft2019-09 keywords that are resolvable to a schema.
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    private static ImmutableArray<RefResolvableKeyword> CreateDraft201909RefResolvableKeywords()
    {
        return
        [
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
            new("dependencies", RefResolvablePropertyKind.MapOfSchemaIfValueIsSchemaLike),
            new("else", RefResolvablePropertyKind.Schema),
            new("then", RefResolvablePropertyKind.Schema),
            new("propertyNames", RefResolvablePropertyKind.Schema),
            new("allOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("anyOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("oneOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("not", RefResolvablePropertyKind.Schema),
            new("contentSchema", RefResolvablePropertyKind.Schema),
            new("unevaluatedItems", RefResolvablePropertyKind.Schema),
            new("unevaluatedProperties", RefResolvablePropertyKind.Schema),
        ];
    }

    /// <summary>
    /// Creates the draft2019-09 reference keywords.
    /// </summary>
    /// <returns>An array of <see cref="RefKeyword"/> instances.</returns>
    private static ImmutableArray<RefKeyword> CreateDraft201909RefKeywords()
    {
        return
        [
            new RefKeyword("$ref", RefKind.Ref),
            new RefKeyword("$recursiveRef", RefKind.RecursiveRef),
        ];
    }

    /// <summary>
    /// Creates the predicate that validates a schema against draft 2019-09 metaschema.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is a valid draft 2019-09 schema.</returns>
    private static Predicate<JsonAny> CreateDraft201909ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is an explicit array type.</returns>
    private static Predicate<JsonAny> CreateDraft201909IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents a simple type.
    /// </summary>
    /// <returns>A predicate that returns <see langword="true"/> if the schema is a simple type.</returns>
    private static Predicate<JsonAny> CreateDraft201909IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft201909.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>A function that provides the dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    private static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft201909GetBuiltInTypeNameFunction()
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
                schema.Type.AsSimpleTypes.GetString(),
                schema.Format.GetString(),
                schema.ContentEncoding.GetString(),
                schema.ContentMediaType.GetString(),
                (validateAs & ValidationSemantics.Draft201909) != 0);
        };
    }

    /// <summary>
    /// Creates the function that builds the dotnet properties for the type declaration.
    /// </summary>
    /// <returns>An action that adds the properties to the given type declaration.</returns>
    private static Action<IPropertyBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft201909FindAndBuildPropertiesAdapter()
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
                    target.AddOrReplaceProperty(new PropertyDeclaration(builder.AnyTypeDeclarationInstance, (string)requiredName, !treatRequiredAsOptional, source == target, false, null, null));
                }
            }

            if (schema.AllOf.IsNotUndefined())
            {
                foreach (TypeDeclaration allOfTypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/allOf")).OrderBy(k => k.Key).Select(k => k.Value))
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
                foreach (TypeDeclaration dependentTypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/dependentSchemas")).OrderBy(k => k.Key).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(dependentTypeDeclaration, target, typesVisited, true);
                }
            }

            if (schema.Dependencies.IsNotUndefined())
            {
                foreach (TypeDeclaration dependentTypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/dependencies")).OrderBy(k => k.Key).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(dependentTypeDeclaration, target, typesVisited, true);
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
                    string propertyName = property.Name.GetString();
                    bool isRequired = false;

                    if (schema.Required.IsNotUndefined())
                    {
                        if (schema.Required.EnumerateArray().Any(r => propertyName == Uri.UnescapeDataString((string)r)))
                        {
                            isRequired = !treatRequiredAsOptional;
                        }
                    }

                    if (source.RefResolvablePropertyDeclarations.TryGetValue(propertyRef.AppendUnencodedPropertyNameToFragment(propertyName), out TypeDeclaration? propertyTypeDeclaration))
                    {
                        target.AddOrReplaceProperty(new PropertyDeclaration(propertyTypeDeclaration, propertyName, isRequired, source == target, propertyTypeDeclaration.Schema().Default.IsNotUndefined(), propertyTypeDeclaration.Schema().Default is JsonAny def ? def.ToString() : default, FormatDocumentation(property.Value.As<Schema>())));
                    }
                }
            }
        };
    }

    private static string? FormatDocumentation(Schema schema)
    {
        StringBuilder documentation = new();
        if (schema.Title.IsNotNullOrUndefined())
        {
            documentation.AppendLine("/// <para>");
            documentation.Append("/// ");
            documentation.AppendLine(Formatting.FormatLiteralOrNull(schema.Title.GetString(), false));
            documentation.AppendLine("/// </para>");
        }

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

        if (schema.Examples.IsNotNullOrUndefined())
        {
            documentation.AppendLine("/// <para>");
            documentation.AppendLine("/// Examples:");
            foreach (JsonAny example in schema.Examples.EnumerateArray())
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

            documentation.AppendLine("/// </para>");
        }

        if (documentation.Length > 0)
        {
            return documentation.ToString();
        }

        return null;
    }
}
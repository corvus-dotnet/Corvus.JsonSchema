﻿// <copyright file="JsonSchemaHelpers.Draft4.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.Draft4;

namespace Corvus.Json.CodeGeneration.Draft4;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft Draft4 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseDraft4(this JsonSchemaTypeBuilder builder)
    {
        JsonSchemaConfiguration configuration = builder.JsonSchemaConfiguration;
        configuration.ValidatingAs = ValidationSemantics.Draft4;
        configuration.AnchorKeywords = CreateDraft4AnchorKeywords();
        configuration.IdKeyword = CreateDraft4IdKeyword();
        configuration.ItemsKeyword = CreateDraft4ItemsKeyword();
        configuration.SchemaKeyword = CreateDraft4SchemaKeyword();
        configuration.IrreducibleKeywords = CreateDraft4IrreducibleKeywords();
        configuration.GeneratorReservedWords = CreateDraft4GeneratorReservedWords();
        configuration.DefinitionKeywords = CreateDraft4DefsKeywords();
        configuration.RefKeywords = CreateDraft4RefKeywords();
        configuration.RefResolvableKeywords = CreateDraft4RefResolvableKeywords();
        configuration.ValidateSchema = CreateDraft4ValidateSchema();
        configuration.GetBuiltInTypeName = CreateDraft4GetBuiltInTypeNameFunction();
        configuration.IsExplicitArrayType = CreateDraft4IsExplicitArrayType();
        configuration.IsSimpleType = CreateDraft4IsSimpleType();
        configuration.FindAndBuildPropertiesAdapter = CreateDraft4FindAndBuildPropertiesAdapter();
        return builder;
    }

    /// <summary>
    /// Creates the draft7 items keyword.
    /// </summary>
    /// <returns>The items keyword.</returns>
    private static string CreateDraft4ItemsKeyword()
    {
        return "items";
    }

    /// <summary>
    /// Creates the list of draft7 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    private static ImmutableArray<AnchorKeyword> CreateDraft4AnchorKeywords()
    {
        return
        [
        ];
    }

    /// <summary>
    /// Gets the draft7 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    private static string CreateDraft4IdKeyword()
    {
        return "id";
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    private static string CreateDraft4SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the draft7 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    private static ImmutableHashSet<string> CreateDraft4DefsKeywords()
    {
        return
        [
            "definitions",
        ];
    }

    /// <summary>
    /// Gets a hashset of keywords that have semantic effect for type declarations.
    /// </summary>
    /// <returns>
    /// A list of the keywords that, if applied alongside a reference
    /// keyword, mean that the type cannot by reduced to the referenced type.
    /// </returns>
    private static ImmutableHashSet<string> CreateDraft4IrreducibleKeywords()
    {
        // $ref always reduces in draft7.
        return
        [
        ];
    }

    /// <summary>
    /// Gets a set of the words used in the code generator that cannot be used for type names.
    /// </summary>
    /// <returns>The immutable set of reserved words.</returns>
    private static ImmutableHashSet<string> CreateDraft4GeneratorReservedWords()
    {
        return
        [
            "Rank",
            "Dimension",
            "ValueBufferSize",
            "TryGetNumericValues",
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
            "Encoding",
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
            "EnumValues",
            "FromProperties",
            "EnumerateObject",
            "HasProperties",
            "HasProperty",
            "JsonPropertyNames",
            "TryGetProperty",
            "SetProperty",
            "RemoveProperty",
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
    private static ImmutableArray<RefResolvableKeyword> CreateDraft4RefResolvableKeywords()
    {
        return
        [
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
            new("unevaluatedProperties", RefResolvablePropertyKind.Schema),
        ];
    }

    /// <summary>
    /// Creates the draft7 reference keywords.
    /// </summary>
    /// <returns>An array of <see cref="RefKeyword"/> instances.</returns>
    private static ImmutableArray<RefKeyword> CreateDraft4RefKeywords()
    {
        return
        [
            new RefKeyword("$ref", RefKind.Ref),
        ];
    }

    /// <summary>
    /// Creates the predicate that validates a schema against draft 7 metaschema.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a valid draft 7 schema.</returns>
    private static Predicate<JsonAny> CreateDraft4ValidateSchema()
    {
        return static s => s.As<JsonSchema.Draft4.Schema>().IsValid();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is an explicit array type.</returns>
    private static Predicate<JsonAny> CreateDraft4IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.Draft4.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents a simple type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a simple type.</returns>
    private static Predicate<JsonAny> CreateDraft4IsSimpleType()
    {
        return static s => s.As<JsonSchema.Draft4.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>The dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    private static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateDraft4GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.Draft4.Schema schema = schemaAny.As<JsonSchema.Draft4.Schema>();

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
                schema.GetContentEncoding().GetString(),
                schema.GetContentMediaType().GetString(),
                (validateAs & ValidationSemantics.Draft201909) != 0);
        };
    }

    /// <summary>
    /// Creates the function that builds the dotnet properties for the type declaration.
    /// </summary>
    /// <returns>An action that adds the properties to the given type declaration.</returns>
    private static Action<IPropertyBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateDraft4FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.Draft4.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.Draft4.Schema>();

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
                        target.AddOrReplaceProperty(new PropertyDeclaration(propertyTypeDeclaration, propertyName, isRequired, source == target, propertyTypeDeclaration.Schema().Default.IsNotUndefined(), propertyTypeDeclaration.Schema().Default is JsonAny def ? def.ToString() : default, FormatDocumentation(propertyTypeDeclaration.Schema())));
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
﻿// <copyright file="JsonSchemaHelpers.OpenApi30.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json.JsonSchema.OpenApi30;

namespace Corvus.Json.CodeGeneration.OpenApi30;

/// <summary>
/// Helper methods for JSON schema type builders.
/// </summary>
public static class JsonSchemaHelpers
{
    /// <summary>
    /// Apply the draft OpenApi30 configuration to the type builder.
    /// </summary>
    /// <param name="builder">The builder to which to apply the configuration.</param>
    /// <returns>The configured type builder.</returns>
    public static JsonSchemaTypeBuilder UseOpenApi30(this JsonSchemaTypeBuilder builder)
    {
        JsonSchemaConfiguration configuration = builder.JsonSchemaConfiguration;
        configuration.ValidatingAs = ValidationSemantics.OpenApi30;
        configuration.AnchorKeywords = CreateOpenApi30AnchorKeywords();
        configuration.IdKeyword = CreateOpenApi30IdKeyword();
        configuration.ItemsKeyword = CreateOpenApi30ItemsKeyword();
        configuration.SchemaKeyword = CreateOpenApi30SchemaKeyword();
        configuration.IrreducibleKeywords = CreateOpenApi30IrreducibleKeywords();
        configuration.GeneratorReservedWords = CreateOpenApi30GeneratorReservedWords();
        configuration.DefinitionKeywords = CreateOpenApi30DefsKeywords();
        configuration.RefKeywords = CreateOpenApi30RefKeywords();
        configuration.RefResolvableKeywords = CreateOpenApi30RefResolvableKeywords();
        configuration.ProposeName = ProposeName;
        configuration.ValidateSchema = CreateOpenApi30ValidateSchema();
        configuration.GetBuiltInTypeName = CreateOpenApi30GetBuiltInTypeNameFunction();
        configuration.IsExplicitArrayType = CreateOpenApi30IsExplicitArrayType();
        configuration.IsSimpleType = CreateOpenApi30IsSimpleType();
        configuration.FindAndBuildPropertiesAdapter = CreateOpenApi30FindAndBuildPropertiesAdapter();
        return builder;
    }

    private static bool ProposeName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, [NotNullWhen(true)] out string? name)
    {
        if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            typeDeclaration.Schema().Title.IsNotUndefined() &&
            typeDeclaration.Schema().Title.TryGetString(out string? titleValueString) &&
            titleValueString.Length > 0 && titleValueString.Length < 64)
        {
            name = titleValueString;
            return true;
        }
        else if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            typeDeclaration.Schema().Description.IsNotUndefined() &&
            typeDeclaration.Schema().Description.TryGetString(out string? descriptionString) &&
            descriptionString.Length > 0 && descriptionString.Length < 64)
        {
            name = descriptionString;
            return true;
        }
        else if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            typeDeclaration.Schema().Default.IsNotUndefined() &&
            typeDeclaration.Schema().EnumerateObject().Count() == 1)
        {
            name = $"DefaultValue{(typeDeclaration.Schema().Default.ValueKind == JsonValueKind.String ? (string)typeDeclaration.Schema().Default.AsString : typeDeclaration.Schema().Default.ToString())}";
            return true;
        }
        else if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            typeDeclaration.Schema().IsObjectType() &&
            typeDeclaration.Schema().Required.IsNotUndefined() &&
            typeDeclaration.Schema().Required.GetArrayLength() < 3)
        {
            StringBuilder s = new();
            foreach (JsonString required in typeDeclaration.Schema().Required.EnumerateArray())
            {
                if (s.Length == 0)
                {
                    s.Append("Required ");
                }
                else
                {
                    s.Append(" and ");
                }

#if NET8_0_OR_GREATER
                s.Append(Formatting.ToPascalCaseWithReservedWords((string)required));
#else
                s.Append(Formatting.ToPascalCaseWithReservedWords((string)required).ToString());
#endif
            }

            name = s.ToString();
            return true;
        }
        else if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            typeDeclaration.Schema().IsObjectType() &&
            typeDeclaration.Schema().Properties.IsNotUndefined())
        {
            var enumProperties = typeDeclaration.Schema().Properties.Where(
                p => p.Value.AsSchema.Enum.IsNullOrUndefined() &&
                     p.Value.AsSchema.Enum.GetArrayLength() == 1).ToList();

            if (enumProperties.Count > 0 && enumProperties.Count < 3)
            {
                StringBuilder s = new();
                foreach (KeyValuePair<JsonPropertyName, OpenApiDocument.Schema.PropertiesEntity.AdditionalPropertiesEntity> enumProperty in enumProperties)
                {
                    if (s.Length == 0)
                    {
                        s.Append("With ");
                    }
                    else
                    {
                        s.Append(" and ");
                    }

#if NET8_0_OR_GREATER
                    s.Append(Formatting.ToPascalCaseWithReservedWords((string)enumProperty.Key));
                    s.Append(Formatting.ToPascalCaseWithReservedWords(
                        enumProperty.Value.AsSchema.Enum[0].ValueKind == JsonValueKind.String ?
                            (string)enumProperty.Value.AsSchema.Enum[0].AsString :
                            enumProperty.Value.AsSchema.Enum[0].ToString()));
#else
                    s.Append(Formatting.ToPascalCaseWithReservedWords((string)enumProperty.Key).ToString());
                    s.Append(Formatting.ToPascalCaseWithReservedWords(
                        enumProperty.Value.AsSchema.Enum[0].ValueKind == JsonValueKind.String ?
                            (string)enumProperty.Value.AsSchema.Enum[0].AsString :
                            enumProperty.Value.AsSchema.Enum[0].ToString()).ToString());
#endif
                }

                name = s.ToString();
                return true;
            }
        }

        name = null;
        return false;
    }

    /// <summary>
    /// Creates the openApi3.0 items keyword.
    /// </summary>
    /// <returns>The items keyword.</returns>
    private static string CreateOpenApi30ItemsKeyword()
    {
        return "items";
    }

    /// <summary>
    /// Creates the list of openApi3.0 anchor keywords.
    /// </summary>
    /// <returns>An array of keywords that represent anchors in draft 2020-12.</returns>
    private static ImmutableArray<AnchorKeyword> CreateOpenApi30AnchorKeywords()
    {
        return
        [
        ];
    }

    /// <summary>
    /// Gets the openApi3.0 <c>$id</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$id"</c>.</returns>
    private static string CreateOpenApi30IdKeyword()
    {
        return string.Empty;
    }

    /// <summary>
    /// Create the schema-identifying keyword.
    /// </summary>
    /// <returns>The schema keyword.</returns>
    private static string CreateOpenApi30SchemaKeyword()
    {
        return "$schema";
    }

    /// <summary>
    /// Gets the openApi3.0 <c>$defs</c> keyword.
    /// </summary>
    /// <returns>Return <c>"$defs"</c>.</returns>
    private static ImmutableHashSet<string> CreateOpenApi30DefsKeywords()
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
    private static ImmutableHashSet<string> CreateOpenApi30IrreducibleKeywords()
    {
        // $ref always reduces in openApi 3.0.
        return
        [
        ];
    }

    /// <summary>
    /// Gets a set of the words used in the code generator that cannot be used for type names.
    /// </summary>
    /// <returns>The immutable set of reserved words.</returns>
    private static ImmutableHashSet<string> CreateOpenApi30GeneratorReservedWords()
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
    /// Creates the openApi3.0 keywords that are resolvable to a schema.
    /// </summary>
    /// <returns>An array of <see cref="RefResolvableKeyword"/> instances.</returns>
    private static ImmutableArray<RefResolvableKeyword> CreateOpenApi30RefResolvableKeywords()
    {
        return
        [
            new("definitions", RefResolvablePropertyKind.MapOfSchema),
            new("items", RefResolvablePropertyKind.Schema),
            new("properties", RefResolvablePropertyKind.MapOfSchema),
            new("additionalProperties", RefResolvablePropertyKind.Schema),
            new("allOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("anyOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("oneOf", RefResolvablePropertyKind.ArrayOfSchema),
            new("not", RefResolvablePropertyKind.Schema),
            new("contentSchema", RefResolvablePropertyKind.Schema),
        ];
    }

    /// <summary>
    /// Creates the openApi3.0 reference keywords.
    /// </summary>
    /// <returns>An array of <see cref="RefKeyword"/> instances.</returns>
    private static ImmutableArray<RefKeyword> CreateOpenApi30RefKeywords()
    {
        return
        [
            new RefKeyword("$ref", RefKind.Ref),
        ];
    }

    /// <summary>
    /// Creates the predicate that validates a schema against openApi30 metaschema.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a valid openApi30 schema.</returns>
    private static Predicate<JsonAny> CreateOpenApi30ValidateSchema()
    {
        // We always claim to be valid.
        return static _ => true;
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents an explicit array type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is an explicit array type.</returns>
    private static Predicate<JsonAny> CreateOpenApi30IsExplicitArrayType()
    {
        return static s => s.As<JsonSchema.OpenApi30.OpenApiDocument.Schema>().IsExplicitArrayType();
    }

    /// <summary>
    /// Creates the predicate that determines whether this schema represents a simple type.
    /// </summary>
    /// <returns><see langword="true"/> if the schema is a simple type.</returns>
    private static Predicate<JsonAny> CreateOpenApi30IsSimpleType()
    {
        return static s => s.As<JsonSchema.OpenApi30.OpenApiDocument.Schema>().IsSimpleType();
    }

    /// <summary>
    /// Creates the function that provides the dotnet type name and namespace for a built in type.
    /// </summary>
    /// <returns>The dotnet type name and namespace for the built-in type declaration, or null if it is not a built-in type.</returns>
    private static Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> CreateOpenApi30GetBuiltInTypeNameFunction()
    {
        return static (schemaAny, validateAs) =>
        {
            JsonSchema.OpenApi30.OpenApiDocument.Schema schema = schemaAny.As<JsonSchema.OpenApi30.OpenApiDocument.Schema>();

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
            else if (schema.IsExplicitNullType())
            {
                return BuiltInTypes.GetTypeNameFor(
                    schema.Type.GetString() ?? "null",
                    schema.Format.GetString(),
                    schema.GetContentEncoding().GetString(),
                    schema.GetContentMediaType().GetString(),
                    (validateAs & ValidationSemantics.Draft201909) != 0);
            }

            return BuiltInTypes.GetTypeNameFor(
                schema.Type.GetString(),
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
    private static Action<IPropertyBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> CreateOpenApi30FindAndBuildPropertiesAdapter()
    {
        return static (builder, source, target, typesVisited, treatRequiredAsOptional) =>
        {
            if (typesVisited.Contains(source))
            {
                return;
            }

            typesVisited.Add(source);

            JsonSchema.OpenApi30.OpenApiDocument.Schema schema = source.LocatedSchema.Schema.As<JsonSchema.OpenApi30.OpenApiDocument.Schema>();

            // First we add the 'required' properties as JsonAny; they will be overridden if we have explicit implementations
            // elsewhere
            if (schema.Required.IsNotUndefined())
            {
                foreach (JsonString requiredName in schema.Required.EnumerateArray())
                {
                    target.AddOrReplaceProperty(new PropertyDeclaration(builder.AnyTypeDeclarationInstance, (string)requiredName, !treatRequiredAsOptional, source == target, false, null, null, false));
                }
            }

            if (schema.AllOf.IsNotUndefined())
            {
                foreach (TypeDeclaration allOfTypeDeclaration in source.RefResolvablePropertyDeclarations.Where(k => k.Key.StartsWith("#/allOf")).OrderBy(k => k.Key).Select(k => k.Value))
                {
                    builder.FindAndBuildProperties(allOfTypeDeclaration, target, typesVisited, treatRequiredAsOptional);
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
                        target.AddOrReplaceProperty(new PropertyDeclaration(propertyTypeDeclaration, propertyName, isRequired, source == target, propertyTypeDeclaration.Schema().Default.IsNotUndefined(), propertyTypeDeclaration.Schema().Default is JsonAny def ? def.ToString() : default, FormatDocumentation(propertyTypeDeclaration.Schema()), false));
                    }
                }
            }
        };
    }

    private static string? FormatDocumentation(OpenApiDocument.Schema schema)
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

        if (documentation.Length > 0)
        {
            return documentation.ToString();
        }

        return null;

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
}
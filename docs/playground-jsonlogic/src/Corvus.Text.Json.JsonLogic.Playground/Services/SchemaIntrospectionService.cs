using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.JsonLogic.Playground.Models;

namespace Corvus.Text.Json.JsonLogic.Playground.Services;

/// <summary>
/// Parses a JSON Schema and extracts a hierarchical property tree
/// for use in Blockly dropdown population.
/// </summary>
public static class SchemaIntrospectionService
{
    private const string SchemaBaseUri = "schema://jsonlogic-playground/";

    /// <summary>
    /// Parse a JSON Schema string and extract its property tree.
    /// </summary>
    /// <returns>A <see cref="SchemaNode"/> tree, or null if the schema is invalid.</returns>
    public static async Task<SchemaNode?> ExtractSchemaTreeAsync(
        string schemaJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return null;
        }

        System.Text.Json.JsonDocument schemaDoc;
        try
        {
            schemaDoc = System.Text.Json.JsonDocument.Parse(schemaJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        try
        {
            using PrepopulatedDocumentResolver documentResolver = new();
            documentResolver.AddMetaschema();

            string uri = SchemaBaseUri + "schema.json";
            documentResolver.AddDocument(uri, schemaDoc);

            // Also register by $id if present
            if (schemaDoc.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement) &&
                idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                idElement.GetString() is string id)
            {
                documentResolver.AddDocument(id, schemaDoc);
            }

            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            vocabularyRegistry.RegisterVocabularies(
                Corvus.Json.CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

            IVocabulary defaultVocabulary =
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
            JsonReference reference = new(uri);

            TypeDeclaration rootType = await typeBuilder
                .AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseAsRoot: false, cancellationToken: cancellationToken);

            HashSet<TypeDeclaration> visited = [];
            return BuildSchemaNode(rootType, visited);
        }
        catch
        {
            return null;
        }
    }

    private static SchemaNode BuildSchemaNode(TypeDeclaration typeDecl, HashSet<TypeDeclaration> visited)
    {
        TypeDeclaration reduced = typeDecl.ReducedTypeDeclaration().ReducedType;
        List<string> types = GetTypeStrings(reduced);

        Dictionary<string, SchemaPropertyNode>? properties = null;
        SchemaNode? items = null;
        bool additionalProperties = true;

        // Only recurse if we haven't visited this type (circular $ref protection)
        if (visited.Add(reduced))
        {
            try
            {
                properties = CollectProperties(reduced, visited);
                items = CollectArrayItems(reduced, visited);
                additionalProperties = InferAdditionalProperties(reduced);

                // For anyOf/oneOf: merge properties from object-typed alternatives
                MergeCompositionProperties(reduced, reduced.AnyOfCompositionTypes(), properties, visited);
                MergeCompositionProperties(reduced, reduced.OneOfCompositionTypes(), properties, visited);
            }
            finally
            {
                visited.Remove(reduced);
            }
        }

        return new SchemaNode
        {
            Types = types,
            Properties = properties is { Count: > 0 } ? properties : null,
            Items = items,
            AdditionalProperties = additionalProperties,
        };
    }

    private static Dictionary<string, SchemaPropertyNode>? CollectProperties(
        TypeDeclaration reduced,
        HashSet<TypeDeclaration> visited)
    {
        if (!reduced.HasPropertyDeclarations)
        {
            return null;
        }

        Dictionary<string, SchemaPropertyNode> properties = [];

        foreach (PropertyDeclaration prop in reduced.PropertyDeclarations)
        {
            TypeDeclaration propReduced = prop.ReducedPropertyType.ReducedTypeDeclaration().ReducedType;
            List<string> propTypes = GetTypeStrings(propReduced);
            bool isRequired = prop.RequiredOrOptional is RequiredOrOptional.Required
                or RequiredOrOptional.ComposedRequired;

            Dictionary<string, SchemaPropertyNode>? nested = null;
            SchemaNode? propItems = null;
            bool propAdditional = true;

            if (visited.Add(propReduced))
            {
                try
                {
                    nested = CollectProperties(propReduced, visited);
                    propItems = CollectArrayItems(propReduced, visited);
                    propAdditional = InferAdditionalProperties(propReduced);
                    MergeCompositionProperties(propReduced, propReduced.AnyOfCompositionTypes(), nested, visited);
                    MergeCompositionProperties(propReduced, propReduced.OneOfCompositionTypes(), nested, visited);
                }
                finally
                {
                    visited.Remove(propReduced);
                }
            }

            properties[prop.JsonPropertyName] = new SchemaPropertyNode
            {
                Types = propTypes,
                Required = isRequired,
                Properties = nested is { Count: > 0 } ? nested : null,
                Items = propItems,
                AdditionalProperties = propAdditional,
            };
        }

        return properties;
    }

    private static SchemaNode? CollectArrayItems(TypeDeclaration reduced, HashSet<TypeDeclaration> visited)
    {
        if (reduced.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItems)
        {
            TypeDeclaration itemReduced = arrayItems.ReducedType.ReducedTypeDeclaration().ReducedType;
            return BuildSchemaNode(itemReduced, visited);
        }

        return null;
    }

    private static void MergeCompositionProperties<TKeyword>(
        TypeDeclaration typeDecl,
        IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes,
        Dictionary<string, SchemaPropertyNode>? targetProperties,
        HashSet<TypeDeclaration> visited)
    {
        if (compositionTypes is null || targetProperties is null)
        {
            return;
        }

        foreach (var kvp in compositionTypes)
        {
            foreach (TypeDeclaration variant in kvp.Value)
            {
                TypeDeclaration variantReduced = variant.ReducedTypeDeclaration().ReducedType;
                CoreTypes variantTypes = variantReduced.ImpliedCoreTypesOrAny();

                if (!variantTypes.HasFlag(CoreTypes.Object))
                {
                    continue;
                }

                if (!variantReduced.HasPropertyDeclarations)
                {
                    continue;
                }

                foreach (PropertyDeclaration prop in variantReduced.PropertyDeclarations)
                {
                    // Don't overwrite existing properties — first definition wins
                    if (targetProperties.ContainsKey(prop.JsonPropertyName))
                    {
                        continue;
                    }

                    TypeDeclaration propReduced = prop.ReducedPropertyType.ReducedTypeDeclaration().ReducedType;
                    List<string> propTypes = GetTypeStrings(propReduced);
                    bool isRequired = prop.RequiredOrOptional is RequiredOrOptional.Required
                        or RequiredOrOptional.ComposedRequired;

                    Dictionary<string, SchemaPropertyNode>? nested = null;
                    SchemaNode? propItems = null;
                    bool propAdditional = true;

                    if (visited.Add(propReduced))
                    {
                        try
                        {
                            nested = CollectProperties(propReduced, visited);
                            propItems = CollectArrayItems(propReduced, visited);
                            propAdditional = InferAdditionalProperties(propReduced);
                        }
                        finally
                        {
                            visited.Remove(propReduced);
                        }
                    }

                    targetProperties[prop.JsonPropertyName] = new SchemaPropertyNode
                    {
                        Types = propTypes,
                        Required = isRequired,
                        Properties = nested is { Count: > 0 } ? nested : null,
                        Items = propItems,
                        AdditionalProperties = propAdditional,
                    };
                }
            }
        }
    }

    private static bool InferAdditionalProperties(TypeDeclaration reduced)
    {
        // If the schema has no object type, additional properties is irrelevant
        CoreTypes coreTypes = reduced.ImpliedCoreTypesOrAny();
        if (!coreTypes.HasFlag(CoreTypes.Object))
        {
            return false;
        }

        // Default to true — most schemas allow additional properties
        // The TypeDeclaration doesn't directly expose additionalProperties=false,
        // but if there are no property declarations and the type is object, it's open
        return true;
    }

    private static List<string> GetTypeStrings(TypeDeclaration reduced)
    {
        CoreTypes coreTypes = reduced.ImpliedCoreTypesOrAny();
        List<string> types = [];

        if (coreTypes.HasFlag(CoreTypes.Object))
        {
            types.Add("object");
        }

        if (coreTypes.HasFlag(CoreTypes.Array))
        {
            types.Add("array");
        }

        if (coreTypes.HasFlag(CoreTypes.String))
        {
            types.Add("string");
        }

        if (coreTypes.HasFlag(CoreTypes.Integer))
        {
            types.Add("integer");
        }
        else if (coreTypes.HasFlag(CoreTypes.Number))
        {
            types.Add("number");
        }

        if (coreTypes.HasFlag(CoreTypes.Boolean))
        {
            types.Add("boolean");
        }

        if (coreTypes.HasFlag(CoreTypes.Null))
        {
            types.Add("null");
        }

        if (types.Count == 0)
        {
            types.Add("any");
        }

        return types;
    }
}

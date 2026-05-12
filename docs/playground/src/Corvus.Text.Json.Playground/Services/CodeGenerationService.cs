using System.Collections.Immutable;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.Playground.Models;
using PropertyDeclarationExtensions = Corvus.Text.Json.CodeGeneration.PropertyDeclarationExtensions;
using SysJsonElement = System.Text.Json.JsonElement;
using SysJsonValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Wraps the V5 code generation pipeline for use in a WASM context.
/// This is the "QuickStart" wrapper that V5 doesn't provide out of the box.
/// </summary>
public class CodeGenerationService
{
    private const string SchemaBaseUri = "schema://playground/";

    // Match V5 SourceGenerator defaults
    private static readonly ImmutableArray<string> DefaultDisabledNamingHeuristics =
        ["DocumentationNameHeuristic"];

    /// <summary>
    /// Generate C# types from multiple JSON Schema files.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        IReadOnlyList<SchemaFile> schemaFiles,
        string rootNamespace = "Playground",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a prepopulated document resolver (no file I/O, WASM-safe)
            using PrepopulatedDocumentResolver documentResolver = new();
            documentResolver.AddMetaschema();

            // Parse and register all schema documents (mimics V5 SourceGenerator pattern)
            var parsedSchemas = new List<(SchemaFile File, string Uri)>();
            var schemaErrors = new List<SchemaError>();

            foreach (SchemaFile schemaFile in schemaFiles)
            {
                System.Text.Json.JsonDocument schemaDoc;
                try
                {
                    schemaDoc = System.Text.Json.JsonDocument.Parse(schemaFile.Content);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    schemaErrors.Add(new SchemaError(
                        schemaFile.Name,
                        ex.Message,
                        ex.LineNumber,
                        ex.BytePositionInLine));
                    continue;
                }

                // Registration 1: By playground URI derived from filename
                string uri = SchemaBaseUri + schemaFile.Name;
                documentResolver.AddDocument(uri, schemaDoc);

                // Registration 2: By $id if present (critical for $ref resolution)
                if (schemaDoc.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement) &&
                    idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                    idElement.GetString() is string id)
                {
                    documentResolver.AddDocument(id, schemaDoc);
                }

                parsedSchemas.Add((schemaFile, uri));
            }

            if (schemaErrors.Count > 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"JSON parse errors in {schemaErrors.Count} schema file(s)",
                    SchemaErrors = schemaErrors,
                };
            }

            // Register vocabulary analyzers (same as V5 SourceGenerator)
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

            // Build type declarations — one AddTypeDeclarations call per root type
            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
            var rootTypes = new List<TypeDeclaration>();

            foreach (var (file, uri) in parsedSchemas)
            {
                if (!file.IsRootType)
                {
                    continue;
                }

                JsonReference reference = new(uri);
                TypeDeclaration rootType = await typeBuilder
                    .AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseAsRoot: false);
                rootTypes.Add(rootType);
            }

            if (rootTypes.Count == 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = "No root types selected for generation. Mark at least one schema as a root type.",
                };
            }

            // Build explicit type name overrides from schemas that specify a TypeName
            var namedTypes = parsedSchemas
                .Where(s => s.File.IsRootType && !string.IsNullOrEmpty(s.File.TypeName))
                .Select(s => new CodeGeneration.CSharpLanguageProvider.NamedType(
                    new JsonReference(s.Uri),
                    s.File.TypeName!))
                .ToArray();

            // Configure the C# language provider to match V5 SourceGenerator defaults
            var options = new CodeGeneration.CSharpLanguageProvider.Options(
                rootNamespace,
                namedTypes: namedTypes.Length > 0 ? namedTypes : null,
                addExplicitUsings: true,
                useImplicitOperatorString: true,
                useOptionalNameHeuristics: true,
                alwaysAssertFormat: true,
                disabledNamingHeuristics: [.. DefaultDisabledNamingHeuristics]);

            var languageProvider = CodeGeneration.CSharpLanguageProvider.DefaultWithOptions(options);

            // Single GenerateCodeUsing call with all root types
            IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                typeBuilder.GenerateCodeUsing(
                    languageProvider,
                    rootTypes,
                    cancellationToken);

            // Build the type map from all root TypeDeclarations
            var typeMap = BuildTypeMap(rootTypes, generatedCode);

            return new GenerationResult
            {
                Success = true,
                GeneratedFiles = generatedCode,
                TypeMap = typeMap,
            };
        }
        catch (Exception ex)
        {
            // Try to associate the error with root schema files
            var rootSchemaNames = schemaFiles
                .Where(f => f.IsRootType)
                .Select(f => f.Name)
                .ToList();

            var errors = rootSchemaNames.Count > 0
                ? rootSchemaNames.Select(n => new SchemaError(n, ex.Message, null, null)).ToList()
                : [new SchemaError(schemaFiles[0].Name, ex.Message, null, null)];

            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Code generation failed: {ex.Message}",
                SchemaErrors = errors,
            };
        }
    }

    /// <summary>
    /// Single-schema convenience overload (backwards compatible).
    /// </summary>
    public Task<GenerationResult> GenerateAsync(
        string schemaJson,
        string rootNamespace = "Playground",
        CancellationToken cancellationToken = default)
    {
        var schemaFile = new SchemaFile
        {
            Name = "user-schema.json",
            Content = schemaJson,
            IsRootType = true,
        };

        return this.GenerateAsync([schemaFile], rootNamespace, cancellationToken);
    }

    private static List<TypeMapEntry> BuildTypeMap(
        IReadOnlyList<TypeDeclaration> rootTypes,
        IReadOnlyCollection<GeneratedCodeFile> generatedFiles)
    {
        var entries = new List<TypeMapEntry>();
        var visited = new HashSet<TypeDeclaration>();

        // Build a set of all TypeDeclarations that were actually generated
        var generatedTypes = new HashSet<TypeDeclaration>(
            generatedFiles
                .Where(f => f.TypeDeclaration is not null)
                .Select(f => f.TypeDeclaration!));

        foreach (TypeDeclaration rootType in rootTypes)
        {
            CollectTypeMapEntries(rootType, generatedTypes, entries, visited);
        }

        return entries;
    }

    private static void CollectTypeMapEntries(
        TypeDeclaration typeDecl,
        HashSet<TypeDeclaration> generatedTypes,
        List<TypeMapEntry> entries,
        HashSet<TypeDeclaration> visited)
    {
        TypeDeclaration reduced = typeDecl.ReducedTypeDeclaration().ReducedType;
        if (!visited.Add(reduced))
        {
            return;
        }

        // Only include types that were actually generated (have their own file or are in the global file)
        if (!generatedTypes.Contains(reduced))
        {
            return;
        }

        // Get the fully qualified .NET type name directly from the TypeDeclaration metadata
        string fullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(reduced);
        string shortName = fullName.Contains('.')
            ? fullName[(fullName.LastIndexOf('.') + 1)..]
            : fullName;

        string kind = InferKind(reduced);
        string? pointer = GetSchemaPointer(reduced);
        string? sourceName = GetSourceSchemaName(reduced);

        var properties = new List<TypeMapProperty>();
        foreach (PropertyDeclaration prop in reduced.PropertyDeclarations)
        {
            TypeDeclaration propReduced = prop.ReducedPropertyType.ReducedTypeDeclaration().ReducedType;

            // Get the property's .NET type name directly from metadata
            string propFullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(propReduced);
            string propTypeName = propFullName.Contains('.')
                ? propFullName[(propFullName.LastIndexOf('.') + 1)..]
                : propFullName;

            // Get the .NET property accessor name
            string dotnetPropName = PropertyDeclarationExtensions.DotnetPropertyName(prop);

            bool isComposed = prop.LocalOrComposed == LocalOrComposed.Composed;

            properties.Add(new TypeMapProperty(
                prop.JsonPropertyName,
                dotnetPropName,
                propTypeName,
                propFullName,
                GetSchemaPointer(prop.ReducedPropertyType),
                GetSourceSchemaName(prop.ReducedPropertyType),
                prop.RequiredOrOptional == RequiredOrOptional.Required ||
                    prop.RequiredOrOptional == RequiredOrOptional.ComposedRequired,
                isComposed));
        }

        // Collect composition groups (allOf, anyOf, oneOf)
        var compositionGroups = new List<TypeMapCompositionGroup>();
        CollectCompositionGroup(reduced, "allOf", reduced.AllOfCompositionTypes(), compositionGroups);
        CollectCompositionGroup(reduced, "anyOf", reduced.AnyOfCompositionTypes(), compositionGroups);
        CollectCompositionGroup(reduced, "oneOf", reduced.OneOfCompositionTypes(), compositionGroups);

        // Array item type
        TypeMapArrayItemType? arrayItemType = null;
        if (reduced.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsForEntry)
        {
            TypeDeclaration itemReduced = arrayItemsForEntry.ReducedType.ReducedTypeDeclaration().ReducedType;
            string itemFullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(itemReduced);
            string itemShortName = itemFullName.Contains('.')
                ? itemFullName[(itemFullName.LastIndexOf('.') + 1)..]
                : itemFullName;
            arrayItemType = new TypeMapArrayItemType(
                itemShortName,
                itemFullName,
                GetSchemaPointer(itemReduced),
                GetSourceSchemaName(itemReduced));
        }

        // Tuple item types
        List<TypeMapTupleItem>? tupleItems = null;
        if (reduced.TupleType() is TupleTypeDeclaration tupleTypeForEntry)
        {
            tupleItems = [];
            for (int i = 0; i < tupleTypeForEntry.ItemsTypes.Length; i++)
            {
                TypeDeclaration itemReduced = tupleTypeForEntry.ItemsTypes[i].ReducedType.ReducedTypeDeclaration().ReducedType;
                string itemFullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(itemReduced);
                string itemShortName = itemFullName.Contains('.')
                    ? itemFullName[(itemFullName.LastIndexOf('.') + 1)..]
                    : itemFullName;
                tupleItems.Add(new TypeMapTupleItem(
                    i + 1, itemShortName, itemFullName,
                    GetSchemaPointer(itemReduced),
                    GetSourceSchemaName(itemReduced)));
            }
        }

        // Prefix items for non-pure-tuple arrays (has prefixItems but also allows additional items)
        List<TypeMapTupleItem>? prefixItems = null;
        if (tupleItems is null)
        {
            TupleTypeDeclaration? prefixTupleType = reduced.ExplicitTupleType() ?? reduced.ImplicitTupleType();
            if (prefixTupleType is not null)
            {
                prefixItems = [];
                for (int i = 0; i < prefixTupleType.ItemsTypes.Length; i++)
                {
                    TypeDeclaration itemReduced = prefixTupleType.ItemsTypes[i].ReducedType.ReducedTypeDeclaration().ReducedType;
                    string itemFullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(itemReduced);
                    string itemShortName = itemFullName.Contains('.')
                        ? itemFullName[(itemFullName.LastIndexOf('.') + 1)..]
                        : itemFullName;
                    prefixItems.Add(new TypeMapTupleItem(
                        i + 1, itemShortName, itemFullName,
                        GetSchemaPointer(itemReduced),
                        GetSourceSchemaName(itemReduced)));
                }
            }
        }

        // Enum values (from the "enum" keyword)
        List<string>? enumValues = null;
        var anyOfConstants = reduced.AnyOfConstantValues();
        if (anyOfConstants is not null)
        {
            enumValues = [];
            foreach (var kvp in anyOfConstants)
            {
                foreach (SysJsonElement el in kvp.Value)
                {
                    enumValues.Add(FormatJsonElement(el));
                }
            }
        }

        // Const value (from the "const" keyword)
        string? constValue = null;
        SysJsonElement explicitConst = reduced.ExplicitSingleConstantValue();
        if (explicitConst.ValueKind != SysJsonValueKind.Undefined)
        {
            constValue = FormatJsonElement(explicitConst);
        }

        entries.Add(new TypeMapEntry(
            shortName, fullName, kind, pointer, sourceName, properties,
            compositionGroups, arrayItemType, tupleItems, prefixItems,
            enumValues, constValue));

        // Recurse into property types
        foreach (PropertyDeclaration prop in reduced.PropertyDeclarations)
        {
            CollectTypeMapEntries(prop.ReducedPropertyType, generatedTypes, entries, visited);
        }

        // Recurse into array item types
        if (reduced.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItems)
        {
            CollectTypeMapEntries(arrayItems.ReducedType, generatedTypes, entries, visited);
        }

        // Recurse into tuple item types
        if (reduced.TupleType() is TupleTypeDeclaration tupleType)
        {
            foreach (ReducedTypeDeclaration tupleItem in tupleType.ItemsTypes)
            {
                CollectTypeMapEntries(tupleItem.ReducedType, generatedTypes, entries, visited);
            }
        }
        else
        {
            // Recurse into prefix item types for non-pure tuples
            TupleTypeDeclaration? prefixTupleForRecurse = reduced.ExplicitTupleType() ?? reduced.ImplicitTupleType();
            if (prefixTupleForRecurse is not null)
            {
                foreach (ReducedTypeDeclaration prefixItem in prefixTupleForRecurse.ItemsTypes)
                {
                    CollectTypeMapEntries(prefixItem.ReducedType, generatedTypes, entries, visited);
                }
            }
        }

        // Recurse into composition member types (oneOf, anyOf, allOf)
        RecurseCompositionTypes(reduced.AllOfCompositionTypes(), generatedTypes, entries, visited);
        RecurseCompositionTypes(reduced.AnyOfCompositionTypes(), generatedTypes, entries, visited);
        RecurseCompositionTypes(reduced.OneOfCompositionTypes(), generatedTypes, entries, visited);
    }

    private static void CollectCompositionGroup<TKeyword>(
        TypeDeclaration typeDecl,
        string keyword,
        IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes,
        List<TypeMapCompositionGroup> groups)
    {
        if (compositionTypes is null)
        {
            return;
        }

        var members = new List<TypeMapCompositionMember>();
        foreach (var kvp in compositionTypes)
        {
            foreach (TypeDeclaration composedType in kvp.Value)
            {
                TypeDeclaration composedReduced = composedType.ReducedTypeDeclaration().ReducedType;
                string composedFullName = CodeGeneration.CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(composedReduced);
                string composedShortName = composedFullName.Contains('.')
                    ? composedFullName[(composedFullName.LastIndexOf('.') + 1)..]
                    : composedFullName;

                // Check if this composition member has a const value
                string? memberConstValue = null;
                SysJsonElement memberConst = composedReduced.ExplicitSingleConstantValue();
                if (memberConst.ValueKind != SysJsonValueKind.Undefined)
                {
                    memberConstValue = FormatJsonElement(memberConst);
                }

                members.Add(new TypeMapCompositionMember(
                    composedShortName, composedFullName,
                    GetSchemaPointer(composedReduced),
                    GetSourceSchemaName(composedReduced),
                    memberConstValue));
            }
        }

        if (members.Count > 0)
        {
            groups.Add(new TypeMapCompositionGroup(keyword, members));
        }
    }

    private static void RecurseCompositionTypes<TKeyword>(
        IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes,
        HashSet<TypeDeclaration> generatedTypes,
        List<TypeMapEntry> entries,
        HashSet<TypeDeclaration> visited)
    {
        if (compositionTypes is null)
        {
            return;
        }

        foreach (var kvp in compositionTypes)
        {
            foreach (TypeDeclaration composedType in kvp.Value)
            {
                CollectTypeMapEntries(composedType, generatedTypes, entries, visited);
            }
        }
    }

    private static string FormatJsonElement(SysJsonElement element)
    {
        return element.ValueKind switch
        {
            SysJsonValueKind.String => $"\"{element.GetString()}\"",
            SysJsonValueKind.Number => element.GetRawText(),
            SysJsonValueKind.True => "true",
            SysJsonValueKind.False => "false",
            SysJsonValueKind.Null => "null",
            _ => element.GetRawText(),
        };
    }

    private static string? GetSchemaPointer(TypeDeclaration typeDecl)
    {
        string location = typeDecl.LocatedSchema.Location.ToString();

        // Extract the fragment (JSON pointer) from the full URI
        // e.g. "schema://playground/person.json#/properties/address" → "#/properties/address"
        int hashIndex = location.IndexOf('#');
        if (hashIndex >= 0)
        {
            return location[hashIndex..];
        }

        // Root schema has no fragment — use "#"
        if (location.StartsWith(SchemaBaseUri, StringComparison.Ordinal))
        {
            return "#";
        }

        return location;
    }

    private static string? GetSourceSchemaName(TypeDeclaration typeDecl)
    {
        string location = typeDecl.LocatedSchema.Location.ToString();

        // Extract filename from "schema://playground/person.json" or
        // "schema://playground/person.json#/properties/address"
        if (location.StartsWith(SchemaBaseUri, StringComparison.Ordinal))
        {
            string rest = location[SchemaBaseUri.Length..];
            int hashIndex = rest.IndexOf('#');
            return hashIndex >= 0 ? rest[..hashIndex] : rest;
        }

        return null;
    }

    private static string InferKind(TypeDeclaration typeDecl)
    {
        // Check for const value first (most specific)
        SysJsonElement explicitConst = typeDecl.ExplicitSingleConstantValue();
        if (explicitConst.ValueKind != SysJsonValueKind.Undefined)
        {
            return "const";
        }

        // Check for enum values
        var anyOfConstants = typeDecl.AnyOfConstantValues();
        if (anyOfConstants is not null && anyOfConstants.Count > 0)
        {
            return "enum";
        }

        CoreTypes coreTypes = typeDecl.ImpliedCoreTypes();

        if (coreTypes.HasFlag(CoreTypes.Object))
        {
            return "object";
        }

        if (coreTypes.HasFlag(CoreTypes.Array))
        {
            if (typeDecl.TupleType() is not null)
            {
                return "tuple";
            }

            int rank = typeDecl.ArrayRank() ?? 1;
            bool isNumeric = typeDecl.IsNumericArray();

            if (rank > 1)
            {
                return isNumeric ? "numeric tensor" : "tensor";
            }

            return isNumeric ? "numeric array" : "array";
        }

        if (coreTypes.HasFlag(CoreTypes.String))
        {
            return "string";
        }

        if (coreTypes.HasFlag(CoreTypes.Integer))
        {
            return "integer";
        }

        if (coreTypes.HasFlag(CoreTypes.Number))
        {
            return "number";
        }

        if (coreTypes.HasFlag(CoreTypes.Boolean))
        {
            return "boolean";
        }

        if (coreTypes.HasFlag(CoreTypes.Null))
        {
            return "null";
        }

        return "unknown";
    }
}

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Corvus.Text.Json.TypeScript.Playground.Models;
using SysJsonElement = System.Text.Json.JsonElement;
using SysJsonValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.TypeScript.Playground.Services;

/// <summary>
/// Wraps the TypeScript code-generation engine (<see cref="TypeScriptLanguageProvider"/>) for use in a WASM
/// context: parse + register schemas, register the dialect vocabularies, build the type declarations, and emit
/// the TypeScript module (generated.ts + the shared corvus-runtime.ts), with a stable evaluateRoot entry point.
/// </summary>
public class CodeGenerationService
{
    private const string SchemaBaseUri = "schema://playground/";

    /// <summary>
    /// Generate a TypeScript module from one or more JSON Schema files.
    /// </summary>
    /// <param name="schemaFiles">The schema files (at least one marked as a root type).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generation result (generated TypeScript + runtime, or errors).</returns>
    public async Task<GenerationResult> GenerateAsync(
        IReadOnlyList<SchemaFile> schemaFiles,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Prepopulated resolver (no file I/O, WASM-safe).
            using PrepopulatedDocumentResolver documentResolver = new();
            documentResolver.AddMetaschema();

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
                    schemaErrors.Add(new SchemaError(schemaFile.Name, ex.Message, ex.LineNumber, ex.BytePositionInLine));
                    continue;
                }

                string uri = SchemaBaseUri + schemaFile.Name;
                documentResolver.AddDocument(uri, schemaDoc);

                // Also register by $id (critical for cross-file $ref resolution).
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

            // Register the supported dialect vocabularies (same set as the CLI / the C# playground).
            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            vocabularyRegistry.RegisterVocabularies(
                Corvus.Json.CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

            IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
            var rootTypes = new List<TypeDeclaration>();

            foreach ((SchemaFile file, string uri) in parsedSchemas)
            {
                if (!file.IsRootType)
                {
                    continue;
                }

                JsonReference reference = new(uri);
                TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseAsRoot: false);
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

            TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.DefaultWithOptions(
                new TypeScriptLanguageProvider.Options(AlwaysAssertFormat: true));

            IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                typeBuilder.GenerateCodeUsing(provider, rootTypes, cancellationToken);

            // The generated types/validators module, plus the appended evaluateRoot entry point (the driver's
            // second step); the runtime module is emitted alongside it.
            string generatedTs = generatedCode.FirstOrDefault(f => f.FileName == "generated.ts")?.FileContent ?? string.Empty;
            generatedTs += provider.RootEvaluatorExport(rootTypes[0]);
            string runtimeTs = generatedCode.FirstOrDefault(f => f.FileName == "corvus-runtime.ts")?.FileContent ?? string.Empty;

            return new GenerationResult
            {
                Success = true,
                GeneratedFiles = generatedCode,
                GeneratedTypeScript = generatedTs,
                RuntimeTypeScript = runtimeTs,
                TypeMap = BuildTypeMap(rootTypes),
            };
        }
        catch (Exception ex)
        {
            var rootSchemaNames = schemaFiles.Where(f => f.IsRootType).Select(f => f.Name).ToList();
            var errors = rootSchemaNames.Count > 0
                ? rootSchemaNames.Select(n => new SchemaError(n, ex.Message, null, null)).ToList()
                : [new SchemaError(schemaFiles.Count > 0 ? schemaFiles[0].Name : "schema.json", ex.Message, null, null)];

            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Code generation failed: {ex.Message}",
                SchemaErrors = errors,
            };
        }
    }

    /// <summary>
    /// Single-schema convenience overload.
    /// </summary>
    /// <param name="schemaJson">The JSON Schema source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generation result.</returns>
    public Task<GenerationResult> GenerateAsync(string schemaJson, CancellationToken cancellationToken = default)
        => this.GenerateAsync(
            [new SchemaFile { Name = "user-schema.json", Content = schemaJson, IsRootType = true }],
            cancellationToken);

    // The metadata key under which TypeScriptLanguageProvider stores each type's assigned TS name
    // (mirrors its private TsFinalKey). A type with an assigned name got an emitted surface (interface/type).
    private const string TsFinalNameKey = "Ts_FinalName";

    private static List<TypeMapEntry> BuildTypeMap(IReadOnlyList<TypeDeclaration> rootTypes)
    {
        var entries = new List<TypeMapEntry>();
        var visited = new HashSet<TypeDeclaration>();
        foreach (TypeDeclaration rootType in rootTypes)
        {
            CollectTypeMapEntries(rootType, entries, visited);
        }

        return entries;
    }

    private static void CollectTypeMapEntries(TypeDeclaration typeDecl, List<TypeMapEntry> entries, HashSet<TypeDeclaration> visited)
    {
        TypeDeclaration reduced = typeDecl.ReducedTypeDeclaration().ReducedType;
        if (!visited.Add(reduced))
        {
            return;
        }

        // Only named, emitted types get a type-map row; bare primitives appear inline as property types.
        if (TryGetTsName(reduced) is not string name)
        {
            RecurseChildren(reduced, entries, visited);
            return;
        }

        var properties = new List<TypeMapProperty>();
        foreach (PropertyDeclaration prop in reduced.PropertyDeclarations)
        {
            TypeDeclaration propReduced = prop.ReducedPropertyType.ReducedTypeDeclaration().ReducedType;
            string propTypeName = TsDisplayName(propReduced);
            bool isRequired = prop.RequiredOrOptional is RequiredOrOptional.Required or RequiredOrOptional.ComposedRequired;
            bool isComposed = prop.LocalOrComposed == LocalOrComposed.Composed;
            properties.Add(new TypeMapProperty(
                prop.JsonPropertyName,
                prop.JsonPropertyName,
                propTypeName,
                propTypeName,
                GetSchemaPointer(prop.ReducedPropertyType),
                GetSourceSchemaName(prop.ReducedPropertyType),
                isRequired,
                isComposed));
        }

        var compositionGroups = new List<TypeMapCompositionGroup>();
        CollectCompositionGroup("allOf", reduced.AllOfCompositionTypes(), compositionGroups);
        CollectCompositionGroup("anyOf", reduced.AnyOfCompositionTypes(), compositionGroups);
        CollectCompositionGroup("oneOf", reduced.OneOfCompositionTypes(), compositionGroups);

        TypeMapArrayItemType? arrayItemType = null;
        if (reduced.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItems)
        {
            TypeDeclaration itemReduced = arrayItems.ReducedType.ReducedTypeDeclaration().ReducedType;
            string itemName = TsDisplayName(itemReduced);
            arrayItemType = new TypeMapArrayItemType(itemName, itemName, GetSchemaPointer(itemReduced), GetSourceSchemaName(itemReduced));
        }

        List<string>? enumValues = null;
        if (reduced.AnyOfConstantValues() is { } anyOfConstants)
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

        string? constValue = null;
        SysJsonElement explicitConst = reduced.ExplicitSingleConstantValue();
        if (explicitConst.ValueKind != SysJsonValueKind.Undefined)
        {
            constValue = FormatJsonElement(explicitConst);
        }

        entries.Add(new TypeMapEntry(
            name, name, InferKind(reduced), GetSchemaPointer(reduced), GetSourceSchemaName(reduced),
            properties, compositionGroups, arrayItemType, null, null, enumValues, constValue));

        RecurseChildren(reduced, entries, visited);
    }

    private static void RecurseChildren(TypeDeclaration reduced, List<TypeMapEntry> entries, HashSet<TypeDeclaration> visited)
    {
        foreach (PropertyDeclaration prop in reduced.PropertyDeclarations)
        {
            CollectTypeMapEntries(prop.ReducedPropertyType, entries, visited);
        }

        if (reduced.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItems)
        {
            CollectTypeMapEntries(arrayItems.ReducedType, entries, visited);
        }

        RecurseCompositionTypes(reduced.AllOfCompositionTypes(), entries, visited);
        RecurseCompositionTypes(reduced.AnyOfCompositionTypes(), entries, visited);
        RecurseCompositionTypes(reduced.OneOfCompositionTypes(), entries, visited);
    }

    private static void CollectCompositionGroup<TKeyword>(
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
                string composedName = TsDisplayName(composedReduced);
                string? memberConst = null;
                SysJsonElement c = composedReduced.ExplicitSingleConstantValue();
                if (c.ValueKind != SysJsonValueKind.Undefined)
                {
                    memberConst = FormatJsonElement(c);
                }

                members.Add(new TypeMapCompositionMember(
                    composedName, composedName, GetSchemaPointer(composedReduced), GetSourceSchemaName(composedReduced), memberConst));
            }
        }

        if (members.Count > 0)
        {
            groups.Add(new TypeMapCompositionGroup(keyword, members));
        }
    }

    private static void RecurseCompositionTypes<TKeyword>(
        IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes,
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
                CollectTypeMapEntries(composedType, entries, visited);
            }
        }
    }

    private static string? TryGetTsName(TypeDeclaration td)
        => td.TryGetMetadata<string>(TsFinalNameKey, out string? n) && !string.IsNullOrEmpty(n) ? n : null;

    // The name shown for a property/member/item type: the assigned TS type name, else the inline TS primitive.
    private static string TsDisplayName(TypeDeclaration td)
    {
        if (TryGetTsName(td) is string n)
        {
            return n;
        }

        CoreTypes ct = td.ImpliedCoreTypes();
        if (ct.HasFlag(CoreTypes.String))
        {
            return "string";
        }

        if (ct.HasFlag(CoreTypes.Integer) || ct.HasFlag(CoreTypes.Number))
        {
            return "number";
        }

        if (ct.HasFlag(CoreTypes.Boolean))
        {
            return "boolean";
        }

        if (ct.HasFlag(CoreTypes.Null))
        {
            return "null";
        }

        if (ct.HasFlag(CoreTypes.Array))
        {
            return "unknown[]";
        }

        if (ct.HasFlag(CoreTypes.Object))
        {
            return "object";
        }

        return "unknown";
    }

    private static string InferKind(TypeDeclaration td)
    {
        if (td.ExplicitSingleConstantValue().ValueKind != SysJsonValueKind.Undefined)
        {
            return "const";
        }

        if (td.AnyOfConstantValues() is { Count: > 0 })
        {
            return "enum";
        }

        if (td.OneOfCompositionTypes() is { Count: > 0 })
        {
            return "oneOf";
        }

        if (td.AnyOfCompositionTypes() is { Count: > 0 })
        {
            return "anyOf";
        }

        if (td.AllOfCompositionTypes() is { Count: > 0 })
        {
            return "allOf";
        }

        CoreTypes ct = td.ImpliedCoreTypes();
        if (ct.HasFlag(CoreTypes.Object))
        {
            return "object";
        }

        if (ct.HasFlag(CoreTypes.Array))
        {
            return td.TupleType() is not null ? "tuple" : "array";
        }

        if (ct.HasFlag(CoreTypes.String))
        {
            return "string";
        }

        if (ct.HasFlag(CoreTypes.Integer))
        {
            return "integer";
        }

        if (ct.HasFlag(CoreTypes.Number))
        {
            return "number";
        }

        if (ct.HasFlag(CoreTypes.Boolean))
        {
            return "boolean";
        }

        if (ct.HasFlag(CoreTypes.Null))
        {
            return "null";
        }

        return "unknown";
    }

    private static string FormatJsonElement(SysJsonElement element) => element.ValueKind switch
    {
        SysJsonValueKind.String => $"\"{element.GetString()}\"",
        SysJsonValueKind.True => "true",
        SysJsonValueKind.False => "false",
        SysJsonValueKind.Null => "null",
        _ => element.GetRawText(),
    };

    private static string? GetSchemaPointer(TypeDeclaration td)
    {
        string location = td.LocatedSchema.Location.ToString();
        int hashIndex = location.IndexOf('#');
        if (hashIndex >= 0)
        {
            return location[hashIndex..];
        }

        return location.StartsWith(SchemaBaseUri, StringComparison.Ordinal) ? "#" : location;
    }

    private static string? GetSourceSchemaName(TypeDeclaration td)
    {
        string location = td.LocatedSchema.Location.ToString();
        if (location.StartsWith(SchemaBaseUri, StringComparison.Ordinal))
        {
            string rest = location[SchemaBaseUri.Length..];
            int hashIndex = rest.IndexOf('#');
            return hashIndex >= 0 ? rest[..hashIndex] : rest;
        }

        return null;
    }
}

using System.Reflection;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi.TypeScript.CodeGeneration;
using Corvus.Text.Json.OpenApi.TypeScript.Playground.Models;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using CorvusJsonElement = Corvus.Text.Json.JsonElement;
using OpenApiGeneratedFile = Corvus.Text.Json.OpenApi.CodeGeneration.GeneratedFile;
using PlaygroundGeneratedFile = Corvus.Text.Json.OpenApi.TypeScript.Playground.Models.GeneratedFile;

namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Services;

/// <summary>
/// Drives the TypeScript OpenAPI code generation pipeline entirely in WASM: the TypeScript model
/// engine emits the models (types + AOT validators + the shared model runtime), and the version-neutral
/// OpenAPI generators are driven with the <see cref="TypeScriptApiEmitter"/> to emit the byte-native
/// client.
/// </summary>
public class CodeGenerationService
{
    // The metadata keys under which the TypeScript model engine stores each type's assigned TS name and
    // its type-reference annotation (mirrors OpenApiGenerateCommand's Ts_FinalName / Ts_TypeRef keys).
    private const string TypeScriptFinalNameMetadataKey = "Ts_FinalName";

    private const string TypeScriptTypeRefMetadataKey = "Ts_TypeRef";

    private static readonly (string Uri, string ResourceSuffix)[] MetaschemaEntries =
    [
        ("http://json-schema.org/draft-04/schema", "metaschema.draft4.schema.json"),
        ("http://json-schema.org/draft-06/schema", "metaschema.draft6.schema.json"),
        ("http://json-schema.org/draft-07/schema", "metaschema.draft7.schema.json"),
        ("https://json-schema.org/draft/2019-09/schema", "metaschema.draft2019_09.schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/applicator", "metaschema.draft2019_09.meta.applicator.json"),
        ("https://json-schema.org/draft/2019-09/meta/content", "metaschema.draft2019_09.meta.content.json"),
        ("https://json-schema.org/draft/2019-09/meta/core", "metaschema.draft2019_09.meta.core.json"),
        ("https://json-schema.org/draft/2019-09/meta/format", "metaschema.draft2019_09.meta.format.json"),
        ("https://json-schema.org/draft/2019-09/meta/hyper-schema", "metaschema.draft2019_09.meta.hyper-schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/meta-data", "metaschema.draft2019_09.meta.meta-data.json"),
        ("https://json-schema.org/draft/2019-09/meta/validation", "metaschema.draft2019_09.meta.validation.json"),
        ("https://json-schema.org/draft/2020-12/schema", "metaschema.draft2020_12.schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/applicator", "metaschema.draft2020_12.meta.applicator.json"),
        ("https://json-schema.org/draft/2020-12/meta/content", "metaschema.draft2020_12.meta.content.json"),
        ("https://json-schema.org/draft/2020-12/meta/core", "metaschema.draft2020_12.meta.core.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-annotation", "metaschema.draft2020_12.meta.format-annotation.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-assertion", "metaschema.draft2020_12.meta.format-assertion.json"),
        ("https://json-schema.org/draft/2020-12/meta/hyper-schema", "metaschema.draft2020_12.meta.hyper-schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/meta-data", "metaschema.draft2020_12.meta.meta-data.json"),
        ("https://json-schema.org/draft/2020-12/meta/unevaluated", "metaschema.draft2020_12.meta.unevaluated.json"),
        ("https://json-schema.org/draft/2020-12/meta/validation", "metaschema.draft2020_12.meta.validation.json"),
        ("https://corvus-oss.org/json-schema/2020-12/schema", "metaschema.corvus.schema.json"),
        ("https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions", "metaschema.corvus.meta.corvus-extensions.json"),
    ];

    /// <summary>
    /// Parses the spec and returns the operation tree without generating code.
    /// This is the first step — user reviews and selects operations, then calls GenerateAsync.
    /// </summary>
    public Task<ListOperationsResult> ListOperationsAsync(IReadOnlyList<SpecFile> specFiles)
    {
        ListOperationsResult result = new();
        SpecFile? rootFile = specFiles.FirstOrDefault(f => f.IsRoot);
        if (rootFile is null)
        {
            result.Errors.Add("No root spec file specified.");
            return Task.FromResult(result);
        }

        try
        {
            using System.Text.Json.JsonDocument sysDoc = System.Text.Json.JsonDocument.Parse(rootFile.Content);
            if (!sysDoc.RootElement.TryGetProperty("openapi", out System.Text.Json.JsonElement versionEl)
                || versionEl.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                result.Errors.Add("Not a valid OpenAPI document: missing 'openapi' field.");
                return Task.FromResult(result);
            }

            result.SpecVersion = versionEl.GetString();
            PopulateOperationTree(rootFile.Content, result.Operations);
        }
        catch (System.Text.Json.JsonException ex)
        {
            result.Errors.Add($"JSON parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Generate the TypeScript client and model code from OpenAPI spec files.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        IReadOnlyList<SpecFile> specFiles,
        GenerationMode mode = GenerationMode.Client,
        IReadOnlySet<string>? includePaths = null,
        CancellationToken cancellationToken = default)
    {
        _ = mode;

        GenerationResult result = new();
        SpecFile? rootFile = specFiles.FirstOrDefault(f => f.IsRoot);
        if (rootFile is null)
        {
            result.Errors.Add("No root spec file specified.");
            return result;
        }

        try
        {
            try
            {
                using System.Text.Json.JsonDocument sysDoc = System.Text.Json.JsonDocument.Parse(rootFile.Content);
                if (!sysDoc.RootElement.TryGetProperty("openapi", out System.Text.Json.JsonElement versionEl)
                    || versionEl.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    result.Errors.Add("Not a valid OpenAPI document: missing 'openapi' field.");
                    return result;
                }

                result.SpecVersion = versionEl.GetString();
            }
            catch (System.Text.Json.JsonException ex)
            {
                result.Errors.Add($"JSON parse error: {ex.Message}");
                return result;
            }

            string specVersionCategory = result.SpecVersion switch
            {
                string v when v.StartsWith("3.2", StringComparison.Ordinal) => "3.2",
                string v when v.StartsWith("3.0", StringComparison.Ordinal) => "3.0",
                _ => "3.1",
            };

            string specFilePath = Path.GetFullPath(string.IsNullOrWhiteSpace(rootFile.Name) ? "openapi.json" : rootFile.Name);
            byte[] specBytes = System.Text.Encoding.UTF8.GetBytes(rootFile.Content);

            using ParsedJsonDocument<CorvusJsonElement> corvusDoc = ParsedJsonDocument<CorvusJsonElement>.Parse(specBytes);
            CorvusJsonElement specRoot = corvusDoc.RootElement;

            PopulateOperationTree(rootFile.Content, result.Operations);

            using ExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);
            RegisterExternalDocuments(referenceResolver, specFilePath, specFiles);

            OperationFilter? filter = includePaths is { Count: > 0 }
                ? new OperationFilter([.. includePaths], null)
                : null;

            SchemaReference[] schemaRefs;
            Dictionary<string, string> parameterNames;

            if (specVersionCategory == "3.2")
            {
                schemaRefs = OpenApi32CodeGenerator.CollectSchemaPointers(specRoot, out parameterNames, filter, referenceResolver);
            }
            else if (specVersionCategory == "3.0")
            {
                schemaRefs = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out parameterNames, filter, referenceResolver);
            }
            else
            {
                schemaRefs = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out parameterNames, filter, referenceResolver);
            }

            Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
            Dictionary<string, string> typeReferenceMap = new(StringComparer.Ordinal);
            List<PlaygroundGeneratedFile> modelFiles = [];

            if (schemaRefs.Length > 0)
            {
                (schemaTypeMap, typeReferenceMap, modelFiles) = await GenerateSchemaTypesAsync(
                    specFilePath,
                    specVersionCategory,
                    "Playground",
                    rootFile.Content,
                    specFiles,
                    schemaRefs,
                    parameterNames,
                    cancellationToken);
            }

            // The generated client imports the byte-native transport runtime and the model barrel; these
            // are the module specifiers the run interop's vfs plugin resolves to the vendored runtimes and
            // the generated model text (see wwwroot/js/playground-interop.js).
            TypeScriptApiEmitterOptions emitterOptions =
                new("@endjin/corvus-json-client-runtime", "./models/generated.js");

            IReadOnlyList<OpenApiGeneratedFile> clientFiles;
            if (specVersionCategory == "3.2")
            {
                OpenApi32CodeGenerator generator = new(
                    "Playground",
                    new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap));
                clientFiles = generator.GenerateUsing(
                    new TypeScriptApiEmitter(
                        new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap),
                        emitterOptions),
                    specRoot,
                    filter,
                    referenceResolver);
            }
            else if (specVersionCategory == "3.0")
            {
                OpenApi30CodeGenerator generator = new(
                    "Playground",
                    new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap));
                clientFiles = generator.GenerateUsing(
                    new TypeScriptApiEmitter(
                        new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap),
                        emitterOptions),
                    specRoot,
                    filter,
                    referenceResolver);
            }
            else
            {
                OpenApi31CodeGenerator generator = new(
                    "Playground",
                    new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap));
                clientFiles = generator.GenerateUsing(
                    new TypeScriptApiEmitter(
                        new TypeScriptSchemaTypeResolver(schemaTypeMap, typeReferenceMap),
                        emitterOptions),
                    specRoot,
                    filter,
                    referenceResolver);
            }

            foreach (PlaygroundGeneratedFile modelFile in modelFiles)
            {
                result.Files.Add(modelFile);
            }

            // Classify client files by filename: a request/response module ends in Request.ts/Response.ts;
            // everything else (the client class, its interface) is the client surface.
            foreach (OpenApiGeneratedFile clientFile in clientFiles)
            {
                bool isRequestResponse = clientFile.FileName.EndsWith("Request.ts", StringComparison.OrdinalIgnoreCase)
                    || clientFile.FileName.EndsWith("Response.ts", StringComparison.OrdinalIgnoreCase);

                result.Files.Add(new PlaygroundGeneratedFile
                {
                    FileName = clientFile.FileName,
                    Content = clientFile.Content,
                    IsClient = !isRequestResponse,
                    IsRequestResponse = isRequestResponse,
                });
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Code generation failed: {ex.Message}");
        }

        return result;
    }

    private static async Task<(Dictionary<string, string> SchemaTypeMap, Dictionary<string, string> TypeReferenceMap, List<PlaygroundGeneratedFile> ModelFiles)> GenerateSchemaTypesAsync(
        string specFilePath,
        string specVersion,
        string rootNamespace,
        string rootFileContent,
        IReadOnlyList<SpecFile> allFiles,
        SchemaReference[] schemaRefs,
        Dictionary<string, string> parameterNames,
        CancellationToken cancellationToken)
    {
        _ = rootNamespace;
        using PrepopulatedDocumentResolver documentResolver = new();
        AddMetaschema(documentResolver);

        System.Text.Json.JsonDocument mainDoc = System.Text.Json.JsonDocument.Parse(rootFileContent);
        RegisterDocument(documentResolver, specFilePath, mainDoc, specFilePath);

        foreach (SpecFile file in allFiles.Where(f => !f.IsRoot))
        {
            System.Text.Json.JsonDocument additionalDoc = System.Text.Json.JsonDocument.Parse(file.Content);
            RegisterDocument(documentResolver, ResolveDocumentIdentifier(specFilePath, file.Name), additionalDoc, specFilePath);
        }

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        vocabularyRegistry.RegisterVocabularies(Corvus.Json.CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

        IVocabulary defaultVocabulary = specVersion == "3.0"
            ? Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary
            : Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (SchemaReference schemaRef in schemaRefs)
        {
            JsonReference reference;
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');

            if (hashIndex == 0)
            {
                reference = new JsonReference(specFilePath, schemaRef.ResolvablePointer);
            }
            else if (hashIndex > 0)
            {
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                string fragment = schemaRef.ResolvablePointer[hashIndex..];
                reference = new JsonReference(ResolveDocumentPath(docPart), fragment);
            }
            else
            {
                reference = new JsonReference(ResolveDocumentPath(schemaRef.ResolvablePointer), "#");
            }

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                defaultVocabulary,
                rebaseAsRoot: false,
                cancellationToken);

            pointerToType[schemaRef.PositionalPointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        await Task.Yield();

        // Emit idiomatic TypeScript models (types + AOT validators) + the shared model runtime. The model
        // module imports its runtime from "./corvus-runtime.js" (the run interop resolves that to the
        // vendored model runtime). AlwaysAssertFormat mirrors the CLI/example-recipe generation.
        TypeScriptLanguageProvider languageProvider = TypeScriptLanguageProvider.DefaultWithOptions(
            new TypeScriptLanguageProvider.Options(
                AlwaysAssertFormat: true,
                RuntimeModuleSpecifier: "./corvus-runtime.js",
                EmitTypeSurface: true,
                ModulePerType: false));
        languageProvider.RegisterNameHeuristics(new OpenApiSchemaNameHeuristic(parameterNames));

        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        // The model engine writes the single generated.ts barrel and corvus-runtime.ts under models/; the
        // generated client imports the barrel from ./models/generated.js and the runtime from
        // ./corvus-runtime.js (relative to the barrel). Prefix the display/file names with models/ so the
        // file tree groups them and the run interop can resolve ./models/generated.js.
        List<PlaygroundGeneratedFile> modelFiles = [];
        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            modelFiles.Add(new PlaygroundGeneratedFile
            {
                FileName = "models/" + codeFile.FileName,
                Content = codeFile.FileContent,
                IsModel = true,
            });
        }

        // Build the pointer -> TS final name map (schemaTypeMap) and the pointer -> TS type-reference map
        // (typeReferenceMap), read from the Ts_FinalName / Ts_TypeRef metadata assigned during generation.
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        Dictionary<string, string> typeReferenceMap = new(StringComparer.Ordinal);
        foreach ((string pointerStr, TypeDeclaration typeDeclaration) in pointerToType)
        {
            TypeDeclaration reduced = typeDeclaration.ReducedTypeDeclaration().ReducedType;

            if (TryGetTsFinalName(reduced, out string? finalName))
            {
                schemaTypeMap[pointerStr] = finalName;
            }

            if (reduced.TryGetMetadata(TypeScriptTypeRefMetadataKey, out string? typeRef)
                && !string.IsNullOrEmpty(typeRef))
            {
                typeReferenceMap[pointerStr] = typeRef;
            }

            AddChildTypesToMap(reduced, schemaTypeMap, typeReferenceMap, new HashSet<TypeDeclaration>());
        }

        return (schemaTypeMap, typeReferenceMap, modelFiles);
    }

    private static bool TryGetTsFinalName(TypeDeclaration reduced, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? finalName)
    {
        if (reduced.TryGetMetadata(TypeScriptFinalNameMetadataKey, out string? name)
            && !string.IsNullOrEmpty(name))
        {
            finalName = name;
            return true;
        }

        finalName = null;
        return false;
    }

    private static void RegisterExternalDocuments(
        ExternalReferenceResolver referenceResolver,
        string specFilePath,
        IReadOnlyList<SpecFile> specFiles)
    {
        foreach (SpecFile file in specFiles.Where(f => !f.IsRoot))
        {
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(file.Content);
            string documentIdentifier = ResolveDocumentIdentifier(specFilePath, file.Name);

            if (Uri.TryCreate(documentIdentifier, UriKind.Absolute, out Uri? absoluteUri) && !absoluteUri.IsFile)
            {
                referenceResolver.AddDocument(absoluteUri, fileBytes);
            }
            else if (Path.IsPathFullyQualified(documentIdentifier))
            {
                referenceResolver.AddDocument(new Uri(documentIdentifier), fileBytes);
            }
            else
            {
                referenceResolver.AddDocument(file.Name, fileBytes);
            }
        }
    }

    private static void RegisterDocument(
        PrepopulatedDocumentResolver documentResolver,
        string canonicalUri,
        System.Text.Json.JsonDocument document,
        string specFilePath)
    {
        documentResolver.AddDocument(canonicalUri, document);
        TryRegisterDocumentId(documentResolver, document);

        if (!Path.IsPathFullyQualified(canonicalUri) && !Uri.TryCreate(canonicalUri, UriKind.Absolute, out _))
        {
            documentResolver.AddDocument(ResolveDocumentIdentifier(specFilePath, canonicalUri), document);
        }
    }

    private static void TryRegisterDocumentId(PrepopulatedDocumentResolver documentResolver, System.Text.Json.JsonDocument document)
    {
        if (document.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement)
            && idElement.ValueKind == System.Text.Json.JsonValueKind.String
            && idElement.GetString() is string id)
        {
            documentResolver.AddDocument(id, document);
        }
    }

    private static string ResolveDocumentIdentifier(string specFilePath, string documentName)
    {
        if (Uri.TryCreate(documentName, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri.IsFile ? absoluteUri.LocalPath : absoluteUri.AbsoluteUri;
        }

        if (Path.IsPathFullyQualified(documentName))
        {
            return Path.GetFullPath(documentName);
        }

        string baseDirectory = Path.GetDirectoryName(specFilePath) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, documentName));
    }

    private static string ResolveDocumentPath(string docPart)
    {
        if (Uri.TryCreate(docPart, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
        {
            return docPart;
        }

        return Path.GetFullPath(docPart);
    }

    private static void AddChildTypesToMap(
        TypeDeclaration parentType,
        Dictionary<string, string> schemaTypeMap,
        Dictionary<string, string> typeReferenceMap,
        HashSet<TypeDeclaration> visited)
    {
        foreach (TypeDeclaration child in parentType.Children())
        {
            TypeDeclaration reducedChild = child.ReducedTypeDeclaration().ReducedType;
            if (!visited.Add(reducedChild))
            {
                continue;
            }

            if (reducedChild.LocatedSchema.RootDocumentPointer is { Length: > 0 } rootPointer)
            {
                string key = "#" + rootPointer;

                if (TryGetTsFinalName(reducedChild, out string? childFinalName))
                {
                    schemaTypeMap.TryAdd(key, childFinalName);
                }

                if (reducedChild.TryGetMetadata(TypeScriptTypeRefMetadataKey, out string? childTypeRef)
                    && !string.IsNullOrEmpty(childTypeRef))
                {
                    typeReferenceMap.TryAdd(key, childTypeRef);
                }
            }

            AddChildTypesToMap(reducedChild, schemaTypeMap, typeReferenceMap, visited);
        }
    }

    private static void PopulateOperationTree(string rootFileContent, List<OperationNode> operations)
    {
        using System.Text.Json.JsonDocument sysDoc = System.Text.Json.JsonDocument.Parse(rootFileContent);
        if (!sysDoc.RootElement.TryGetProperty("paths", out System.Text.Json.JsonElement paths))
        {
            return;
        }

        // Collect all operations with their tags.
        List<OperationNode> allOps = [];
        foreach (System.Text.Json.JsonProperty pathItem in paths.EnumerateObject())
        {
            foreach (System.Text.Json.JsonProperty method in pathItem.Value.EnumerateObject())
            {
                if (!IsHttpMethod(method.Name))
                {
                    continue;
                }

                string? operationId = method.Value.TryGetProperty("operationId", out System.Text.Json.JsonElement opId)
                    ? opId.GetString()
                    : null;
                string? summary = method.Value.TryGetProperty("summary", out System.Text.Json.JsonElement sum)
                    ? sum.GetString()
                    : null;

                List<string> tags = [];
                if (method.Value.TryGetProperty("tags", out System.Text.Json.JsonElement tagsEl)
                    && tagsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (System.Text.Json.JsonElement tagEl in tagsEl.EnumerateArray())
                    {
                        string? tag = tagEl.GetString();
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }

                allOps.Add(new OperationNode
                {
                    Path = pathItem.Name,
                    Method = method.Name.ToUpperInvariant(),
                    OperationId = operationId,
                    Summary = summary,
                    Tags = tags,
                });
            }
        }

        // Group by tag. Operations with multiple tags appear under each tag.
        // Operations with no tags go under "Untagged".
        bool anyTags = allOps.Any(op => op.Tags.Count > 0);

        if (anyTags)
        {
            Dictionary<string, OperationNode> tagGroups = new(StringComparer.OrdinalIgnoreCase);

            foreach (OperationNode op in allOps)
            {
                IEnumerable<string> effectiveTags = op.Tags.Count > 0
                    ? op.Tags
                    : ["Untagged"];

                foreach (string tag in effectiveTags)
                {
                    if (!tagGroups.TryGetValue(tag, out OperationNode? group))
                    {
                        group = new OperationNode { GroupName = tag, Path = tag };
                        tagGroups[tag] = group;
                    }

                    group.Children.Add(op);
                }
            }

            foreach (OperationNode group in tagGroups.Values.OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase))
            {
                operations.Add(group);
            }
        }
        else
        {
            // No tags — group by path (original behaviour).
            foreach (System.Text.Json.JsonProperty pathItem in paths.EnumerateObject())
            {
                OperationNode pathNode = new() { Path = pathItem.Name, GroupName = pathItem.Name };
                foreach (OperationNode op in allOps.Where(o => o.Path == pathItem.Name))
                {
                    pathNode.Children.Add(op);
                }

                if (pathNode.Children.Count > 0)
                {
                    operations.Add(pathNode);
                }
            }
        }
    }

    private static bool IsHttpMethod(string name) =>
        name is "get" or "post" or "put" or "delete" or "patch" or "head" or "options" or "trace";

    private static void AddMetaschema(PrepopulatedDocumentResolver documentResolver)
    {
        Assembly assembly = typeof(CodeGenerationService).Assembly;
        string[] resourceNames = assembly.GetManifestResourceNames();

        foreach ((string uri, string suffix) in MetaschemaEntries)
        {
            string? resourceName = Array.Find(resourceNames, n => n.EndsWith(suffix, StringComparison.Ordinal));
            if (resourceName is null)
            {
                throw new InvalidOperationException($"Embedded resource ending with '{suffix}' not found.");
            }

            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
            using StreamReader reader = new(stream);
            documentResolver.AddDocument(uri, System.Text.Json.JsonDocument.Parse(reader.ReadToEnd()));
        }
    }
}

public enum GenerationMode
{
    Client,
    Server,
    Both,
}
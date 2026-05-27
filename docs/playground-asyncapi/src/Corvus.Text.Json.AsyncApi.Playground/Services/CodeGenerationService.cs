using System.Collections.Immutable;
using System.Reflection;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.AsyncApi.Playground.Models;
using Corvus.Text.Json.CodeGeneration;
using CorvusJsonElement = Corvus.Text.Json.JsonElement;
using AsyncApiGeneratedFile = Corvus.Text.Json.AsyncApi.CodeGeneration.GeneratedFile;
using PlaygroundGeneratedFile = Corvus.Text.Json.AsyncApi.Playground.Models.GeneratedFile;

namespace Corvus.Text.Json.AsyncApi.Playground.Services;

/// <summary>
/// Drives the V5 AsyncAPI code generation pipeline entirely in WASM.
/// </summary>
public class CodeGenerationService
{
    private static readonly ImmutableArray<string> DefaultDisabledNamingHeuristics =
        ["DocumentationNameHeuristic"];

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
    /// Parses the spec and returns the channel/operation tree without generating code.
    /// This is the first step — user reviews and selects operations, then calls GenerateAsync.
    /// </summary>
    public Task<ListChannelsResult> ListChannelsAsync(IReadOnlyList<SpecFile> specFiles)
    {
        ListChannelsResult result = new();
        SpecFile? rootFile = specFiles.FirstOrDefault(f => f.IsRoot);
        if (rootFile is null)
        {
            result.Errors.Add("No root spec file specified.");
            return Task.FromResult(result);
        }

        try
        {
            using System.Text.Json.JsonDocument sysDoc = System.Text.Json.JsonDocument.Parse(rootFile.Content);
            if (!sysDoc.RootElement.TryGetProperty("asyncapi", out System.Text.Json.JsonElement versionEl)
                || versionEl.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                result.Errors.Add("Not a valid AsyncAPI document: missing 'asyncapi' field.");
                return Task.FromResult(result);
            }

            result.SpecVersion = versionEl.GetString();
            PopulateChannelTree(rootFile.Content, result.Channels);
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
    /// Generate C# handler, producer, and model code from AsyncAPI spec files.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        IReadOnlyList<SpecFile> specFiles,
        GenerationMode mode = GenerationMode.Both,
        IReadOnlySet<string>? includeChannels = null,
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
                if (!sysDoc.RootElement.TryGetProperty("asyncapi", out System.Text.Json.JsonElement versionEl)
                    || versionEl.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    result.Errors.Add("Not a valid AsyncAPI document: missing 'asyncapi' field.");
                    return result;
                }

                result.SpecVersion = versionEl.GetString();
            }
            catch (System.Text.Json.JsonException ex)
            {
                result.Errors.Add($"JSON parse error: {ex.Message}");
                return result;
            }

            string specFilePath = Path.GetFullPath(string.IsNullOrWhiteSpace(rootFile.Name) ? "asyncapi.json" : rootFile.Name);
            byte[] specBytes = System.Text.Encoding.UTF8.GetBytes(rootFile.Content);

            using ParsedJsonDocument<CorvusJsonElement> corvusDoc = ParsedJsonDocument<CorvusJsonElement>.Parse(specBytes);
            CorvusJsonElement specRoot = corvusDoc.RootElement;

            PopulateChannelTree(rootFile.Content, result.Channels);

            if (!IsAsyncApi26Version(result.SpecVersion) && !IsAsyncApi30Version(result.SpecVersion))
            {
                result.Errors.Add($"Unsupported AsyncAPI version: {result.SpecVersion}");
                return result;
            }

            using AsyncApiExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);
            RegisterExternalDocuments(referenceResolver, specFilePath, specFiles);

            OperationFilter? filter = includeChannels is { Count: > 0 }
                ? new OperationFilter([.. includeChannels], null)
                : null;

            string[] schemaPointers = IsAsyncApi26Version(result.SpecVersion)
                ? AsyncApi26CodeGenerator.CollectSchemaPointers(specRoot, filter, referenceResolver)
                : AsyncApi30CodeGenerator.CollectSchemaPointers(specRoot, filter, referenceResolver);

            Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
            List<PlaygroundGeneratedFile> modelFiles = [];

            if (schemaPointers.Length > 0)
            {
                (schemaTypeMap, modelFiles) = await GenerateSchemaTypesAsync(
                    specFilePath,
                    "Playground",
                    rootFile.Content,
                    specFiles,
                    schemaPointers,
                    cancellationToken);
            }

            IReadOnlyList<AsyncApiGeneratedFile> asyncApiFiles;
            if (IsAsyncApi26Version(result.SpecVersion))
            {
                AsyncApi26CodeGenerator generator = new("Playground", schemaTypeMap);
                asyncApiFiles = generator.Generate(specRoot, filter, referenceResolver);
            }
            else
            {
                AsyncApi30CodeGenerator generator = new("Playground", schemaTypeMap);
                asyncApiFiles = generator.Generate(specRoot, filter, referenceResolver);
            }

            foreach (PlaygroundGeneratedFile modelFile in modelFiles)
            {
                result.Files.Add(modelFile);
            }

            foreach (AsyncApiGeneratedFile genFile in asyncApiFiles)
            {
                bool isHandler = genFile.FileName.StartsWith("IReceive", StringComparison.Ordinal)
                    || genFile.FileName.EndsWith("Handler.cs", StringComparison.Ordinal)
                    || genFile.FileName.EndsWith("Consumer.cs", StringComparison.Ordinal);

                bool isProducer = genFile.FileName.EndsWith("Producer.cs", StringComparison.Ordinal);

                result.Files.Add(new PlaygroundGeneratedFile
                {
                    FileName = genFile.FileName,
                    Content = genFile.Content,
                    IsHandler = isHandler,
                    IsProducer = isProducer && !isHandler,
                    IsModel = !isHandler && !isProducer,
                });
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Code generation failed: {ex.Message}");
        }

        return result;
    }

    private static async Task<(Dictionary<string, string> SchemaTypeMap, List<PlaygroundGeneratedFile> ModelFiles)> GenerateSchemaTypesAsync(
        string specFilePath,
        string rootNamespace,
        string rootFileContent,
        IReadOnlyList<SpecFile> allFiles,
        string[] schemaPointers,
        CancellationToken cancellationToken)
    {
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

        IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string schemaPointer in schemaPointers)
        {
            JsonReference reference;
            int hashIndex = schemaPointer.IndexOf('#');

            if (hashIndex == 0)
            {
                reference = new JsonReference(specFilePath, schemaPointer);
            }
            else if (hashIndex > 0)
            {
                string docPart = schemaPointer[..hashIndex];
                string fragment = schemaPointer[hashIndex..];
                reference = new JsonReference(ResolveDocumentPath(docPart), fragment);
            }
            else
            {
                reference = new JsonReference(ResolveDocumentPath(schemaPointer), "#");
            }

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                defaultVocabulary,
                rebaseAsRoot: false,
                cancellationToken);

            pointerToType[schemaPointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        await Task.Yield();

        CSharpLanguageProvider.Options options = new(
            rootNamespace,
            useOptionalNameHeuristics: true,
            alwaysAssertFormat: true,
            disabledNamingHeuristics: [.. DefaultDisabledNamingHeuristics],
            useImplicitOperatorString: true,
            addExplicitUsings: true);
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(AsyncApiSchemaNameHeuristic.Instance);

        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        List<PlaygroundGeneratedFile> modelFiles = [];
        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            modelFiles.Add(new PlaygroundGeneratedFile
            {
                FileName = codeFile.FileName,
                Content = codeFile.FileContent,
                IsModel = true,
            });
        }

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        foreach ((string pointerStr, TypeDeclaration typeDeclaration) in pointerToType)
        {
            TypeDeclaration reduced = typeDeclaration.ReducedTypeDeclaration().ReducedType;
            if (reduced.HasDotnetTypeName())
            {
                schemaTypeMap[pointerStr] = reduced.FullyQualifiedDotnetTypeName();
            }

            AddChildTypesToMap(reduced, schemaTypeMap, new HashSet<TypeDeclaration>());
        }

        return (schemaTypeMap, modelFiles);
    }

    private static bool IsAsyncApi26Version(string? specVersion)
    {
        return specVersion is not null && specVersion.StartsWith("2.6", StringComparison.Ordinal);
    }

    private static bool IsAsyncApi30Version(string? specVersion)
    {
        return specVersion is not null && specVersion.StartsWith("3.0", StringComparison.Ordinal);
    }

    private static void RegisterExternalDocuments(
        AsyncApiExternalReferenceResolver referenceResolver,
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
        HashSet<TypeDeclaration> visited)
    {
        foreach (TypeDeclaration child in parentType.Children())
        {
            TypeDeclaration reducedChild = child.ReducedTypeDeclaration().ReducedType;
            if (!visited.Add(reducedChild))
            {
                continue;
            }

            if (reducedChild.HasDotnetTypeName()
                && reducedChild.LocatedSchema.RootDocumentPointer is { Length: > 0 } rootPointer)
            {
                schemaTypeMap.TryAdd("#" + rootPointer, reducedChild.FullyQualifiedDotnetTypeName());
            }

            AddChildTypesToMap(reducedChild, schemaTypeMap, visited);
        }
    }

    private static void PopulateChannelTree(string rootFileContent, List<ChannelNode> channels)
    {
        using System.Text.Json.JsonDocument sysDoc = System.Text.Json.JsonDocument.Parse(rootFileContent);

        // Build channel→operations grouping
        Dictionary<string, ChannelNode> channelGroups = new(StringComparer.OrdinalIgnoreCase);

        if (sysDoc.RootElement.TryGetProperty("operations", out System.Text.Json.JsonElement operations))
        {
            foreach (System.Text.Json.JsonProperty operation in operations.EnumerateObject())
            {
                string operationId = operation.Name;

                string? action = operation.Value.TryGetProperty("action", out System.Text.Json.JsonElement actionEl)
                    ? actionEl.GetString()
                    : null;

                if (action is null)
                {
                    continue;
                }

                string channelAddress = ResolveChannelAddress(operation.Value, sysDoc.RootElement);
                AddOperationNode(channelGroups, channelAddress, action, operationId, operation.Value);
            }
        }
        else if (sysDoc.RootElement.TryGetProperty("channels", out System.Text.Json.JsonElement asyncApi26Channels) &&
            asyncApi26Channels.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (System.Text.Json.JsonProperty channel in asyncApi26Channels.EnumerateObject())
            {
                if (channel.Value.TryGetProperty("publish", out System.Text.Json.JsonElement publish) &&
                    publish.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    AddOperationNode(channelGroups, channel.Name, "receive", GetOperationId(publish, "publish"), publish);
                }

                if (channel.Value.TryGetProperty("subscribe", out System.Text.Json.JsonElement subscribe) &&
                    subscribe.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    AddOperationNode(channelGroups, channel.Name, "send", GetOperationId(subscribe, "subscribe"), subscribe);
                }
            }
        }

        foreach (ChannelNode group in channelGroups.Values.OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase))
        {
            channels.Add(group);
        }
    }

    private static void AddOperationNode(
        Dictionary<string, ChannelNode> channelGroups,
        string channelAddress,
        string action,
        string operationId,
        System.Text.Json.JsonElement operation)
    {
        string? summary = operation.TryGetProperty("summary", out System.Text.Json.JsonElement sum)
            ? sum.GetString()
            : null;

        List<string> tags = [];
        if (operation.TryGetProperty("tags", out System.Text.Json.JsonElement tagsEl)
            && tagsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement tagEl in tagsEl.EnumerateArray())
            {
                if (tagEl.TryGetProperty("name", out System.Text.Json.JsonElement nameEl)
                    && nameEl.GetString() is string tagName)
                {
                    tags.Add(tagName);
                }
            }
        }

        if (!channelGroups.TryGetValue(channelAddress, out ChannelNode? group))
        {
            group = new ChannelNode { GroupName = channelAddress, Channel = channelAddress };
            channelGroups[channelAddress] = group;
        }

        group.Children.Add(new ChannelNode
        {
            Channel = channelAddress,
            Action = action,
            OperationId = operationId,
            Summary = summary,
            Tags = tags,
        });
    }

    private static string GetOperationId(System.Text.Json.JsonElement operation, string fallback)
    {
        return operation.TryGetProperty("operationId", out System.Text.Json.JsonElement operationId) &&
            operationId.ValueKind == System.Text.Json.JsonValueKind.String
                ? operationId.GetString()!
                : fallback;
    }

    private static string ResolveChannelAddress(
        System.Text.Json.JsonElement operation,
        System.Text.Json.JsonElement root)
    {
        if (!operation.TryGetProperty("channel", out System.Text.Json.JsonElement channelRef))
        {
            return "(unknown)";
        }

        // Channel can be a $ref like "#/channels/lightingMeasured"
        if (channelRef.TryGetProperty("$ref", out System.Text.Json.JsonElement refEl)
            && refEl.GetString() is string refStr
            && refStr.StartsWith("#/channels/", StringComparison.Ordinal))
        {
            string channelName = refStr["#/channels/".Length..];

            if (root.TryGetProperty("channels", out System.Text.Json.JsonElement channelsEl)
                && channelsEl.TryGetProperty(channelName, out System.Text.Json.JsonElement channelDef))
            {
                if (channelDef.TryGetProperty("address", out System.Text.Json.JsonElement addrEl)
                    && addrEl.GetString() is string addr)
                {
                    return addr;
                }

                return channelName;
            }

            return channelName;
        }

        return "(inline)";
    }

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
    Producer,
    Consumer,
    Both,
}
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace XmlDocToMarkdown;

/// <summary>
/// Reads a portable PDB to extract source file paths and line numbers for types and members.
/// Uses three PDB features for maximum coverage:
/// <list type="bullet">
///   <item>SourceLink JSON — maps local paths to GitHub repository URLs</item>
///   <item>TypeDefinitionDocuments — maps interfaces, enums, and delegates to their source documents</item>
///   <item>EmbeddedSource — enables scanning for precise type/member declaration line numbers</item>
/// </list>
/// </summary>
public sealed class SourceLinkResolver : IDisposable
{
    private readonly MetadataReaderProvider _pdbProvider;
    private readonly MetadataReader _pdbReader;
    private readonly PEReader _peReader;
    private readonly MetadataReader _peMetadata;

    /// <summary>
    /// Key: type or member identifier matching the patterns used in the doc model.
    /// Value: GitHub URL with line number fragment.
    /// </summary>
    private readonly Dictionary<string, string> _sourceUrls = new(StringComparer.Ordinal);

    /// <summary>
    /// Fallback URL base (from --repo-url + git) for files not covered by SourceLink.
    /// </summary>
    private readonly string _repoBaseUrl;
    private readonly string _localPathPrefix;
    private readonly string _branch;

    /// <summary>
    /// SourceLink mappings parsed from the PDB: local path prefix → URL template.
    /// The URL template has a '*' wildcard replaced by the relative file path.
    /// </summary>
    private readonly List<(string LocalPrefix, string UrlTemplate)> _sourceLinkMappings = [];

    /// <summary>
    /// Maps PDB document handles to file paths.
    /// </summary>
    private readonly Dictionary<DocumentHandle, string> _documentPaths = [];

    /// <summary>
    /// Maps PDB document paths to their embedded source lines (from EmbedAllSources).
    /// </summary>
    private readonly Dictionary<string, string[]> _embeddedSourceCache = new(StringComparer.OrdinalIgnoreCase);

    // Portable PDB custom debug info GUIDs
    private static readonly Guid SourceLinkGuid = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    private static readonly Guid EmbeddedSourceGuid = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
    private static readonly Guid TypeDefinitionDocumentsGuid = new("932E74BC-DBA9-4478-8D46-0F32A7BAB3D3");

    /// <summary>
    /// Creates a new resolver by reading the PDB and PE metadata.
    /// </summary>
    public SourceLinkResolver(string pdbPath, string assemblyPath, string repoUrl, string branch, string repoRoot)
    {
        _branch = branch;
        _repoBaseUrl = $"{repoUrl.TrimEnd('/')}/blob/{branch}/";
        _localPathPrefix = repoRoot.TrimEnd('\\', '/').Replace('\\', '/') + "/";

        // Open PDB
        using FileStream pdbStream = File.OpenRead(pdbPath);
        _pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream, MetadataStreamOptions.PrefetchMetadata);
        _pdbReader = _pdbProvider.GetMetadataReader();

        // Open PE (assembly) via PEReader
        FileStream peStream = File.OpenRead(assemblyPath);
        _peReader = new PEReader(peStream);
        _peMetadata = _peReader.GetMetadataReader();
    }

    /// <summary>
    /// Scans the PDB and builds the source URL map.
    /// Call once after construction, before querying.
    /// </summary>
    public void Build()
    {
        // Step 0: Parse SourceLink JSON from PDB
        ParseSourceLink();

        // Step 1: Build document path cache and load embedded source
        foreach (DocumentHandle docHandle in _pdbReader.Documents)
        {
            Document doc = _pdbReader.GetDocument(docHandle);
            if (doc.Name.IsNil)
            {
                continue;
            }

            string path = _pdbReader.GetString(doc.Name);
            _documentPaths[docHandle] = path;
            LoadEmbeddedSource(docHandle, path);
        }

        Console.WriteLine($"  Loaded {_embeddedSourceCache.Count} embedded source documents from PDB.");

        // Step 2: Build type token → (full name, short name) map from PE metadata
        Dictionary<int, (string FullName, string ShortName, TypeDefinitionHandle Handle)> typeTokenToInfo = [];
        foreach (TypeDefinitionHandle typeHandle in _peMetadata.TypeDefinitions)
        {
            TypeDefinition typeDef = _peMetadata.GetTypeDefinition(typeHandle);
            string ns = typeDef.Namespace.IsNil ? "" : _peMetadata.GetString(typeDef.Namespace);
            string name = _peMetadata.GetString(typeDef.Name);

            if (name.StartsWith('<') || name.Contains('$'))
            {
                continue;
            }

            string fullName = BuildFullTypeName(typeDef, ns, name);
            string shortName = name;
            int backtick = shortName.IndexOf('`');
            if (backtick >= 0)
            {
                shortName = shortName[..backtick];
            }

            typeTokenToInfo[MetadataTokens.GetToken(typeHandle)] = (fullName, shortName, typeHandle);
        }

        // Step 3: Walk method debug info — build member URLs and identify type→file mappings
        // Key: type full name, Value: preferred source file path
        Dictionary<string, string> typeFileMap = new(StringComparer.Ordinal);
        // Track all files per type (for primary file selection)
        Dictionary<string, List<string>> typeAllFiles = new(StringComparer.Ordinal);

        foreach (MethodDebugInformationHandle mdiHandle in _pdbReader.MethodDebugInformation)
        {
            MethodDebugInformation mdi = _pdbReader.GetMethodDebugInformation(mdiHandle);
            if (mdi.Document.IsNil)
            {
                continue;
            }

            if (!_documentPaths.TryGetValue(mdi.Document, out string? filePath))
            {
                continue;
            }

            // Get the first non-hidden sequence point line
            int firstLine = int.MaxValue;
            foreach (SequencePoint sp in mdi.GetSequencePoints())
            {
                if (!sp.IsHidden && sp.StartLine < firstLine)
                {
                    firstLine = sp.StartLine;
                }
            }

            if (firstLine == int.MaxValue)
            {
                continue;
            }

            // Map back to MethodDefinition in PE
            MethodDefinitionHandle methodHandle = MetadataTokens.MethodDefinitionHandle(
                MetadataTokens.GetRowNumber(mdiHandle));
            if (methodHandle.IsNil)
            {
                continue;
            }

            MethodDefinition methodDef = _peMetadata.GetMethodDefinition(methodHandle);
            int typeToken = MetadataTokens.GetToken(methodDef.GetDeclaringType());
            if (!typeTokenToInfo.TryGetValue(typeToken, out var typeInfo))
            {
                continue;
            }

            // Store member-level source URL (using PDB sequence point line)
            string methodName = _peMetadata.GetString(methodDef.Name);
            string memberKey = methodName == ".ctor"
                ? $"{typeInfo.FullName}.#ctor"
                : $"{typeInfo.FullName}.{methodName}";

            string? memberUrl = BuildSourceUrl(filePath, firstLine);
            if (memberUrl is not null)
            {
                // Store with full parameter signature for overload disambiguation
                string paramSuffix = XmlDocIdTypeProvider.FormatMethodParams(_peMetadata, methodDef);
                string fullKey = $"{memberKey}{paramSuffix}";

                if (!_sourceUrls.ContainsKey(fullKey))
                {
                    _sourceUrls[fullKey] = memberUrl;
                }

                // Also store without parameters (first overload wins for non-overloaded members)
                if (!_sourceUrls.ContainsKey(memberKey))
                {
                    _sourceUrls[memberKey] = memberUrl;
                }
            }

            // Track files for this type
            if (!typeAllFiles.TryGetValue(typeInfo.FullName, out List<string>? files))
            {
                files = [];
                typeAllFiles[typeInfo.FullName] = files;
            }

            if (!files.Contains(filePath))
            {
                files.Add(filePath);
            }
        }

        // Select primary file for each type found via methods
        foreach (var kvp in typeAllFiles)
        {
            string typeName = kvp.Key;
            if (!typeTokenToInfo.Values.Any(t => t.FullName == typeName))
            {
                continue;
            }

            var info = typeTokenToInfo.Values.First(t => t.FullName == typeName);
            typeFileMap[typeName] = SelectPrimaryFile(kvp.Value, info.ShortName);
        }

        // Step 4: Use TypeDefinitionDocuments for types without method debug info
        foreach (var kvp in typeTokenToInfo)
        {
            if (typeFileMap.ContainsKey(kvp.Value.FullName))
            {
                continue;
            }

            string? docPath = GetTypeDefinitionDocument(kvp.Value.Handle, kvp.Value.ShortName);
            if (docPath is not null)
            {
                typeFileMap[kvp.Value.FullName] = docPath;
            }
        }

        // Step 4b: For types still not in typeFileMap, walk up the declaring type chain
        foreach (var kvp in typeTokenToInfo)
        {
            if (typeFileMap.ContainsKey(kvp.Value.FullName))
            {
                continue;
            }

            TypeDefinition typeDef = _peMetadata.GetTypeDefinition(kvp.Value.Handle);
            TypeDefinitionHandle parentHandle = typeDef.GetDeclaringType();
            while (!parentHandle.IsNil)
            {
                int parentToken = MetadataTokens.GetToken(parentHandle);
                if (typeTokenToInfo.TryGetValue(parentToken, out var parentInfo) &&
                    typeFileMap.TryGetValue(parentInfo.FullName, out string? parentFile))
                {
                    typeFileMap[kvp.Value.FullName] = parentFile;
                    break;
                }

                TypeDefinition parentDef = _peMetadata.GetTypeDefinition(parentHandle);
                parentHandle = parentDef.GetDeclaringType();
            }
        }

        // Step 5: Build type-level URLs by scanning embedded source for declaration lines
        foreach (var kvp in typeTokenToInfo)
        {
            string typeName = kvp.Value.FullName;
            string shortName = kvp.Value.ShortName;

            if (!typeFileMap.TryGetValue(typeName, out string? typeFile))
            {
                continue;
            }

            int declLine = FindTypeDeclarationLine(typeFile, shortName);
            int line = declLine > 0 ? declLine : 1;
            string? url = BuildSourceUrl(typeFile, line);
            if (url is not null)
            {
                _sourceUrls[typeName] = url;
            }
        }

        // Step 6: Resolve members without PDB sequence points (interface/abstract members)
        foreach (var kvp in typeTokenToInfo)
        {
            string typeName = kvp.Value.FullName;

            if (!typeFileMap.TryGetValue(typeName, out string? typeFile))
            {
                continue;
            }

            TypeDefinition typeDef = _peMetadata.GetTypeDefinition(kvp.Value.Handle);

            foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods())
            {
                MethodDefinition methodDef = _peMetadata.GetMethodDefinition(methodHandle);
                string methodName = _peMetadata.GetString(methodDef.Name);

                string memberKey = methodName == ".ctor"
                    ? $"{typeName}.#ctor"
                    : $"{typeName}.{methodName}";

                if (_sourceUrls.ContainsKey(memberKey))
                {
                    continue; // Already resolved from sequence points
                }

                // Scan embedded source for the member declaration
                string scanName = methodName;
                if (methodName.StartsWith("get_") || methodName.StartsWith("set_"))
                {
                    scanName = methodName[4..];
                }
                else if (methodName.StartsWith("add_") || methodName.StartsWith("remove_"))
                {
                    scanName = methodName[(methodName.IndexOf('_') + 1)..];
                }

                int memberLine = FindMemberDeclarationLine(typeFile, scanName);
                if (memberLine > 0)
                {
                    string? url = BuildSourceUrl(typeFile, memberLine);
                    if (url is not null)
                    {
                        _sourceUrls[memberKey] = url;
                    }
                }
            }
        }

        Console.WriteLine($"  Built source URL map with {_sourceUrls.Count} entries.");
    }

    /// <summary>
    /// Returns the GitHub source URL for a type, or null if not found.
    /// </summary>
    public string? GetTypeSourceUrl(string typeFullName)
    {
        if (_sourceUrls.TryGetValue(typeFullName, out string? url))
        {
            return url;
        }

        string dotForm = typeFullName.Replace('+', '.');
        if (_sourceUrls.TryGetValue(dotForm, out url))
        {
            return url;
        }

        // Try with generic arity variants
        string nameWithoutArity = StripGenericArity(dotForm);
        for (int arity = 1; arity <= 4; arity++)
        {
            if (_sourceUrls.TryGetValue($"{nameWithoutArity}`{arity}", out url))
            {
                return url;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the GitHub source URL for a member, or null if not found.
    /// </summary>
    public string? GetMemberSourceUrl(string xmlDocKey)
    {
        // Strip the prefix (M:, P:, F:, E:)
        string rawKey = xmlDocKey.Length > 2 && xmlDocKey[1] == ':'
            ? xmlDocKey[2..]
            : xmlDocKey;

        string fullKey = rawKey.Replace('+', '.');

        // Try full key with parameters first (exact overload match for methods)
        if (_sourceUrls.TryGetValue(fullKey, out string? url))
        {
            return url;
        }

        // Try with generic arity stripped from the type portion
        string fullKeyNoArity = StripGenericArity(fullKey);
        if (fullKeyNoArity != fullKey && _sourceUrls.TryGetValue(fullKeyNoArity, out url))
        {
            return url;
        }

        // Try arity variants on the full key (with parameters preserved)
        url = TryArityVariantsFullKey(fullKeyNoArity);
        if (url is not null)
        {
            return url;
        }

        // For properties/indexers with parameters (e.g. "Type.Item(System.Int32)"),
        // try get_/set_ accessor with the parameters preserved
        url = TryPropertyAccessorWithParams(fullKeyNoArity);
        if (url is not null)
        {
            return url;
        }

        // Strip parameters for unqualified lookup
        string key = fullKey;
        int parenIdx = key.IndexOf('(');
        if (parenIdx >= 0)
        {
            key = key[..parenIdx];
        }

        // Fall back to unqualified (non-overloaded) lookup
        // Try exact key
        if (_sourceUrls.TryGetValue(key, out url))
        {
            return url;
        }

        // Strip generic arity while preserving member name
        string keyWithoutArity = StripGenericArity(key);
        if (keyWithoutArity != key && _sourceUrls.TryGetValue(keyWithoutArity, out url))
        {
            return url;
        }

        // Extract parent type and member name
        int lastDot = keyWithoutArity.LastIndexOf('.');
        if (lastDot < 0)
        {
            return null;
        }

        string parentKey = keyWithoutArity[..lastDot];
        string memberName = keyWithoutArity[(lastDot + 1)..];

        // Try arity variants on parent
        for (int arity = 1; arity <= 4; arity++)
        {
            if (_sourceUrls.TryGetValue($"{parentKey}`{arity}.{memberName}", out url))
            {
                return url;
            }
        }

        // Try property accessor names without params (get_/set_)
        foreach (string prefix in new[] { "get_", "set_" })
        {
            if (_sourceUrls.TryGetValue($"{parentKey}.{prefix}{memberName}", out url))
            {
                return url;
            }

            for (int arity = 1; arity <= 4; arity++)
            {
                if (_sourceUrls.TryGetValue($"{parentKey}`{arity}.{prefix}{memberName}", out url))
                {
                    return url;
                }
            }
        }

        // Fall back to declaring type URL
        int origLastDot = key.LastIndexOf('.');
        string parentFromOrig = origLastDot >= 0 ? key[..origLastDot] : parentKey;
        return GetTypeSourceUrl(parentFromOrig);
    }

    /// <summary>
    /// For indexer properties like "Type.Item(System.Int32)", tries looking up
    /// "Type.get_Item(System.Int32)" and "Type.set_Item(System.Int32)" in the PDB map,
    /// including generic arity variants on the parent type.
    /// </summary>
    private string? TryPropertyAccessorWithParams(string fullKeyNoArity)
    {
        int parenIdx = fullKeyNoArity.IndexOf('(');
        if (parenIdx < 0)
        {
            return null;
        }

        string withoutParams = fullKeyNoArity[..parenIdx];
        string paramSuffix = fullKeyNoArity[parenIdx..];

        int lastDot = withoutParams.LastIndexOf('.');
        if (lastDot < 0)
        {
            return null;
        }

        string parentType = withoutParams[..lastDot];
        string memberName = withoutParams[(lastDot + 1)..];

        foreach (string prefix in new[] { "get_", "set_" })
        {
            string accessorKey = $"{parentType}.{prefix}{memberName}{paramSuffix}";
            if (_sourceUrls.TryGetValue(accessorKey, out string? url))
            {
                return url;
            }

            // Try arity variants
            for (int arity = 1; arity <= 4; arity++)
            {
                accessorKey = $"{parentType}`{arity}.{prefix}{memberName}{paramSuffix}";
                if (_sourceUrls.TryGetValue(accessorKey, out url))
                {
                    return url;
                }
            }
        }

        return null;
    }

    // ─── SourceLink JSON parsing ──────────────────────────────────────

    /// <summary>
    /// Reads the SourceLink JSON blob from the PDB's module-level custom debug info.
    /// Maps local path prefixes to GitHub URL templates.
    /// </summary>
    private void ParseSourceLink()
    {
        // SourceLink is stored as a custom debug info on the module (row 1 of the Module table)
        EntityHandle moduleHandle = MetadataTokens.EntityHandle(0x00000001); // Module row 1
        foreach (CustomDebugInformationHandle cdiHandle in _pdbReader.GetCustomDebugInformation(moduleHandle))
        {
            CustomDebugInformation cdi = _pdbReader.GetCustomDebugInformation(cdiHandle);
            if (_pdbReader.GetGuid(cdi.Kind) != SourceLinkGuid)
            {
                continue;
            }

            byte[] blob = _pdbReader.GetBlobBytes(cdi.Value);
            string json = System.Text.Encoding.UTF8.GetString(blob);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("documents", out JsonElement documents))
                {
                    foreach (JsonProperty prop in documents.EnumerateObject())
                    {
                        // Key: "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\*"
                        // Value: "https://raw.githubusercontent.com/corvus-dotnet/Corvus.JsonSchema/COMMIT/*"
                        string localPattern = prop.Name.Replace('\\', '/');
                        string urlPattern = prop.Value.GetString() ?? "";

                        // Strip the trailing '*' wildcard
                        if (localPattern.EndsWith('*') && urlPattern.EndsWith('*'))
                        {
                            _sourceLinkMappings.Add((
                                localPattern[..^1],
                                urlPattern[..^1]));
                        }
                    }
                }
            }
            catch
            {
                // If SourceLink JSON is malformed, fall back to git-based URLs
            }

            Console.WriteLine($"  Parsed SourceLink JSON with {_sourceLinkMappings.Count} mappings.");
            break;
        }
    }

    // ─── Embedded source loading ──────────────────────────────────────

    private void LoadEmbeddedSource(DocumentHandle docHandle, string path)
    {
        foreach (CustomDebugInformationHandle cdiHandle in _pdbReader.GetCustomDebugInformation(docHandle))
        {
            CustomDebugInformation cdi = _pdbReader.GetCustomDebugInformation(cdiHandle);
            if (_pdbReader.GetGuid(cdi.Kind) != EmbeddedSourceGuid)
            {
                continue;
            }

            byte[] blob = _pdbReader.GetBlobBytes(cdi.Value);
            if (blob.Length < 4)
            {
                continue;
            }

            int uncompressedSize = BitConverter.ToInt32(blob, 0);
            string sourceText;

            if (uncompressedSize == 0)
            {
                sourceText = System.Text.Encoding.UTF8.GetString(blob, 4, blob.Length - 4);
            }
            else
            {
                using var compressed = new MemoryStream(blob, 4, blob.Length - 4);
                using var deflate = new System.IO.Compression.DeflateStream(
                    compressed, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(deflate, System.Text.Encoding.UTF8);
                sourceText = reader.ReadToEnd();
            }

            _embeddedSourceCache[path] = sourceText.Split('\n');
            break;
        }
    }

    // ─── TypeDefinitionDocuments ───────────────────────────────────────

    /// <summary>
    /// Reads the TypeDefinitionDocuments custom debug info for a type handle.
    /// Returns the preferred source document path, or null.
    /// </summary>
    private string? GetTypeDefinitionDocument(TypeDefinitionHandle typeHandle, string shortTypeName)
    {
        foreach (CustomDebugInformationHandle cdiHandle in _pdbReader.GetCustomDebugInformation(typeHandle))
        {
            CustomDebugInformation cdi = _pdbReader.GetCustomDebugInformation(cdiHandle);
            if (_pdbReader.GetGuid(cdi.Kind) != TypeDefinitionDocumentsGuid)
            {
                continue;
            }

            // Blob contains a sequence of compressed document handle row numbers
            BlobReader reader = _pdbReader.GetBlobReader(cdi.Value);
            List<string> docPaths = [];

            while (reader.RemainingBytes > 0)
            {
                int docRowNumber = reader.ReadCompressedInteger();
                DocumentHandle docHandle = MetadataTokens.DocumentHandle(docRowNumber);
                if (_documentPaths.TryGetValue(docHandle, out string? path))
                {
                    docPaths.Add(path);
                }
            }

            if (docPaths.Count > 0)
            {
                return SelectPrimaryFile(docPaths, shortTypeName);
            }
        }

        return null;
    }

    // ─── URL construction ─────────────────────────────────────────────

    /// <summary>
    /// Builds a GitHub browsable URL from a local file path and line number.
    /// Tries SourceLink mapping first (raw.githubusercontent → github.com/blob),
    /// falls back to git-based repo URL.
    /// </summary>
    private string? BuildSourceUrl(string localPath, int lineNumber)
    {
        string normalised = localPath.Replace('\\', '/');

        // Try SourceLink mappings: convert raw.githubusercontent.com URL to github.com/blob URL
        foreach (var (localPrefix, urlTemplate) in _sourceLinkMappings)
        {
            if (normalised.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = normalised[localPrefix.Length..];
                string rawUrl = $"{urlTemplate}{relativePath}";

                // Convert raw.githubusercontent.com/owner/repo/COMMIT/path
                // to github.com/owner/repo/blob/main/path
                string browsableUrl = ConvertToGitHubBlobUrl(rawUrl);
                return $"{browsableUrl}#L{lineNumber}";
            }
        }

        // Fallback: git-based URL
        if (normalised.StartsWith(_localPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = normalised[_localPathPrefix.Length..];
            return $"{_repoBaseUrl}{relativePath}#L{lineNumber}";
        }

        return null;
    }

    /// <summary>
    /// Converts a raw.githubusercontent.com URL to a github.com/blob/{branch} URL.
    /// Input:  https://raw.githubusercontent.com/owner/repo/COMMITSHA/path/to/file.cs
    /// Output: https://github.com/owner/repo/blob/{branch}/path/to/file.cs
    /// </summary>
    private string ConvertToGitHubBlobUrl(string rawUrl)
    {
        // raw.githubusercontent.com/owner/repo/commit/path
        const string rawPrefix = "https://raw.githubusercontent.com/";
        if (!rawUrl.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        string afterPrefix = rawUrl[rawPrefix.Length..];
        // Split: owner/repo/commit/rest...
        string[] parts = afterPrefix.Split('/', 4);
        if (parts.Length < 4)
        {
            return rawUrl;
        }

        string owner = parts[0];
        string repo = parts[1];
        // parts[2] = commit SHA — replace with the current branch
        string filePath = parts[3];

        return $"https://github.com/{owner}/{repo}/blob/{_branch}/{filePath}";
    }

    // ─── Source scanning ──────────────────────────────────────────────

    private static readonly string[] TypeKeywords = ["class", "struct", "interface", "enum", "record", "delegate"];

    /// <summary>
    /// Scans embedded source for a type declaration line (e.g. "struct BigNumber").
    /// Returns 1-based line number, or 0 if not found.
    /// </summary>
    private int FindTypeDeclarationLine(string filePath, string shortTypeName)
    {
        if (!_embeddedSourceCache.TryGetValue(filePath, out string[]? lines))
        {
            return 0;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            foreach (string keyword in TypeKeywords)
            {
                int kwIdx = line.IndexOf(keyword, StringComparison.Ordinal);
                if (kwIdx < 0)
                {
                    continue;
                }

                if (kwIdx > 0 && char.IsLetterOrDigit(line[kwIdx - 1]))
                {
                    continue;
                }

                int afterKeyword = kwIdx + keyword.Length;
                int nameIdx = line.IndexOf(shortTypeName, afterKeyword, StringComparison.Ordinal);
                if (nameIdx < 0)
                {
                    continue;
                }

                int afterName = nameIdx + shortTypeName.Length;
                if (afterName < line.Length &&
                    (char.IsLetterOrDigit(line[afterName]) || line[afterName] == '_'))
                {
                    continue;
                }

                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Scans embedded source for a member declaration line.
    /// Used for interface/abstract members without PDB sequence points.
    /// Returns 1-based line number, or 0 if not found.
    /// </summary>
    private int FindMemberDeclarationLine(string filePath, string memberName)
    {
        if (!_embeddedSourceCache.TryGetValue(filePath, out string[]? lines))
        {
            return 0;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimStart();

            if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*") || line.StartsWith("#"))
            {
                continue;
            }

            int nameIdx = line.IndexOf(memberName, StringComparison.Ordinal);
            if (nameIdx < 0)
            {
                continue;
            }

            if (nameIdx > 0 && (char.IsLetterOrDigit(line[nameIdx - 1]) || line[nameIdx - 1] == '_'))
            {
                continue;
            }

            int afterName = nameIdx + memberName.Length;
            if (afterName < line.Length &&
                (char.IsLetterOrDigit(line[afterName]) || line[afterName] == '_'))
            {
                continue;
            }

            if (afterName >= line.Length)
            {
                return i + 1;
            }

            char nextChar = line[afterName];
            if (nextChar == '(' || nextChar == '<' || nextChar == '{' || nextChar == ';' ||
                nextChar == ' ' || nextChar == '\t')
            {
                return i + 1;
            }
        }

        return 0;
    }

    // ─── Helper methods ───────────────────────────────────────────────

    /// <summary>
    /// Selects the primary file for a type from a list of candidate files.
    /// Prefers a file whose name matches the type name exactly (e.g. "BigNumber.cs" over "BigNumber.Parse.cs").
    /// </summary>
    private static string SelectPrimaryFile(List<string> files, string shortTypeName)
    {
        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(fileName, shortTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        // No exact match — return the first file
        return files[0];
    }

    private static string StripGenericArity(string key)
    {
        int backtickIdx = key.IndexOf('`');
        if (backtickIdx < 0)
        {
            return key;
        }

        int endIdx = backtickIdx + 1;
        while (endIdx < key.Length && char.IsDigit(key[endIdx]))
        {
            endIdx++;
        }

        return key[..backtickIdx] + key[endIdx..];
    }

    /// <summary>
    /// Tries generic arity variants on the type portion of a full key (with parameters).
    /// E.g. for "Ns.Type.Method(System.String)", tries "Ns.Type`1.Method(System.String)" etc.
    /// </summary>
    private string? TryArityVariantsFullKey(string fullKeyNoArity)
    {
        // Split into method+params portion and type portion
        // "Ns.Type.Method(params)" → parentType="Ns.Type", memberAndParams="Method(params)"
        int parenIdx = fullKeyNoArity.IndexOf('(');
        string withoutParams = parenIdx >= 0 ? fullKeyNoArity[..parenIdx] : fullKeyNoArity;
        string paramSuffix = parenIdx >= 0 ? fullKeyNoArity[parenIdx..] : "";

        int lastDot = withoutParams.LastIndexOf('.');
        if (lastDot < 0)
        {
            return null;
        }

        string parentType = withoutParams[..lastDot];
        string memberName = withoutParams[(lastDot + 1)..];

        for (int arity = 1; arity <= 4; arity++)
        {
            string candidate = $"{parentType}`{arity}.{memberName}{paramSuffix}";
            if (_sourceUrls.TryGetValue(candidate, out string? url))
            {
                return url;
            }
        }

        return null;
    }

    private string BuildFullTypeName(TypeDefinition typeDef, string ns, string name)
    {
        if (!typeDef.GetDeclaringType().IsNil)
        {
            TypeDefinition parent = _peMetadata.GetTypeDefinition(typeDef.GetDeclaringType());
            string parentNs = parent.Namespace.IsNil ? "" : _peMetadata.GetString(parent.Namespace);
            string parentName = _peMetadata.GetString(parent.Name);
            string parentFull = BuildFullTypeName(parent, parentNs, parentName);
            return $"{parentFull}.{name}";
        }

        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public void Dispose()
    {
        _pdbProvider.Dispose();
        _peReader.Dispose();
    }
}

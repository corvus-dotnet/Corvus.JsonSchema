using XmlDocToMarkdown;

// Collect multiple --xml/--assembly/--ns20-assembly sets (paired positionally)
List<string> xmlPaths = [];
List<string> assemblyPaths = [];
List<string?> ns20AssemblyPaths = [];

string? outputPath = null;
string? taxonomyOutputPath = null;
string? apiViewsDir = null;
string? sharedViewsDir = null;
string? repoUrl = null;
string? nsDescriptionsDir = null;
string? typeExamplesDir = null;
string? apiBaseUrl = null;
string? sidebarPartialName = null;
string? layoutPath = null;
string? versionLabel = null;
string? altVersionLabel = null;
string? altVersionUrl = null;

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--xml":
            xmlPaths.Add(args[++i]);
            // Ensure ns20 list stays aligned (placeholder for this pair)
            while (ns20AssemblyPaths.Count < xmlPaths.Count)
            {
                ns20AssemblyPaths.Add(null);
            }

            break;
        case "--assembly":
            assemblyPaths.Add(args[++i]);
            break;
        case "--output":
            outputPath = args[++i];
            break;
        case "--taxonomy-output":
            taxonomyOutputPath = args[++i];
            break;
        case "--api-views-dir":
            apiViewsDir = args[++i];
            break;
        case "--shared-views-dir":
            sharedViewsDir = args[++i];
            break;
        case "--repo-url":
            repoUrl = args[++i];
            break;
        case "--ns-descriptions":
            nsDescriptionsDir = args[++i];
            break;
        case "--type-examples":
            typeExamplesDir = args[++i];
            break;
        case "--ns20-assembly":
            // Apply to the most recent --xml pair
            if (ns20AssemblyPaths.Count > 0)
            {
                ns20AssemblyPaths[^1] = args[++i];
            }
            else
            {
                ns20AssemblyPaths.Add(args[++i]);
            }

            break;
        case "--api-base-url":
            apiBaseUrl = args[++i];
            break;
        case "--sidebar-partial-name":
            sidebarPartialName = args[++i];
            break;
        case "--layout-path":
            layoutPath = args[++i];
            break;
        case "--version-label":
            versionLabel = args[++i];
            break;
        case "--alt-version-label":
            altVersionLabel = args[++i];
            break;
        case "--alt-version-url":
            altVersionUrl = args[++i];
            break;
    }
}

string resolvedBaseUrl = (apiBaseUrl ?? "/api").TrimEnd('/');
string resolvedSidebarName = sidebarPartialName ?? "_ApiSidebar";
string resolvedLayoutPath = layoutPath ?? "../Shared/_Layout.cshtml";

if (xmlPaths.Count == 0 || assemblyPaths.Count == 0)
{
    Console.Error.WriteLine("Usage: XmlDocToMarkdown --xml <path> --assembly <path> [--xml <path2> --assembly <path2> ...] [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  --xml                  Path to an XML documentation file (repeatable for multi-assembly)");
    Console.Error.WriteLine("  --assembly             Path to the compiled DLL (repeatable, paired with --xml)");
    Console.Error.WriteLine("  --ns20-assembly        (Optional) netstandard2.0 build of the preceding assembly");
    Console.Error.WriteLine("  --output               Output directory for generated markdown files");
    Console.Error.WriteLine("  --taxonomy-output      Output directory for generated taxonomy YAML files");
    Console.Error.WriteLine("  --api-views-dir        (Optional) Directory for the generated API index view");
    Console.Error.WriteLine("  --shared-views-dir     (Optional) Directory for generated shared Razor partials");
    Console.Error.WriteLine("  --repo-url             (Optional) GitHub repository URL for source links (auto-detected from git if omitted)");
    Console.Error.WriteLine("  --ns-descriptions      (Optional) Directory containing {Namespace}.md files with namespace descriptions");
    Console.Error.WriteLine("  --type-examples        (Optional) Directory containing {slug}.md files with hand-authored examples for types and members");
    Console.Error.WriteLine("  --api-base-url         (Optional) Base URL path for API pages (default: /api)");
    Console.Error.WriteLine("  --sidebar-partial-name (Optional) Name for the sidebar Razor partial (default: _ApiSidebar)");
    Console.Error.WriteLine("  --layout-path          (Optional) Relative path to Layout.cshtml from the views dir (default: ../Shared/_Layout.cshtml)");
    return 1;
}

if (xmlPaths.Count != assemblyPaths.Count)
{
    Console.Error.WriteLine($"Mismatched --xml ({xmlPaths.Count}) and --assembly ({assemblyPaths.Count}) counts. Each --xml must be paired with an --assembly.");
    return 1;
}

// Pad ns20 list to match
while (ns20AssemblyPaths.Count < xmlPaths.Count)
{
    ns20AssemblyPaths.Add(null);
}

// Validate all files exist
for (int i = 0; i < xmlPaths.Count; i++)
{
    if (!File.Exists(xmlPaths[i]))
    {
        Console.Error.WriteLine($"XML documentation file not found: {xmlPaths[i]}");
        return 1;
    }

    if (!File.Exists(assemblyPaths[i]))
    {
        Console.Error.WriteLine($"Assembly file not found: {assemblyPaths[i]}");
        return 1;
    }
}

// Pre-scan all assemblies to build a combined type URL map
Dictionary<string, string> combinedTypeUrlMap = new(StringComparer.Ordinal);
for (int i = 0; i < assemblyPaths.Count; i++)
{
    Console.WriteLine($"Pre-scanning assembly [{i + 1}/{assemblyPaths.Count}]: {assemblyPaths[i]}");
    AssemblyInspector inspector = new(assemblyPaths[i]);
    Dictionary<string, string> partialMap = inspector.PreScanTypeUrls(resolvedBaseUrl);
    foreach (KeyValuePair<string, string> kvp in partialMap)
    {
        combinedTypeUrlMap[kvp.Key] = kvp.Value;
    }
}

XmlDocParser.TypeUrlMap = combinedTypeUrlMap;
Console.WriteLine($"  Built combined type URL map with {combinedTypeUrlMap.Count} entries.");

// Parse all XML documentation files into a combined members dictionary
Dictionary<string, DocMember> members = new(StringComparer.Ordinal);
for (int i = 0; i < xmlPaths.Count; i++)
{
    Console.WriteLine($"Parsing XML documentation [{i + 1}/{xmlPaths.Count}]: {xmlPaths[i]}");
    XmlDocParser parser = new(xmlPaths[i]);
    Dictionary<string, DocMember> partialMembers = parser.Parse();
    foreach (KeyValuePair<string, DocMember> kvp in partialMembers)
    {
        members[kvp.Key] = kvp.Value;
    }

    Console.WriteLine($"  Found {partialMembers.Count} documented members (total: {members.Count}).");
}

// Inspect all assemblies and merge namespace dictionaries
Dictionary<string, NamespaceInfo> namespaces = new(StringComparer.Ordinal);
for (int i = 0; i < assemblyPaths.Count; i++)
{
    Console.WriteLine($"Inspecting assembly [{i + 1}/{assemblyPaths.Count}]: {assemblyPaths[i]}");
    AssemblyInspector inspector = new(assemblyPaths[i]);
    Dictionary<string, NamespaceInfo> partialNamespaces = inspector.Inspect(members);

    // Merge: if a namespace already exists, append its types
    foreach (KeyValuePair<string, NamespaceInfo> kvp in partialNamespaces)
    {
        if (namespaces.TryGetValue(kvp.Key, out NamespaceInfo? existing))
        {
            existing.Types.AddRange(kvp.Value.Types);
            existing.Types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }
        else
        {
            namespaces[kvp.Key] = kvp.Value;
        }
    }
}

Console.WriteLine($"  Found {namespaces.Count} namespace(s) with public types across {assemblyPaths.Count} assemblies.");

// Build implementedBy reverse map: for each interface, collect which types implement it
Dictionary<string, TypeInfo> typesByFullName = new(StringComparer.Ordinal);
List<TypeInfo> allTypes = [];
foreach (NamespaceInfo nsInfo in namespaces.Values)
{
    foreach (TypeInfo typeInfo in nsInfo.Types)
    {
        typesByFullName[typeInfo.FullName] = typeInfo;
        allTypes.Add(typeInfo);
    }
}

foreach (TypeInfo typeInfo in allTypes)
{
    foreach ((string displayName, string? fullName) in typeInfo.InterfacesWithFullNames)
    {
        if (fullName is not null && typesByFullName.TryGetValue(fullName, out TypeInfo? ifaceInfo) && ifaceInfo.Kind == "interface")
        {
            ifaceInfo.ImplementedBy.Add((typeInfo.Name, typeInfo.FullName));
        }
    }
}

// Sort ImplementedBy lists alphabetically
foreach (TypeInfo typeInfo in allTypes)
{
    if (typeInfo.ImplementedBy.Count > 1)
    {
        typeInfo.ImplementedBy.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
    }
}

foreach (KeyValuePair<string, NamespaceInfo> ns in namespaces)
{
    Console.WriteLine($"    {ns.Key}: {ns.Value.Types.Count} type(s)");
}

// Scan netstandard2.0 assemblies to determine TFM-specific member availability
HashSet<string> combinedNs20Keys = new(StringComparer.Ordinal);
bool hasNs20 = false;
for (int i = 0; i < ns20AssemblyPaths.Count; i++)
{
    string? ns20Path = ns20AssemblyPaths[i];
    if (ns20Path is not null && File.Exists(ns20Path))
    {
        Console.WriteLine($"Scanning netstandard2.0 assembly [{i + 1}]: {ns20Path}");
        HashSet<string> partialKeys = AssemblyInspector.ScanMemberKeys(ns20Path);
        Console.WriteLine($"  Found {partialKeys.Count} members.");
        combinedNs20Keys.UnionWith(partialKeys);
        hasNs20 = true;
    }
}

if (hasNs20)
{
    Console.WriteLine($"  Combined netstandard2.0 member set: {combinedNs20Keys.Count} entries.");

    // Mark types and members not present in any ns2.0 assembly
    foreach (TypeInfo typeInfo in allTypes)
    {
        if (!combinedNs20Keys.Contains($"T:{typeInfo.FullName}"))
        {
            typeInfo.AvailableOnNetStandard20 = false;
        }

        foreach (MemberInfo m in typeInfo.Constructors.Concat(typeInfo.Properties)
            .Concat(typeInfo.Methods).Concat(typeInfo.Operators)
            .Concat(typeInfo.Fields).Concat(typeInfo.Events))
        {
            if (!combinedNs20Keys.Contains(m.XmlDocKey))
            {
                m.AvailableOnNetStandard20 = false;
            }
        }
    }
}

// Build source URL map from PDB if possible (uses first assembly's PDB)
SourceLinkResolver? sourceResolver = null;
string pdbPath = Path.ChangeExtension(Path.GetFullPath(assemblyPaths[0]), ".pdb");
if (File.Exists(pdbPath))
{
    Console.WriteLine($"Reading PDB for source links: {pdbPath}");

    // Auto-detect repo root from git
    string repoRoot = RunGit("rev-parse --show-toplevel")?.Trim() ?? "";
    string branch = "main";

    // Auto-detect repo URL from git remote if not provided
    if (repoUrl is null)
    {
        string? remoteUrl = RunGit("remote get-url origin")?.Trim();
        if (remoteUrl is not null)
        {
            // Normalise SSH → HTTPS and strip .git suffix
            if (remoteUrl.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                remoteUrl = "https://github.com/" + remoteUrl["git@github.com:".Length..];
            }

            if (remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                remoteUrl = remoteUrl[..^4];
            }

            repoUrl = remoteUrl;
        }
    }

    if (!string.IsNullOrEmpty(repoRoot) && !string.IsNullOrEmpty(repoUrl))
    {
        Console.WriteLine($"  Repo URL: {repoUrl}");
        Console.WriteLine($"  Branch: {branch}");
        sourceResolver = new SourceLinkResolver(pdbPath, Path.GetFullPath(assemblyPaths[0]), repoUrl, branch, repoRoot);
        sourceResolver.Build();
    }
    else
    {
        Console.WriteLine("  Warning: Could not detect git repo root or URL — source links disabled.");
    }
}
else
{
    Console.WriteLine("  No PDB found — source links disabled.");
}

if (outputPath is not null)
{
    Directory.CreateDirectory(outputPath);

    Console.WriteLine($"Generating markdown to: {outputPath}");
    MarkdownGenerator markdownGen = new(outputPath, resolvedBaseUrl, nsDescriptionsDir, sourceResolver, typeExamplesDir);

    Console.Write("  Namespace pages...");
    markdownGen.Generate(namespaces);
    Console.WriteLine($" {namespaces.Count} files.");

    int typeCount = namespaces.Values.Sum(ns => ns.Types.Count);
    Console.Write($"  Type pages ({typeCount} types)...");
    markdownGen.GeneratePerType(namespaces);
    Console.WriteLine(" done.");

    Console.Write("  Member pages...");
    int memberPageCount = markdownGen.GenerateMemberPages(namespaces);
    Console.WriteLine($" {memberPageCount} files.");
}

if (taxonomyOutputPath is not null)
{
    Directory.CreateDirectory(taxonomyOutputPath);

    // Derive template name from base URL (e.g., "/api" → "api/api-page")
    string templateName = resolvedBaseUrl.TrimStart('/') + "/api-page";

    Console.WriteLine($"Generating taxonomy to: {taxonomyOutputPath}");
    TaxonomyGenerator taxonomyGen = new(taxonomyOutputPath, outputPath!, resolvedBaseUrl, templateName);

    Console.Write("  Namespace taxonomy...");
    taxonomyGen.Generate(namespaces);
    Console.WriteLine($" {namespaces.Count} files.");

    int typeCount = namespaces.Values.Sum(ns => ns.Types.Count);
    Console.Write($"  Type taxonomy ({typeCount} types)...");
    taxonomyGen.GeneratePerType(namespaces);
    Console.WriteLine(" done.");

    Console.Write("  Member taxonomy...");
    int memberTaxCount = taxonomyGen.GeneratePerMember(namespaces);
    Console.WriteLine($" {memberTaxCount} files.");
}

if (apiViewsDir is not null)
{
    Console.WriteLine($"Generating API views to: {apiViewsDir}");
    Directory.CreateDirectory(apiViewsDir);
    ApiViewGenerator.GenerateIndexView(apiViewsDir, namespaces, resolvedBaseUrl, resolvedSidebarName, resolvedLayoutPath, nsDescriptionsDir, versionLabel, altVersionLabel, altVersionUrl);
}

if (sharedViewsDir is not null)
{
    Console.WriteLine($"Generating API sidebar partial to: {sharedViewsDir}");
    Directory.CreateDirectory(sharedViewsDir);
    ApiViewGenerator.GenerateApiSidebar(sharedViewsDir, namespaces, resolvedBaseUrl, resolvedSidebarName);
}

if (outputPath is not null)
{
    string searchIndexPath = Path.Combine(outputPath, "search-index.json");
    Console.WriteLine($"Generating search index: {searchIndexPath}");
    SearchIndexGenerator searchGen = new(searchIndexPath, resolvedBaseUrl);
    searchGen.Generate(namespaces);
}

Console.WriteLine("Done.");
sourceResolver?.Dispose();
return 0;

static string? RunGit(string arguments)
{
    try
    {
        using System.Diagnostics.Process proc = new()
        {
            StartInfo = new()
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return proc.ExitCode == 0 ? output : null;
    }
    catch
    {
        return null;
    }
}

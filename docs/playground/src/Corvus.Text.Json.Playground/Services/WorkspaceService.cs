using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Loads .NET assemblies from the WASM _framework/ directory as Roslyn MetadataReferences.
/// This enables in-browser compilation of user code against the Corvus.Text.Json runtime.
/// </summary>
public class WorkspaceService
{
    private readonly HttpClient httpClient;
    private readonly List<MetadataReference> references = [];
    private bool referencesLoaded;
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private Dictionary<string, string>? assetMappings;

    // Assemblies needed to compile generated types + user code
    private static readonly string[] RequiredAssemblies =
    [
        // Core runtime
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Console",
        "System.Collections",
        "System.Linq",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Runtime.InteropServices",
        "System.Diagnostics.Debug",
        "System.Buffers",
        "System.Memory",
        "System.ComponentModel",
        "System.Numerics.Vectors",
        "System.Runtime.Numerics",
        "netstandard",

        // JSON support
        "System.Text.Json",
        "System.Text.Encodings.Web",

        // Corvus.Text.Json runtime (what generated types reference)
        "Corvus.Text.Json",

        // NodaTime (used by date/time format types)
        "NodaTime",
    ];

    /// <summary>
    /// Global usings included as a separate document in every compilation.
    /// </summary>
    public const string GlobalUsings =
        """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using Corvus.Text.Json;
        """;

    public WorkspaceService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Gets the loaded metadata references.
    /// </summary>
    public IReadOnlyList<MetadataReference> References => this.references;

    /// <summary>
    /// Ensures assemblies are loaded and ready for compilation.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (this.referencesLoaded)
        {
            return;
        }

        await this.loadLock.WaitAsync();
        try
        {
            if (this.referencesLoaded)
            {
                return;
            }

            await this.LoadAssetMappingsAsync();

            foreach (string assemblyName in RequiredAssemblies)
            {
                try
                {
                    byte[]? bytes = await this.TryLoadAssemblyBytesAsync(assemblyName);
                    if (bytes is not null)
                    {
                        MetadataReference reference = MetadataReference.CreateFromImage(bytes);
                        this.references.Add(reference);
                    }
                }
                catch
                {
                    // Assembly load failures are non-fatal; compilation will
                    // report missing type errors if the assembly was needed.
                }
            }

            this.referencesLoaded = true;
        }
        finally
        {
            this.loadLock.Release();
        }
    }

    /// <summary>
    /// Creates a CSharpCompilation for the given source trees (generated code + user code).
    /// </summary>
    public CSharpCompilation CreateCompilation(IEnumerable<string> generatedSources, string userCode)
    {
        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
            .WithPreprocessorSymbols("NET", "NET10_0", "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER");

        var syntaxTrees = new List<SyntaxTree>();

        // Global usings
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions, path: "GlobalUsings.cs"));

        // Generated code files (hidden from user)
        int fileIndex = 0;
        foreach (string source in generatedSources)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                source,
                parseOptions,
                path: $"Generated_{fileIndex++}.cs"));
        }

        // User code
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(userCode, parseOptions, path: "Program.cs"));

        return CSharpCompilation.Create(
            $"PlaygroundAssembly_{Guid.NewGuid():N}",
            syntaxTrees,
            this.references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithConcurrentBuild(true));
    }

    private async Task LoadAssetMappingsAsync()
    {
        if (this.assetMappings is not null)
        {
            return;
        }

        this.assetMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            HttpResponseMessage response = await this.httpClient.GetAsync("_framework/asset-manifest.json");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Endpoints", out System.Text.Json.JsonElement endpoints))
            {
                return;
            }

            foreach (System.Text.Json.JsonElement endpoint in endpoints.EnumerateArray())
            {
                if (!endpoint.TryGetProperty("Route", out System.Text.Json.JsonElement routeEl) ||
                    !endpoint.TryGetProperty("AssetFile", out System.Text.Json.JsonElement assetEl))
                {
                    continue;
                }

                string? route = routeEl.GetString();
                string? assetFile = assetEl.GetString();

                if (string.IsNullOrEmpty(route) || string.IsNullOrEmpty(assetFile) ||
                    !route.EndsWith(".dll") || !assetFile.EndsWith(".dll"))
                {
                    continue;
                }

                // Skip compressed versions
                if (assetFile.Contains(".dll.br") || assetFile.Contains(".dll.gz"))
                {
                    continue;
                }

                // Map non-fingerprinted route to the actual fingerprinted file
                string routeFileName = route[(route.LastIndexOf('/') + 1)..];
                int dllIndex = routeFileName.LastIndexOf(".dll", StringComparison.Ordinal);
                string baseFileName = routeFileName[..dllIndex];

                int lastDot = baseFileName.LastIndexOf('.');
                if (lastDot > 0)
                {
                    string lastSegment = baseFileName[(lastDot + 1)..];
                    bool isFingerprint = lastSegment.Length >= 8 && lastSegment.Length <= 12 &&
                                         lastSegment.All(c => char.IsLetterOrDigit(c) && char.IsLower(c));
                    if (isFingerprint)
                    {
                        continue;
                    }
                }

                this.assetMappings[route] = assetFile;
            }
        }
        catch
        {
            // Asset mapping failures are non-fatal; fall back to direct paths.
        }
    }

    private async Task<byte[]?> TryLoadAssemblyBytesAsync(string assemblyName)
    {
        string virtualPath = $"_framework/{assemblyName}.dll";

        string actualPath = this.assetMappings is not null &&
                            this.assetMappings.TryGetValue(virtualPath, out string? mappedPath)
            ? mappedPath
            : virtualPath;

        string[] patterns = [actualPath, $"_framework/{assemblyName}.wasm"];

        foreach (string pattern in patterns)
        {
            try
            {
                HttpResponseMessage response = await this.httpClient.GetAsync(pattern);
                if (response.IsSuccessStatusCode)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                    // Verify it's a valid PE file (starts with MZ)
                    if (bytes.Length > 2 && bytes[0] == 0x4D && bytes[1] == 0x5A)
                    {
                        return bytes;
                    }
                }
            }
            catch
            {
                // Try next pattern
            }
        }

        return null;
    }
}

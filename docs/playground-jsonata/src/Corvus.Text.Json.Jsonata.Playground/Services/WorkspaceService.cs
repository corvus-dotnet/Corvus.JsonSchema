using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.Jsonata.Playground.Services;

/// <summary>
/// Loads .NET assemblies from the WASM _framework/ directory as Roslyn MetadataReferences.
/// This enables in-browser compilation of user-defined bindings code.
/// </summary>
public class WorkspaceService
{
    private readonly HttpClient httpClient;
    private readonly List<MetadataReference> references = [];
    private bool referencesLoaded;
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private Dictionary<string, string>? assetMappings;

    private static readonly string[] RequiredAssemblies =
    [
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Buffers",
        "System.Memory",
        "System.Numerics.Vectors",
        "System.Runtime.Numerics",
        "netstandard",
        "System.Text.Json",
        "System.Text.Encodings.Web",
        "Corvus.Text.Json",
        "Corvus.Text.Json.Jsonata",
    ];

    /// <summary>
    /// Preamble included in every bindings compilation.
    /// </summary>
    public const string Preamble =
        """
        using System;
        using System.Collections.Generic;
        using Corvus.Text.Json;
        using Corvus.Text.Json.Jsonata;
        """;

    public WorkspaceService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public IReadOnlyList<MetadataReference> References => this.references;

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
                    // Assembly load failures are non-fatal.
                }
            }

            this.referencesLoaded = true;
        }
        finally
        {
            this.loadLock.Release();
        }
    }

    public CSharpCompilation CreateCompilation(string sourceCode)
    {
        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
            .WithPreprocessorSymbols("NET", "NET10_0", "NET10_0_OR_GREATER");

        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(Preamble, parseOptions, path: "Preamble.cs"),
            CSharpSyntaxTree.ParseText(sourceCode, parseOptions, path: "Bindings.cs"),
        };

        return CSharpCompilation.Create(
            $"BindingsAssembly_{Guid.NewGuid():N}",
            syntaxTrees,
            this.references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));
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

            // Build mapping: clean Route (e.g. _framework/System.Runtime.dll)
            // → fingerprinted AssetFile (e.g. _framework/System.Runtime.abc123.dll)
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

                // Skip compressed entries
                if (endpoint.TryGetProperty("Selectors", out System.Text.Json.JsonElement selectors) &&
                    selectors.GetArrayLength() > 0)
                {
                    continue;
                }

                // We want clean-name routes that map to fingerprinted asset files.
                // Clean routes look like _framework/System.Runtime.dll (no fingerprint in the name).
                // Fingerprinted routes look like _framework/System.Runtime.abc123.dll.
                // When route != assetFile, route is the clean one and assetFile is fingerprinted.
                if (!route.Equals(assetFile, StringComparison.Ordinal))
                {
                    this.assetMappings[route] = assetFile;
                }
            }
        }
        catch
        {
            // Non-fatal.
        }
    }

    private async Task<byte[]?> TryLoadAssemblyBytesAsync(string assemblyName)
    {
        // The asset manifest maps clean routes → fingerprinted asset files.
        // e.g. "_framework/System.Runtime.dll" → "_framework/System.Runtime.abc123.dll"
        string cleanRoute = $"_framework/{assemblyName}.dll";

        string url = this.assetMappings is not null &&
                     this.assetMappings.TryGetValue(cleanRoute, out string? mappedPath)
            ? mappedPath
            : cleanRoute;

        try
        {
            HttpResponseMessage response = await this.httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                // Verify valid PE (starts with MZ)
                if (bytes.Length > 2 && bytes[0] == 0x4D && bytes[1] == 0x5A)
                {
                    return bytes;
                }
            }
        }
        catch
        {
            // Non-fatal.
        }

        return null;
    }
}

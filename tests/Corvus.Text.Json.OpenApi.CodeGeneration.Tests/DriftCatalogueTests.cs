// <copyright file="DriftCatalogueTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;
using Corvus.Text.Json.OpenApi20;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// The harness that produced the drift catalogue for consolidating the four C# client emitters, kept as the proof
/// that every version's prepared model emits through the one <see cref="CSharpClientEmitter"/>. Each document is
/// prepared by its own version's generator and emitted over a synthetic type map that exercises both the builder and
/// the no-builder paths. The raw outputs are written under the directory in CORVUS_DRIFT_CATALOGUE_OUT; without it
/// the harness only checks that the emitter accepts every model.
/// </summary>
[TestClass]
public class DriftCatalogueTests
{
    private static readonly string[] Versions = ["2.0", "3.0", "3.1", "3.2"];

    [TestMethod]
    [TestCategory("outerloop")]
    public void EveryDocumentEmitsThroughTheSharedEmitter()
    {
        string? outDir = Environment.GetEnvironmentVariable("CORVUS_DRIFT_CATALOGUE_OUT");
        CSharpClientEmitter emitter = new();
        SyntheticTypeMap types = new();
        ClientEmitOptions options = new("Drift.Client", null);

        foreach ((string name, string version, OperationScope scope) in Documents())
        {
            JsonElement root = Load(name);
            OpenApiClientModel model = Prepare(root, version, scope);
            IReadOnlyList<GeneratedFile> files = scope == OperationScope.Paths
                ? emitter.EmitClient(in model, types, options)
                : emitter.EmitCallbackClient(in model, types, options);

            Assert.IsTrue(files.Count > 0, $"{name} ({scope}) emitted no files");

            if (outDir is not null)
            {
                string dir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(name) + (scope == OperationScope.Paths ? string.Empty : "-callbacks"));
                Directory.CreateDirectory(dir);
                foreach (GeneratedFile file in files)
                {
                    File.WriteAllText(Path.Combine(dir, file.FileName.Replace('<', '_').Replace('>', '_')), file.Content);
                }
            }
        }
    }

    private static IEnumerable<(string Name, string Version, OperationScope Scope)> Documents()
    {
        foreach (string version in Versions)
        {
            yield return ($"covspec-{version}.json", version, OperationScope.Paths);
            yield return ($"petstore-{version}.json", version, OperationScope.Paths);
            if (version != "2.0")
            {
                yield return ($"callbacks-{version}.json", version, OperationScope.WebhooksAndCallbacks);
            }
        }
    }

    private static OpenApiClientModel Prepare(JsonElement root, string version, OperationScope scope) => version switch
    {
        "2.0" => new OpenApi20CodeGenerator("Drift.Client", new Dictionary<string, string>()).PrepareOperations(root, scope),
        "3.0" => new OpenApi30CodeGenerator("Drift.Client", new Dictionary<string, string>()).PrepareOperations(root, scope),
        "3.1" => new OpenApi31CodeGenerator("Drift.Client", new Dictionary<string, string>()).PrepareOperations(root, scope),
        _ => new OpenApi32CodeGenerator("Drift.Client", new Dictionary<string, string>()).PrepareOperations(root, scope),
    };

    private static JsonElement Load(string name)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    // Every schema pointer maps to a deterministic synthetic type name; half of them, by hash, have a builder shape
    // and half do not, so both emission paths run over every document.
    private sealed class SyntheticTypeMap : IModelTypeMap
    {
        public string FallbackTypeReference => "JsonElement";

        public bool TryGetTypeReference(string schemaPointer, out string typeReference)
        {
            typeReference = "Drift.Client.Models.T" + Hash(schemaPointer)[..8];
            return true;
        }

        public bool HasBuilder(string schemaPointer) => (Hash(schemaPointer)[^1] & 1) == 0;

        private static string Hash(string schemaPointer)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(schemaPointer)));
    }
}
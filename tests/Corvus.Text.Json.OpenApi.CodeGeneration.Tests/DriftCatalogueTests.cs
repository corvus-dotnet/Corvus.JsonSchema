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
/// The drift catalogue harness for consolidating the four C# client emitters. Each document is prepared once, by its
/// own version's generator, and the prepared model is emitted by all four emitters over the same synthetic type map.
/// Every difference between an emitter's output and the document's own emitter's output is either feature-driven
/// (the record carries a field only one version fills) or drift (the same record content emitted differently).
/// The raw outputs and a summary are written under the directory in CORVUS_DRIFT_CATALOGUE_OUT; without it the
/// harness only checks that every emitter accepts every model.
/// </summary>
[TestClass]
public class DriftCatalogueTests
{
    private static readonly string[] Versions = ["2.0", "3.0", "3.1", "3.2"];

    [TestMethod]
    [TestCategory("outerloop")]
    public void CrossFeedEveryDocumentThroughEveryEmitter()
    {
        string? outDir = Environment.GetEnvironmentVariable("CORVUS_DRIFT_CATALOGUE_OUT");
        StringBuilder summary = new();
        summary.Append("| document | scope | prepared by | emitter | files only here | files only in own | differing files (lines) |\n|---|---|---|---|---|---|---|\n");

        foreach ((string name, string version, OperationScope scope) in Documents())
        {
            JsonElement root = Load(name);
            IOpenApiClientEmitter own = Generator(version);
            OpenApiClientModel model = Prepare(own, root, version, scope);
            SyntheticTypeMap types = new();
            ClientEmitOptions options = new("Drift.Client", null);

            Dictionary<string, Dictionary<string, string>> outputs = new(StringComparer.Ordinal);
            foreach (string emitterVersion in Versions)
            {
                IOpenApiClientEmitter emitter = Generator(emitterVersion);
                try
                {
                    IReadOnlyList<GeneratedFile> files = scope == OperationScope.Paths
                        ? emitter.EmitClient(in model, types, options)
                        : emitter.EmitCallbackClient(in model, types, options);
                    outputs[emitterVersion] = files.ToDictionary(f => f.FileName, f => f.Content, StringComparer.Ordinal);
                }
                catch (NotSupportedException ex)
                {
                    outputs[emitterVersion] = new Dictionary<string, string>(StringComparer.Ordinal) { ["<unsupported>"] = ex.Message };
                }

                if (outDir is not null)
                {
                    string dir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(name) + (scope == OperationScope.Paths ? string.Empty : "-callbacks"), emitterVersion);
                    Directory.CreateDirectory(dir);
                    foreach ((string file, string content) in outputs[emitterVersion])
                    {
                        File.WriteAllText(Path.Combine(dir, file.Replace('<', '_').Replace('>', '_')), content);
                    }
                }
            }

            Dictionary<string, string> reference = outputs[version];
            foreach (string emitterVersion in Versions)
            {
                Dictionary<string, string> other = outputs[emitterVersion];
                int onlyHere = other.Keys.Count(k => !reference.ContainsKey(k));
                int onlyOwn = reference.Keys.Count(k => !other.ContainsKey(k));
                List<string> differing = [];
                foreach ((string file, string content) in other)
                {
                    if (reference.TryGetValue(file, out string? referenceContent) && !string.Equals(referenceContent, content, StringComparison.Ordinal))
                    {
                        differing.Add($"{file} ({DifferingLines(referenceContent, content)})");
                    }
                }

                summary.Append($"| {name} | {scope} | {version} | {emitterVersion} | {onlyHere} | {onlyOwn} | {string.Join(", ", differing)} |\n");
            }
        }

        if (outDir is not null)
        {
            File.WriteAllText(Path.Combine(outDir, "summary.md"), summary.ToString());
        }

        Console.WriteLine(summary.ToString());
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

    private static IOpenApiClientEmitter Generator(string version) => version switch
    {
        "2.0" => new OpenApi20CodeGenerator("Drift.Client", new Dictionary<string, string>()),
        "3.0" => new OpenApi30CodeGenerator("Drift.Client", new Dictionary<string, string>()),
        "3.1" => new OpenApi31CodeGenerator("Drift.Client", new Dictionary<string, string>()),
        _ => new OpenApi32CodeGenerator("Drift.Client", new Dictionary<string, string>()),
    };

    private static OpenApiClientModel Prepare(IOpenApiClientEmitter generator, JsonElement root, string version, OperationScope scope) => generator switch
    {
        OpenApi20CodeGenerator g => g.PrepareOperations(root, scope),
        OpenApi30CodeGenerator g => g.PrepareOperations(root, scope),
        OpenApi31CodeGenerator g => g.PrepareOperations(root, scope),
        OpenApi32CodeGenerator g => g.PrepareOperations(root, scope),
        _ => throw new InvalidOperationException(version),
    };

    private static JsonElement Load(string name)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    // Counts lines present in one text and not the other, as a cheap size of a difference.
    private static int DifferingLines(string a, string b)
    {
        HashSet<string> linesA = new(a.Split('\n'), StringComparer.Ordinal);
        HashSet<string> linesB = new(b.Split('\n'), StringComparer.Ordinal);
        return linesA.Count(l => !linesB.Contains(l)) + linesB.Count(l => !linesA.Contains(l));
    }

    // Every schema pointer maps to a deterministic synthetic type name, and every pointer has a builder shape, so
    // the emitters produce their fullest surface over identical inputs.
    private sealed class SyntheticTypeMap : IModelTypeMap
    {
        public string FallbackTypeReference => "JsonElement";

        public bool TryGetTypeReference(string schemaPointer, out string typeReference)
        {
            typeReference = "Drift.Client.Models.T" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(schemaPointer)))[..8];
            return true;
        }

        public bool HasBuilder(string schemaPointer) => true;
    }
}
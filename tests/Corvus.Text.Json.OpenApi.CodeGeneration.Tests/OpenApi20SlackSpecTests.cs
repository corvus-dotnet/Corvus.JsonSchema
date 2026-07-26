// <copyright file="OpenApi20SlackSpecTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi20;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// Real-world acceptance test for OpenAPI 2.0 (Swagger) support: the archived Slack
/// Web API specification (the corpus that motivated #899), pinned via the
/// slack-api-specs git submodule. Marked outerloop: hermetic but large (~1.2MB spec,
/// 174 operations, 817 schema positions).
/// </summary>
[TestClass]
public class OpenApi20SlackSpecTests
{
    private static JsonElement slackRoot;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "slack_web_openapi_v2.json");
        if (!File.Exists(path))
        {
            Assert.Inconclusive("The slack-api-specs submodule is not initialized; run git submodule update --init.");
        }

        string json = File.ReadAllText(path);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        slackRoot = doc.RootElement.Clone();
    }

    [TestMethod]
    [TestCategory("outerloop")]
    public void ListOperations_FindsTheFullSlackSurface()
    {
        OperationSummary[] operations = OpenApi20CodeGenerator.ListOperations(slackRoot);

        Assert.AreEqual(174, operations.Length);
        Assert.AreEqual(91, operations.Count(o => o.HasRequestBody));
    }

    [TestMethod]
    [TestCategory("outerloop")]
    public void CollectSchemaPointers_CoversEverySchemaPositionIncludingFormBodies()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(
            slackRoot, out var parameterNames, out IReadOnlyDictionary<string, string> syntheticDocuments);

        Assert.AreEqual(817, refs.Length);
        Assert.AreEqual(378, parameterNames.Count);

        // The synthesized form-body document aggregates the formData operations.
        string formBodies = syntheticDocuments[OpenApi20CodeGenerator.FormBodyDocumentUri];
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(formBodies);
        int formBodyCount = 0;
        foreach (var prop in doc.RootElement.GetProperty("schemas"u8).EnumerateObject())
        {
            formBodyCount++;
        }

        Assert.AreEqual(91, formBodyCount);
    }

    [TestMethod]
    [TestCategory("outerloop")]
    public void Generate_ProducesTheFullClientAndServerSurface()
    {
        OpenApi20CodeGenerator generator = new("Slack.Client", new Dictionary<string, string>(StringComparer.Ordinal));

        IReadOnlyList<GeneratedFile> clientFiles = generator.Generate(slackRoot);
        Assert.IsTrue(clientFiles.Count > 400, $"expected the full client surface, got {clientFiles.Count} files");

        // chat.postMessage is the canonical Slack operation: urlencoded formData.
        GeneratedFile chatClient = clientFiles.First(f => f.FileName == "ApiChatClient.cs");
        StringAssert.Contains(chatClient.Content, "application/x-www-form-urlencoded");

        // files.upload declares its file field as type: string over urlencoded consumes
        // (per the archived Slack spec), so it serializes as an urlencoded form body.
        GeneratedFile filesClient = clientFiles.First(f => f.FileName == "ApiFilesClient.cs");
        StringAssert.Contains(filesClient.Content, "application/x-www-form-urlencoded");

        IReadOnlyList<GeneratedFile> serverFiles = generator.GenerateServer(slackRoot);
        Assert.IsTrue(serverFiles.Count > 300, $"expected the full server surface, got {serverFiles.Count} files");
    }
}
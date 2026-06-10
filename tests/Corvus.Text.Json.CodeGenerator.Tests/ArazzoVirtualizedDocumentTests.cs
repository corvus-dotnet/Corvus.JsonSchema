// <copyright file="ArazzoVirtualizedDocumentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Covers Arazzo generation from <em>virtualized</em> documents — an Arazzo description and its
/// OpenAPI/AsyncAPI sources supplied in memory (registered by URI) rather than read from the file
/// system, with relative references resolved as URI references (Arazzo §5.6).
/// </summary>
[TestClass]
public class ArazzoVirtualizedDocumentTests : IDisposable
{
    private readonly string outputDir;

    public ArazzoVirtualizedDocumentTests()
    {
        this.outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(this.outputDir);
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task Generates_from_an_in_memory_arazzo_document_and_a_registered_https_source()
    {
        // The Arazzo document references its OpenAPI source by an absolute https URI — and neither the
        // Arazzo document nor the source exists on disk. Both are registered in memory, so generation
        // succeeds purely from the registry: a file-system or network fallback would fail (the https URI
        // is never fetched, the paths are never written), proving the documents are virtualized.
        Uri sourceUri = new("https://specs.example.test/pets.openapi.json");

        string arazzoJson = $$"""
            {
              "arazzo": "1.0.1",
              "info": { "title": "Adopt", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "pets", "url": "{{sourceUri}}", "type": "openapi" }
              ],
              "workflows": [
                {
                  "workflowId": "adopt",
                  "inputs": {
                    "type": "object",
                    "properties": { "petId": { "type": "string" } },
                    "required": [ "petId" ]
                  },
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                      "successCriteria": [ { "condition": "$statusCode == 200" } ],
                      "outputs": { "petName": "$response.body#/name" }
                    }
                  ],
                  "outputs": { "name": "$steps.getPet.outputs.petName" }
                }
              ]
            }
            """;

        // A path that is used only as the Arazzo document's identity (retrieval URI); it is never read
        // from disk because the bytes are registered under its file URI.
        string virtualArazzoPath = Path.Combine(Path.GetTempPath(), "corvus-virtual", Guid.NewGuid().ToString("N"), "adopt.arazzo.json");
        Uri arazzoUri = new(Path.GetFullPath(virtualArazzoPath));

        byte[] petsBytes = await File.ReadAllBytesAsync(CodeGeneratorRunner.GetFixturePath("Arazzo", "pets.openapi.json"));

        var registered = new List<RegisteredDocument>
        {
            new(arazzoUri, Encoding.UTF8.GetBytes(arazzoJson)),
            new(sourceUri, petsBytes),
        };

        IReadOnlyList<string> written = await ArazzoGenerationDriver.GenerateAsync(
            virtualArazzoPath, "TestWorkflows", this.outputDir, clientName: null, durable: false, CancellationToken.None, registered);

        // The executor for the 'adopt' workflow was generated from the in-memory Arazzo document.
        string executorPath = Path.Combine(this.outputDir, "Workflows", "AdoptWorkflow.cs");
        Assert.IsTrue(File.Exists(executorPath), $"Expected {executorPath}. Wrote: {string.Join(", ", written)}");

        // The OpenAPI client + models for the registered https source were generated under Pets/.
        Assert.IsTrue(
            Directory.Exists(Path.Combine(this.outputDir, "Pets"))
            && Directory.GetFiles(Path.Combine(this.outputDir, "Pets"), "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated OpenAPI client files under {Path.Combine(this.outputDir, "Pets")}.");

        // The executor bound the getPet operation from the registered source's generated namespace.
        string executor = await File.ReadAllTextAsync(executorPath);
        StringAssert.Contains(executor, "TestWorkflows.Pets");
    }
}
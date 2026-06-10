// <copyright file="ArazzoCrossDocumentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Covers cross-document sub-workflow references: a step that targets a workflow defined in a separate
/// Arazzo description via <c>$sourceDescriptions.&lt;name&gt;.&lt;workflowId&gt;</c> (Arazzo §5.5.2). The
/// referenced (<c>type: arazzo</c>) source is generated recursively into a per-source namespace, and the
/// parent step invokes that namespace's executor.
/// </summary>
[TestClass]
public class ArazzoCrossDocumentTests : IDisposable
{
    private readonly string outputDir;

    public ArazzoCrossDocumentTests()
    {
        this.outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(this.outputDir);
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task Generates_a_cross_document_sub_workflow_into_a_per_source_namespace()
    {
        // pets (OpenAPI) ← child.arazzo (workflow "lookup") ← parent.arazzo (step targets
        // $sourceDescriptions.child.lookup). All three documents are supplied in memory.
        Uri childUri = new("https://specs.example.test/child.arazzo.json");
        Uri petsUri = new("https://specs.example.test/pets.openapi.json");

        string parentJson = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "Parent", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "child", "url": "https://specs.example.test/child.arazzo.json", "type": "arazzo" }
              ],
              "workflows": [
                {
                  "workflowId": "parent",
                  "inputs": { "type": "object", "properties": { "petId": { "type": "string" } }, "required": [ "petId" ] },
                  "steps": [
                    {
                      "stepId": "runChild",
                      "workflowId": "$sourceDescriptions.child.lookup",
                      "parameters": [ { "name": "petId", "value": "$inputs.petId" } ]
                    }
                  ],
                  "outputs": { "name": "$steps.runChild.outputs.name" }
                }
              ]
            }
            """;

        string childJson = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "Child", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "pets", "url": "./pets.openapi.json", "type": "openapi" }
              ],
              "workflows": [
                {
                  "workflowId": "lookup",
                  "inputs": { "type": "object", "properties": { "petId": { "type": "string" } }, "required": [ "petId" ] },
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

        string parentPath = Path.Combine(Path.GetTempPath(), "corvus-xdoc", Guid.NewGuid().ToString("N"), "parent.arazzo.json");
        Uri parentUri = new(Path.GetFullPath(parentPath));
        byte[] petsBytes = await File.ReadAllBytesAsync(CodeGeneratorRunner.GetFixturePath("Arazzo", "pets.openapi.json"));

        var registered = new List<RegisteredDocument>
        {
            new(parentUri, Encoding.UTF8.GetBytes(parentJson)),
            new(childUri, Encoding.UTF8.GetBytes(childJson)),
            new(petsUri, petsBytes),
        };

        await ArazzoGenerationDriver.GenerateAsync(
            parentPath, "Acme", this.outputDir, clientName: null, durable: false, CancellationToken.None, registered);

        // The child Arazzo source was generated recursively into Acme.Child (executor + its OpenAPI client).
        string childExecutor = Path.Combine(this.outputDir, "Child", "Workflows", "LookupWorkflow.cs");
        Assert.IsTrue(File.Exists(childExecutor), $"Expected the child workflow executor at {childExecutor}.");
        Assert.IsTrue(
            Directory.Exists(Path.Combine(this.outputDir, "Child", "Pets")),
            "Expected the child's OpenAPI client under Child/Pets/.");

        // The parent executor invokes the child workflow in its per-source namespace.
        string parentExecutor = await File.ReadAllTextAsync(Path.Combine(this.outputDir, "Workflows", "ParentWorkflow.cs"));
        StringAssert.Contains(parentExecutor, "Acme.Child.Workflows.LookupWorkflow");
    }

    [TestMethod]
    public async Task Generates_a_shared_arazzo_source_only_once_when_referenced_by_two_names()
    {
        // The same child document is referenced twice (sources "x" and "y" share one url). It must be
        // generated exactly once and both references must resolve to that single generated copy — a
        // diamond is not a cycle, and nothing should be generated twice.
        Uri childUri = new("https://specs.example.test/shared.arazzo.json");
        Uri petsUri = new("https://specs.example.test/pets.openapi.json");

        string parentJson = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "Parent", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "x", "url": "https://specs.example.test/shared.arazzo.json", "type": "arazzo" },
                { "name": "y", "url": "https://specs.example.test/shared.arazzo.json", "type": "arazzo" }
              ],
              "workflows": [
                {
                  "workflowId": "parent",
                  "inputs": { "type": "object", "properties": { "petId": { "type": "string" } }, "required": [ "petId" ] },
                  "steps": [
                    { "stepId": "viaX", "workflowId": "$sourceDescriptions.x.lookup", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] },
                    { "stepId": "viaY", "workflowId": "$sourceDescriptions.y.lookup", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
                  ],
                  "outputs": {}
                }
              ]
            }
            """;

        string childJson = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "Shared", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "pets", "url": "./pets.openapi.json", "type": "openapi" }
              ],
              "workflows": [
                {
                  "workflowId": "lookup",
                  "inputs": { "type": "object", "properties": { "petId": { "type": "string" } }, "required": [ "petId" ] },
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                      "successCriteria": [ { "condition": "$statusCode == 200" } ]
                    }
                  ],
                  "outputs": {}
                }
              ]
            }
            """;

        string parentPath = Path.Combine(Path.GetTempPath(), "corvus-xdoc", Guid.NewGuid().ToString("N"), "parent.arazzo.json");
        Uri parentUri = new(Path.GetFullPath(parentPath));
        byte[] petsBytes = await File.ReadAllBytesAsync(CodeGeneratorRunner.GetFixturePath("Arazzo", "pets.openapi.json"));

        var registered = new List<RegisteredDocument>
        {
            new(parentUri, Encoding.UTF8.GetBytes(parentJson)),
            new(childUri, Encoding.UTF8.GetBytes(childJson)),
            new(petsUri, petsBytes),
        };

        await ArazzoGenerationDriver.GenerateAsync(
            parentPath, "Acme", this.outputDir, clientName: null, durable: false, CancellationToken.None, registered);

        // The shared child's executor is generated exactly once across the whole output tree.
        string[] lookupExecutors = Directory.GetFiles(this.outputDir, "LookupWorkflow.cs", SearchOption.AllDirectories);
        Assert.AreEqual(1, lookupExecutors.Length, $"Expected the shared workflow generated once; got: {string.Join(", ", lookupExecutors)}");

        // Both references resolve to that single generated namespace (the first reference's, "x"); the
        // second name ("y") did not produce its own copy.
        string parentExecutor = await File.ReadAllTextAsync(Path.Combine(this.outputDir, "Workflows", "ParentWorkflow.cs"));
        StringAssert.Contains(parentExecutor, "Acme.X.Workflows.LookupWorkflow");
        Assert.IsFalse(Directory.Exists(Path.Combine(this.outputDir, "Y")), "The second reference must reuse the first copy, not generate a 'Y' tree.");
    }
}
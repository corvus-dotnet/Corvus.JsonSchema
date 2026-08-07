// <copyright file="ArazzoDocumentGateTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// What the generator refuses to compile. Two checks that fail differently: conformance to the Arazzo schema catches a
/// malformed document, and the identifier rule catches a well-formed document carrying a hostile value.
/// </summary>
/// <remarks>
/// <para>
/// The distinction is the reason both exist. <c>workflowId: "adopt\"); Evil(); //"</c> is <em>valid Arazzo</em> — the
/// schema constrains it no further than <c>type: string</c>, and that is a string — so schema conformance passes it and
/// only the identifier rule refuses it. Conversely a document missing a required field is well-identified and still
/// unfit to compile.
/// </para>
/// <para>
/// Both run in the generator rather than at an ingress, so they cover every path that compiles a document: the control
/// plane's catalog upload, the CLI, the AOT build, and each recursively generated cross-document source.
/// </para>
/// </remarks>
[TestClass]
public class ArazzoDocumentGateTests : IDisposable
{
    private readonly string outputDir;

    public ArazzoDocumentGateTests()
    {
        this.outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(this.outputDir);
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task A_workflow_id_that_only_a_sink_would_find_meaningful_is_refused()
    {
        // Valid Arazzo, hostile value. The escaping in the emitter makes this safe to compile; the gate makes it not
        // arrive, which is what covers the sinks escaping does not reach.
        await AssertRefusedAsync("""adopt"); Evil(); //""");
    }

    [TestMethod]
    public async Task A_workflow_id_containing_a_newline_is_refused()
        => await AssertRefusedAsync("adopt\nclass Injected {}");

    [TestMethod]
    public async Task A_workflow_id_containing_a_path_separator_is_refused()
        => await AssertRefusedAsync("../../etc/passwd");

    [TestMethod]
    public async Task An_ordinary_workflow_id_is_accepted()
    {
        // Every identifier in this repository's documents and samples matches the accepted shape, so the rule must cost
        // nothing that is written today. A gate that refused these would be measured in broken builds, not in security.
        foreach (string id in new[] { "adopt", "onboard-customer", "onboard-customer-async", "access-approval", "serverless-check", "nightly-reconcile" })
        {
            Exception failure = await TryGenerateAsync(id);
            Assert.IsFalse(
                failure is InvalidDataException,
                $"the gate refused '{id}', which is an identifier this repository already uses: {failure?.Message}");
        }
    }

    private static async Task AssertRefusedAsync(string workflowId)
    {
        Exception failure = await TryGenerateAsync(workflowId);

        Assert.IsInstanceOfType<InvalidDataException>(
            failure,
            $"the generator accepted a workflow id it should refuse. Got: {failure?.GetType().Name ?? "no exception"}");
    }

    // Generation is expected to fail for other reasons too (there is no real source document here), so what is measured
    // is WHICH failure arrives: the gate's InvalidDataException, or something further down the pipeline.
    private static async Task<Exception> TryGenerateAsync(string workflowId)
    {
        string escaped = workflowId
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n");

        string document = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Gate", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "pets", "url": "https://specs.example.test/pets.json", "type": "openapi" }
              ],
              "workflows": [
                {
                  "workflowId": "{{escaped}}",
                  "steps": [ { "stepId": "getPet", "operationId": "getPet" } ]
                }
              ]
            }
            """;

        var uri = new Uri("https://specs.example.test/gate.arazzo.json");
        try
        {
            await ArazzoGenerationDriver.GenerateAsync(
                uri,
                "Gate",
                CodeGeneratorRunner.CreateTempOutputDirectory(),
                clientName: null,
                durable: false,
                CancellationToken.None,
                [new RegisteredDocument(uri, Encoding.UTF8.GetBytes(document))]);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
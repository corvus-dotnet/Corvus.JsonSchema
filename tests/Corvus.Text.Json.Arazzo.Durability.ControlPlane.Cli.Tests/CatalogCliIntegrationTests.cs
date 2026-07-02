// <copyright file="CatalogCliIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// Drives the <c>catalog</c> CLI subcommands: the offline <c>pack</c>/<c>verify</c> against temp files, and the
/// server-backed add/search/get/versions/package/workflow/source/update/obsolete/delete/purge against an
/// in-process control plane (reusing the shared harness in <see cref="CliIntegrationTests"/>).
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Catalog_pack_then_verify_locally()
    {
        using var workspace = new TempWorkspace();
        string workflow = workspace.WriteWorkflow("cli-demo");
        string envelope = Path.Combine(workspace.Dir, "package.json");

        (int packExit, _, _) = await RunLocalAsync("catalog", "pack", "--workflow", workflow, "--out", envelope);
        packExit.ShouldBe(0);
        File.Exists(envelope).ShouldBeTrue();

        (int verifyExit, string verifyOut, _) = await RunLocalAsync("catalog", "verify", envelope);
        verifyExit.ShouldBe(0);
        verifyOut.ShouldContain("cli-demo");
    }

    [TestMethod]
    public async Task Catalog_pack_then_unpack_roundtrips_documents()
    {
        using var workspace = new TempWorkspace();
        string envelope = Path.Combine(workspace.Dir, "package.json");
        await RunLocalAsync("catalog", "pack", "--workflow", workspace.WriteWorkflow("cli-demo"), "--out", envelope);

        string outDir = Path.Combine(workspace.Dir, "unpacked");
        (int exit, string stdout, _) = await RunLocalAsync("catalog", "unpack", envelope, "--out-dir", outDir);

        exit.ShouldBe(0);
        stdout.ShouldContain("1 source");
        File.Exists(Path.Combine(outDir, "workflow.json")).ShouldBeTrue();
        File.Exists(Path.Combine(outDir, "sources", "petstore.json")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(outDir, "workflow.json")).ShouldContain("cli-demo");
        File.ReadAllText(Path.Combine(outDir, "sources", "petstore.json")).ShouldContain("Petstore");
    }

    [TestMethod]
    public async Task Catalog_verify_rejects_versioned_id_and_hash_mismatch()
    {
        using var workspace = new TempWorkspace();
        string envelope = Path.Combine(workspace.Dir, "package.json");
        await RunLocalAsync("catalog", "pack", "--workflow", workspace.WriteWorkflow("already-v3"), "--out", envelope);

        // The packed id is "already-v3" — a -vN suffix the catalog rejects.
        (int versionedExit, _, _) = await RunLocalAsync("catalog", "verify", envelope);
        versionedExit.ShouldNotBe(0);

        (int hashExit, _, _) = await RunLocalAsync("catalog", "verify", envelope, "--expect-hash", "deadbeef");
        hashExit.ShouldNotBe(0);
    }

    [TestMethod]
    public async Task Catalog_add_from_workflow_then_get_and_search()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();
        // Source-less: nothing to credential, so the workflow is deployable everywhere and the readiness gate passes.
        string workflow = workspace.WriteWorkflow("cli-demo", withSource: false);

        (int addExit, string addOut, _) = await RunAsync(
            host, "catalog", "add", "--workflow", workflow, "--owner-name", "Team A", "--owner-email", "team-a@example.com", "--tag", "demo");
        addExit.ShouldBe(0);
        addOut.ShouldContain("cli-demo-v1");

        (int getExit, string getOut, _) = await RunAsync(host, "catalog", "get", "cli-demo", "1");
        getExit.ShouldBe(0);
        getOut.ShouldContain("cli-demo-v1");

        (int searchExit, string searchOut, _) = await RunAsync(host, "catalog", "search", "--output", "json");
        searchExit.ShouldBe(0);
        searchOut.ShouldContain("cli-demo");

        (int versionsExit, _, _) = await RunAsync(host, "catalog", "versions", "cli-demo");
        versionsExit.ShouldBe(0);
    }

    [TestMethod]
    public async Task Catalog_add_pre_built_package_then_download_documents()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();
        string envelope = Path.Combine(workspace.Dir, "package.json");
        await RunLocalAsync("catalog", "pack", "--workflow", workspace.WriteWorkflow("cli-demo"), "--out", envelope);

        // The package carries the petstore source, so the workflow must be ready somewhere before it can be added:
        // register an environment and a petstore credential there.
        (await RunAsync(host, "environments", "create", "production")).Exit.ShouldBe(0);
        (await RunAsync(host, "credentials", "create", "petstore", "production",
            "--auth-kind", "apiKey", "--ref", "value=keyvault://petstore-key#3", "--config", "parameterName=X-Api-Key")).Exit.ShouldBe(0);

        (int addExit, _, _) = await RunAsync(
            host, "catalog", "add", "--package", envelope, "--owner-name", "Team A", "--owner-email", "team-a@example.com");
        addExit.ShouldBe(0);

        // The package is a streamed binary archive; download to a file, then unpack it to confirm contents.
        string downloaded = Path.Combine(workspace.Dir, "downloaded-package.zip");
        (int packageExit, _, _) = await RunAsync(host, "catalog", "package", "cli-demo", "1", "--out", downloaded);
        packageExit.ShouldBe(0);
        (byte[] downloadedWorkflow, _) = CatalogPackage.Unpack(File.ReadAllBytes(downloaded));
        System.Text.Encoding.UTF8.GetString(downloadedWorkflow).ShouldContain("cli-demo-v1");

        (int workflowExit, string workflowOut, _) = await RunAsync(host, "catalog", "workflow", "cli-demo", "1");
        workflowExit.ShouldBe(0);
        workflowOut.ShouldContain("cli-demo-v1");

        (int sourceExit, string sourceOut, _) = await RunAsync(host, "catalog", "source", "cli-demo", "1", "petstore");
        sourceExit.ShouldBe(0);
        sourceOut.ShouldContain("Petstore");
    }

    [TestMethod]
    public async Task Catalog_add_refuses_a_workflow_not_ready_in_any_environment()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();
        string workflow = workspace.WriteWorkflow("needs-creds"); // references the petstore source

        // No environment has a credential for petstore → the workflow can't run anywhere → the add is refused.
        (int refusedExit, _, string refusedErr) = await RunAsync(
            host, "catalog", "add", "--workflow", workflow, "--owner-name", "A", "--owner-email", "a@example.com");
        refusedExit.ShouldNotBe(0);
        refusedErr.ShouldContain("not ready in any environment");

        // Make it deployable — register an environment and the petstore credential — then the add succeeds.
        (await RunAsync(host, "environments", "create", "production")).Exit.ShouldBe(0);
        (await RunAsync(host, "credentials", "create", "petstore", "production",
            "--auth-kind", "apiKey", "--ref", "value=keyvault://petstore-key#3", "--config", "parameterName=X-Api-Key")).Exit.ShouldBe(0);

        (int okExit, string okOut, _) = await RunAsync(
            host, "catalog", "add", "--workflow", workflow, "--owner-name", "A", "--owner-email", "a@example.com");
        okExit.ShouldBe(0);
        okOut.ShouldContain("needs-creds-v1");
    }

    [TestMethod]
    public async Task Catalog_update_obsolete_then_delete()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();
        await RunAsync(host, "catalog", "add", "--workflow", workspace.WriteWorkflow("cli-demo", withSource: false), "--owner-name", "A", "--owner-email", "a@example.com");

        (int updateExit, string updateOut, _) = await RunAsync(host, "catalog", "update", "cli-demo", "1", "--tag", "prod", "--tag", "billing");
        updateExit.ShouldBe(0);
        updateOut.ShouldContain("prod");

        (int obsoleteExit, string obsoleteOut, _) = await RunAsync(host, "catalog", "obsolete", "cli-demo", "1");
        obsoleteExit.ShouldBe(0);
        obsoleteOut.ShouldContain("Obsolete");

        // No runs reference cli-demo-v1, so deletion is allowed.
        (int deleteExit, _, _) = await RunAsync(host, "catalog", "delete", "cli-demo", "1");
        deleteExit.ShouldBe(0);

        (int getExit, _, _) = await RunAsync(host, "catalog", "get", "cli-demo", "1");
        getExit.ShouldNotBe(0);
    }

    [TestMethod]
    public async Task Catalog_add_and_update_security_tags()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();

        // Add with a user security tag (§14.2). withSource:false avoids the deployability gate.
        (int addExit, string addOut, _) = await RunAsync(
            host, "catalog", "add", "--workflow", workspace.WriteWorkflow("cli-sec", withSource: false),
            "--owner-name", "A", "--owner-email", "a@example.com", "--security-tag", "team=payments");
        addExit.ShouldBe(0);
        addOut.ShouldContain("payments"); // the user security tag round-trips into the response

        // --security-tag re-tags the non-internal labels; the old one is replaced.
        (int updateExit, string updateOut, _) = await RunAsync(
            host, "catalog", "update", "cli-sec", "1", "--security-tag", "team=billing");
        updateExit.ShouldBe(0);
        updateOut.ShouldContain("billing");
        updateOut.ShouldNotContain("payments");
    }

    [TestMethod]
    public async Task Catalog_purge_reaps_obsolete()
    {
        await using Host host = await StartAsync();
        using var workspace = new TempWorkspace();
        await RunAsync(host, "catalog", "add", "--workflow", workspace.WriteWorkflow("cli-demo", withSource: false), "--owner-name", "A", "--owner-email", "a@example.com");
        await RunAsync(host, "catalog", "obsolete", "cli-demo", "1");

        (int purgeExit, string purgeOut, _) = await RunAsync(host, "catalog", "purge");
        purgeExit.ShouldBe(0);
        purgeOut.ShouldContain("1");
    }

    // Runs a CLI invocation with no server (for pack/verify), capturing stdout/stderr.
    private static async Task<(int Exit, string Stdout, string Stderr)> RunLocalAsync(params string[] args)
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            int exit = await CliApp.Create().RunAsync(args);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            this.Dir = Path.Combine(Path.GetTempPath(), "arazzo-catalog-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Dir);
            File.WriteAllText(
                Path.Combine(this.Dir, "petstore.json"),
                """{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        }

        public string Dir { get; }

        public string WriteWorkflow(string workflowId, bool withSource = true)
        {
            string path = Path.Combine(this.Dir, "workflow.arazzo.json");
            string sourceDescriptions = withSource
                ? "\"sourceDescriptions\": [ { \"name\": \"petstore\", \"url\": \"petstore.json\", \"type\": \"openapi\" } ],"
                : "\"sourceDescriptions\": [],";
            File.WriteAllText(
                path,
                $$"""
                {
                  "arazzo": "1.1.0",
                  "info": { "title": "CLI Demo", "description": "A demo workflow." },
                  {{sourceDescriptions}}
                  "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
                }
                """);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Dir, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
// <copyright file="CliCredentialsIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>credentials</c> command surface, driven in-process against a real control-plane server
/// over loopback HTTP. The CLI manages references and non-secret metadata only; these tests exercise the status-aware
/// listing and the fetch-merge update (re-pointing a reference is how a credential is rotated).
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Credential_has_a_create_get_update_delete_lifecycle_over_http()
    {
        await using Host host = await StartAsync();

        (int createExit, string createOut, _) = await RunAsync(
            host,
            "credentials", "create", "petstore", "production",
            "--auth-kind", "apiKey",
            "--ref", "value=keyvault://petstore-key#3",
            "--config", "parameterName=X-Api-Key");
        createExit.ShouldBe(0);
        createOut.ShouldContain("petstore");
        createOut.ShouldContain("keyvault://petstore-key#3");

        (int getExit, string getOut, _) = await RunAsync(host, "credentials", "get", "petstore", "production");
        getExit.ShouldBe(0);
        getOut.ShouldContain("keyvault://petstore-key#3");

        (int deleteExit, string deleteOut, _) = await RunAsync(host, "credentials", "delete", "petstore", "production");
        deleteExit.ShouldBe(0);
        deleteOut.ShouldContain("Deleted");

        (int missingExit, _, _) = await RunAsync(host, "credentials", "get", "petstore", "production");
        missingExit.ShouldBe(1);
    }

    [TestMethod]
    public async Task Update_re_points_a_reference_stamps_rotation_and_preserves_other_fields()
    {
        await using Host host = await StartAsync();

        await RunAsync(
            host,
            "credentials", "create", "petstore", "production",
            "--auth-kind", "apiKey",
            "--ref", "value=keyvault://petstore-key#3",
            "--config", "parameterName=X-Api-Key");

        // Re-point only the reference. The fetch-merge keeps authKind + config, swaps the ref, and stamps rotatedAt.
        (int updateExit, string updateOut, _) = await RunAsync(
            host,
            "credentials", "update", "petstore", "production",
            "--ref", "value=keyvault://petstore-key#4");
        updateExit.ShouldBe(0);
        updateOut.ShouldContain("keyvault://petstore-key#4");
        updateOut.ShouldContain("apiKey");          // authKind preserved
        updateOut.ShouldContain("X-Api-Key");        // config preserved
        updateOut.ShouldContain("rotatedAt");        // a reference change stamps the rotation timestamp
        updateOut.ShouldNotContain("petstore-key#3");
    }

    [TestMethod]
    public async Task Credential_management_tags_are_settable_on_create_and_re_taggable_on_update()
    {
        await using Host host = await StartAsync();

        (int createExit, string createOut, _) = await RunAsync(
            host,
            "credentials", "create", "petstore", "production",
            "--auth-kind", "apiKey",
            "--ref", "value=keyvault://petstore-key#3",
            "--config", "parameterName=X-Api-Key",
            "--manage", "team=payments");
        createExit.ShouldBe(0);
        createOut.ShouldContain("payments");

        // --manage re-tags who may administer the binding; the old label is replaced (authKind/config preserved).
        (int updateExit, string updateOut, _) = await RunAsync(
            host,
            "credentials", "update", "petstore", "production",
            "--manage", "team=billing");
        updateExit.ShouldBe(0);
        updateOut.ShouldContain("billing");
        updateOut.ShouldNotContain("payments");
        updateOut.ShouldContain("apiKey");

        // Omitting --manage carries the tags forward (a ref-only rotation keeps billing).
        (int keepExit, string keepOut, _) = await RunAsync(
            host,
            "credentials", "update", "petstore", "production",
            "--ref", "value=keyvault://petstore-key#4");
        keepExit.ShouldBe(0);
        keepOut.ShouldContain("billing");
    }

    [TestMethod]
    public async Task List_renders_status_and_filters_by_status()
    {
        await using Host host = await StartAsync();

        await CreateWithExpiryAsync(host, "fresh", DateTimeOffset.UtcNow.AddDays(30));
        await CreateWithExpiryAsync(host, "stale", DateTimeOffset.UtcNow.AddDays(-1));

        // The default table lists both with their derived status words.
        (int listExit, string listOut, _) = await RunAsync(host, "credentials", "list");
        listExit.ShouldBe(0);
        listOut.ShouldContain("fresh");
        listOut.ShouldContain("stale");
        listOut.ShouldContain("expired");

        // --status filters client-side to the expired binding only.
        (int filterExit, string filterOut, _) = await RunAsync(host, "credentials", "list", "--status", "expired");
        filterExit.ShouldBe(0);
        filterOut.ShouldContain("stale");
        filterOut.ShouldNotContain("fresh");

        // --output json bypasses the table and emits the raw list.
        (int jsonExit, string jsonOut, _) = await RunAsync(host, "credentials", "list", "--output", "json");
        jsonExit.ShouldBe(0);
        jsonOut.ShouldContain("\"credentialStatus\"");
    }

    [TestMethod]
    public async Task An_inline_secret_is_rejected_at_the_edge()
    {
        await using Host host = await StartAsync();

        // A bare value with no scheme is not a SecretRef — the server refuses it (400) so secret material cannot be
        // smuggled into a binding inline. The CLI surfaces the failure as a non-zero exit.
        (int exit, _, string stderr) = await RunAsync(
            host,
            "credentials", "create", "x", "y",
            "--auth-kind", "apiKey",
            "--ref", "value=hunter2-the-actual-secret");
        exit.ShouldBe(1);
        stderr.ShouldNotBeNullOrEmpty();
    }

    private static async Task CreateWithExpiryAsync(Host host, string source, DateTimeOffset expiresAt)
    {
        (int exit, _, _) = await RunAsync(
            host,
            "credentials", "create", source, "production",
            "--auth-kind", "apiKey",
            "--ref", "value=env://" + source.ToUpperInvariant(),
            "--expires-at", expiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        exit.ShouldBe(0);
    }
}
// <copyright file="CliDebugRunsIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// CLI-layer tests for the §18 debug-run commands (<c>arazzo-runs debug-runs …</c>, workflow-designer
/// design §18). These verify command registration and settings validation — the CLI's own logic — with
/// no server. The debug-run execution path (start / step / inject-message / resume against a real runner)
/// is covered by the control-plane server and handler tests and is live-verified in the sample; the CLI
/// commands are thin, uniform wrappers over the generated client, so their own testable surface is the
/// argument contract.
/// </summary>
[TestClass]
public sealed class CliDebugRunsIntegrationTests
{
    [TestMethod]
    public async Task Debug_run_start_requires_a_workflow_id()
        => (await RunCliAsync("debug-runs", "start", "wc-1")).Exit.ShouldNotBe(0);

    [TestMethod]
    public async Task Debug_run_start_requires_a_working_copy_id()
        => (await RunCliAsync("debug-runs", "start", "--workflow-id", "onboard-customer")).Exit.ShouldNotBe(0);

    [TestMethod]
    public async Task Debug_run_inject_message_requires_a_channel()
        => (await RunCliAsync("debug-runs", "inject-message", "wc-1", "run-1", "--payload-file", "payload.json")).Exit.ShouldNotBe(0);

    [TestMethod]
    public async Task Debug_run_inject_message_requires_a_payload_file()
        => (await RunCliAsync("debug-runs", "inject-message", "wc-1", "run-1", "--channel", "kyc.verdict")).Exit.ShouldNotBe(0);

    [TestMethod]
    public async Task Debug_run_get_requires_a_debug_run_id()
        => (await RunCliAsync("debug-runs", "get", "wc-1")).Exit.ShouldNotBe(0);

    [TestMethod]
    public async Task Debug_run_resume_requires_a_debug_run_id()
        => (await RunCliAsync("debug-runs", "resume", "wc-1")).Exit.ShouldNotBe(0);

    private static async Task<(int Exit, string Stdout, string Stderr)> RunCliAsync(params string[] args)
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
}
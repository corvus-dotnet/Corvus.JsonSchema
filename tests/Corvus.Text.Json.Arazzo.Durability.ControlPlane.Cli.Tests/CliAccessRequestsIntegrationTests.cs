// <copyright file="CliAccessRequestsIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>access-requests</c> command surface (§16.5), driven in-process against the same
/// authenticated control-plane host as the administrators tests. The CLI's <c>--token</c> stands in for the
/// requesting/deciding identity, which is also the grant's subject (the host wires the access-request subject to the
/// tenant identity).
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Access_request_is_submitted_queued_and_approved_over_http()
    {
        await using Host host = await StartAdministeredAsync();

        // bob requests run access to 'flow'; it is pending (he is not eligible to self-elevate).
        (int submitExit, string submitOut, _) = await RunAsync(host, "access-requests", "submit", "flow", "--scope", "runs:write", "--token", "bob");
        submitExit.ShouldBe(0);
        submitOut.ShouldContain("Pending");
        submitOut.ShouldContain("bob");
        string id = RequestId(submitOut);

        // acme (the workflow's administrator) sees bob's request in the queue.
        (int queueExit, string queueOut, _) = await RunAsync(host, "access-requests", "list", "--workflow", "flow", "--token", "acme", "--output", "json");
        queueExit.ShouldBe(0);
        queueOut.ShouldContain(id);

        // acme approves; the grant is time-boxed.
        (int approveExit, string approveOut, _) = await RunAsync(host, "access-requests", "approve", id, "--token", "acme", "--reason", "looks good");
        approveExit.ShouldBe(0);
        approveOut.ShouldContain("Approved");

        // bob lists his own requests (no --workflow) and sees it approved.
        (int mineExit, string mineOut, _) = await RunAsync(host, "access-requests", "list", "--token", "bob", "--output", "json");
        mineExit.ShouldBe(0);
        mineOut.ShouldContain(id);
        mineOut.ShouldContain("Approved");
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_approve_over_http()
    {
        await using Host host = await StartAdministeredAsync();

        (int submitExit, string submitOut, _) = await RunAsync(host, "access-requests", "submit", "flow", "--scope", "runs:write", "--token", "bob");
        submitExit.ShouldBe(0);
        string id = RequestId(submitOut);

        // mallory is not an administrator of 'flow' → the approval is refused (403 → non-zero exit).
        (int exit, _, string stderr) = await RunAsync(host, "access-requests", "approve", id, "--token", "mallory");
        exit.ShouldBe(1);
        stderr.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task Eligibility_grant_enables_self_service_over_http()
    {
        await using Host host = await StartAdministeredAsync();

        // bob requests; acme grants durable eligibility rather than a live grant.
        (int submitExit, string submitOut, _) = await RunAsync(host, "access-requests", "submit", "flow", "--scope", "runs:write", "--token", "bob");
        submitExit.ShouldBe(0);
        string id = RequestId(submitOut);

        (int eligExit, string eligOut, _) = await RunAsync(host, "access-requests", "approve-as-eligible", id, "--token", "acme");
        eligExit.ShouldBe(0);
        eligOut.ShouldContain("Eligible");

        // bob now self-serves: a fresh request is auto-approved against the stored eligibility — no approver involved.
        (int selfExit, string selfOut, _) = await RunAsync(host, "access-requests", "submit", "flow", "--scope", "runs:write", "--token", "bob");
        selfExit.ShouldBe(0);
        selfOut.ShouldContain("Approved");
    }

    private static string RequestId(string viewJson)
    {
        Match match = Regex.Match(viewJson, "\"id\"\\s*:\\s*\"([^\"]+)\"");
        match.Success.ShouldBeTrue($"could not find an id in: {viewJson}");
        return match.Groups[1].Value;
    }
}
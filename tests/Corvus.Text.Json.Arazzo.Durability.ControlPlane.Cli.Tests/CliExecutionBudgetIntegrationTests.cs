// <copyright file="CliExecutionBudgetIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// The execution budget (ADR 0068) from the CLI, over real HTTP to a real control plane: authoring an environment's
/// override, reading it beside the deployment's ceiling and the budget in effect, and reading what a run is held to
/// and why it faulted.
/// </summary>
public sealed partial class CliIntegrationTests
{
    private const string Budgeted1 = "b0d9e700000000000000000000000001";

    [TestMethod]
    public async Task An_environments_budget_is_authored_on_create_and_read_beside_the_ceiling_and_the_budget_in_effect()
    {
        await using Host host = await StartEnvironmentsAsync(ExecutionBudget.CeilingFrom(maxSteps: 200, wallClockSeconds: 7200));

        (await RunAsync(host, "environments", "create", "production", "--max-steps", "25", "--step-timeout-seconds", "7", "--token", "acme")).Exit.ShouldBe(0);

        (int exit, string stdout, _) = await RunAsync(host, "environments", "budget", "production", "--output", "json", "--token", "acme");
        exit.ShouldBe(0);
        using (Stj.JsonDocument budget = Stj.JsonDocument.Parse(stdout))
        {
            // What was authored, exactly: two limits and no more.
            Stj.JsonElement authored = budget.RootElement.GetProperty("override");
            authored.GetProperty("maxSteps").GetInt32().ShouldBe(25);
            authored.GetProperty("stepTimeoutSeconds").GetInt32().ShouldBe(7);
            authored.TryGetProperty("wallClockSeconds", out _).ShouldBeFalse();

            // The ceiling is the deployment's, and the budget in effect is the two resolved.
            budget.RootElement.GetProperty("ceiling").GetProperty("maxSteps").GetInt32().ShouldBe(200);
            budget.RootElement.GetProperty("effective").GetProperty("maxSteps").GetInt32().ShouldBe(25);
            budget.RootElement.GetProperty("effective").GetProperty("wallClockSeconds").GetInt64().ShouldBe(7200);
            budget.RootElement.GetProperty("effective").GetProperty("stepTimeoutSeconds").GetInt32().ShouldBe(7);
        }

        // The table says the same to a person: a limit the environment leaves out has no Override cell.
        (int tableExit, string table, _) = await RunAsync(host, "environments", "budget", "production", "--token", "acme");
        tableExit.ShouldBe(0);
        table.ShouldContain("Override");
        table.ShouldContain("Effective");
        table.ShouldContain("Ceiling");
        table.ShouldContain("7200s (2h)");
    }

    [TestMethod]
    public async Task Naming_one_limit_on_update_keeps_the_others_and_clearing_removes_the_override()
    {
        await using Host host = await StartEnvironmentsAsync(ExecutionBudget.CeilingFrom(maxSteps: 200));
        (await RunAsync(host, "environments", "create", "production", "--max-steps", "25", "--step-timeout-seconds", "7", "--token", "acme")).Exit.ShouldBe(0);

        // The server replaces an override whole, so the CLI lays the limit named over the ones already authored.
        (await RunAsync(host, "environments", "update", "production", "--max-steps", "40", "--token", "acme")).Exit.ShouldBe(0);
        using (Stj.JsonDocument budget = await BudgetAsync(host))
        {
            Stj.JsonElement authored = budget.RootElement.GetProperty("override");
            authored.GetProperty("maxSteps").GetInt32().ShouldBe(40);
            authored.GetProperty("stepTimeoutSeconds").GetInt32().ShouldBe(7);
        }

        // An update that names no limit leaves the override alone.
        (await RunAsync(host, "environments", "update", "production", "--description", "Live", "--token", "acme")).Exit.ShouldBe(0);
        using (Stj.JsonDocument budget = await BudgetAsync(host))
        {
            budget.RootElement.GetProperty("override").GetProperty("maxSteps").GetInt32().ShouldBe(40);
        }

        // Cleared, the environment's runs take the ceiling.
        (await RunAsync(host, "environments", "update", "production", "--clear-budget", "--token", "acme")).Exit.ShouldBe(0);
        using (Stj.JsonDocument budget = await BudgetAsync(host))
        {
            budget.RootElement.GetProperty("override").TryGetProperty("maxSteps", out _).ShouldBeFalse();
            budget.RootElement.GetProperty("effective").GetProperty("maxSteps").GetInt32().ShouldBe(200);
        }
    }

    [TestMethod]
    public async Task A_limit_wider_than_the_deployments_ceiling_is_refused()
    {
        await using Host host = await StartEnvironmentsAsync(ExecutionBudget.CeilingFrom(maxSteps: 200));

        (int exit, _, string stderr) = await RunAsync(host, "environments", "create", "production", "--max-steps", "201", "--token", "acme");

        exit.ShouldNotBe(0);
        stderr.ShouldContain("maxSteps");
    }

    [TestMethod]
    public async Task A_run_faulted_on_its_budget_says_what_it_was_held_to_and_what_to_do()
    {
        await using Host host = await StartAsync();
        var budget = new ExecutionBudget(3, TimeSpan.FromHours(2), 1, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(7), 2048);
        using (WorkflowRun run = WorkflowRun.CreateNew(host.Store, Budgeted1, "wf", default, "development", host.Clock, budget: budget))
        {
            await run.FaultAsync("step1", attempt: 1, ExecutionBudgetFault.Fuel, default);
        }

        // The run as the API returns it carries the budget frozen into it.
        (int jsonExit, string json, _) = await RunAsync(host, "get", Budgeted1);
        jsonExit.ShouldBe(0);
        using (Stj.JsonDocument detail = Stj.JsonDocument.Parse(json))
        {
            detail.RootElement.GetProperty("budget").GetProperty("maxSteps").GetInt32().ShouldBe(3);
            detail.RootElement.GetProperty("budget").GetProperty("wallClockSeconds").GetInt64().ShouldBe(7200);
        }

        // Laid out to read, the fault type is explained and the remedy is the right one: a budget fault is terminal.
        (int exit, string stdout, _) = await RunAsync(host, "get", Budgeted1, "--output", "detail");
        exit.ShouldBe(0);
        stdout.ShouldContain("budget-fuel");
        stdout.ShouldContain("max steps");
        stdout.ShouldContain("Start a new run");
        stdout.ShouldContain("7200s (2h)");

        // And the list says why a run is Faulted without opening it.
        (int listExit, string list, _) = await RunAsync(host, "list", "--status", "Faulted");
        listExit.ShouldBe(0);
        list.ShouldContain("Error");
        list.ShouldContain("budget-fuel");
    }

    [TestMethod]
    public async Task A_steps_own_failure_is_shown_as_recorded_and_not_explained()
    {
        await using Host host = await StartAsync();
        await FaultRunAsync(host.Store, R1, host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "get", R1, "--output", "detail");

        exit.ShouldBe(0);
        stdout.ShouldContain("boom");
        stdout.ShouldNotContain("Start a new run");
        stdout.ShouldNotContain("resume the run");
    }

    private static async Task<Stj.JsonDocument> BudgetAsync(Host host)
    {
        (int exit, string stdout, _) = await RunAsync(host, "environments", "budget", "production", "--output", "json", "--token", "acme");
        exit.ShouldBe(0);
        return Stj.JsonDocument.Parse(stdout);
    }
}
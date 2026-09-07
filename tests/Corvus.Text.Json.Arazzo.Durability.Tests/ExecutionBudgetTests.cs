// <copyright file="ExecutionBudgetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

[TestClass]
public sealed class ExecutionBudgetTests
{
    [TestMethod]
    public void The_default_is_the_journal_cap_a_day_the_depth_cap_and_an_hour()
    {
        ExecutionBudget budget = ExecutionBudget.Default;

        budget.MaxSteps.ShouldBe(ExecutionBudget.MaxStepsCeiling);
        budget.MaxSteps.ShouldBe(500);
        budget.WallClock.ShouldBe(TimeSpan.FromHours(24));
        budget.MaxSubWorkflowDepth.ShouldBe(IWorkflowRun.MaxSubWorkflowDepth);
        budget.RetryAfterCeiling.ShouldBe(TimeSpan.FromHours(1));
    }

    [TestMethod]
    public void Fuel_cannot_be_constructed_above_the_journal_cap_or_below_one()
    {
        // ADR 0068: the journal is the counter the coordinator verifies against, and it is exact only up to its cap.
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(ExecutionBudget.MaxStepsCeiling + 1, TimeSpan.FromHours(1), 8, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(0, TimeSpan.FromHours(1), 8, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.Zero, 8, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), -1, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.FromSeconds(-1)));
    }

    [TestMethod]
    public void An_environment_override_only_tightens_and_an_omitted_limit_is_the_ceilings()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10));
        using ParsedJsonDocument<JsonElement> over = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"maxSteps":1000,"wallClockSeconds":60,"maxSubWorkflowDepth":2}"""));

        ExecutionBudget resolved = ExecutionBudget.Resolve(ceiling, over.RootElement);

        // A stored override wider than the ceiling never widens it (a malformed or squatted record can only tighten).
        resolved.MaxSteps.ShouldBe(200);
        resolved.WallClock.ShouldBe(TimeSpan.FromSeconds(60));
        resolved.MaxSubWorkflowDepth.ShouldBe(2);
        resolved.RetryAfterCeiling.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [TestMethod]
    public void An_absent_or_malformed_stored_override_resolves_to_the_ceiling()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10));
        ExecutionBudget.Resolve(ceiling, default).ShouldBe(ceiling);

        using ParsedJsonDocument<JsonElement> malformed = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"maxSteps":"lots","wallClockSeconds":0}"""));
        ExecutionBudget.Resolve(ceiling, malformed.RootElement).ShouldBe(ceiling);

        using ParsedJsonDocument<JsonElement> notAnObject = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("42"));
        ExecutionBudget.Resolve(ceiling, notAnObject.RootElement).ShouldBe(ceiling);
    }

    [TestMethod]
    public void An_authored_override_wider_than_the_ceiling_is_refused_and_a_tighter_one_admitted()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10));

        using ParsedJsonDocument<JsonElement> wider = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"maxSteps":201}"""));
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetOverride.ValidateAuthored(wider.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> longer = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"wallClockSeconds":7201}"""));
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetOverride.ValidateAuthored(longer.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> notInteger = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"maxSubWorkflowDepth":"deep"}"""));
        Should.Throw<ArgumentException>(() => ExecutionBudgetOverride.ValidateAuthored(notInteger.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> notAnObject = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("[]"));
        Should.Throw<ArgumentException>(() => ExecutionBudgetOverride.ValidateAuthored(notAnObject.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> tighter = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"maxSteps":200,"wallClockSeconds":7200,"maxSubWorkflowDepth":0,"retryAfterCeilingSeconds":0}"""));
        Should.NotThrow(() => ExecutionBudgetOverride.ValidateAuthored(tighter.RootElement, ceiling));
    }
}
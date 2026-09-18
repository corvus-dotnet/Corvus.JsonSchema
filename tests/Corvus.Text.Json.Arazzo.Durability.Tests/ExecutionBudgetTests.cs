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
    public void A_budget_fault_is_recognised_by_its_error_type()
    {
        ExecutionBudgetFault.IsBudgetFault(ExecutionBudgetFault.Fuel).ShouldBeTrue();
        ExecutionBudgetFault.IsBudgetFault(ExecutionBudgetFault.Deadline).ShouldBeTrue();
        ExecutionBudgetFault.IsBudgetFault(ExecutionBudgetFault.Depth).ShouldBeTrue();
        ExecutionBudgetFault.IsBudgetFault("boom").ShouldBeFalse();
        ExecutionBudgetFault.IsBudgetFault((string?)null).ShouldBeFalse();

        ExecutionBudgetFault.IsBudgetFault("budget-deadline"u8).ShouldBeTrue();
        ExecutionBudgetFault.IsBudgetFault("budget-deadlines"u8).ShouldBeFalse();
    }

    [TestMethod]
    public void The_predicate_finds_fuel_before_the_deadline_and_nothing_within_budget()
    {
        // ADR 0068: fuel is decided first, and a truncated journal is over any admissible budget by definition.
        var budget = new ExecutionBudget(3, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        ExecutionBudgetFault.Find(budget, new CheckpointBudgetFacts(budget, 3, false, false), createdAt, createdAt + TimeSpan.FromHours(1)).ShouldBeNull();
        ExecutionBudgetFault.Find(budget, new CheckpointBudgetFacts(budget, 4, false, false), createdAt, createdAt).ShouldBe(ExecutionBudgetFault.Fuel);
        ExecutionBudgetFault.Find(budget, new CheckpointBudgetFacts(budget, 1, true, false), createdAt, createdAt).ShouldBe(ExecutionBudgetFault.Fuel);
        ExecutionBudgetFault.Find(budget, new CheckpointBudgetFacts(budget, 4, false, false), createdAt, createdAt + TimeSpan.FromHours(2)).ShouldBe(ExecutionBudgetFault.Fuel);
        ExecutionBudgetFault.Find(budget, new CheckpointBudgetFacts(budget, 1, false, false), createdAt, createdAt + TimeSpan.FromHours(1) + TimeSpan.FromTicks(1)).ShouldBe(ExecutionBudgetFault.Deadline);
    }

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
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(ExecutionBudget.MaxStepsCeiling + 1, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(0, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.Zero, 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), -1, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.FromSeconds(-1), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));
    }

    [TestMethod]
    public void An_environment_override_only_tightens_and_an_omitted_limit_is_the_ceilings()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
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
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
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
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);

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

    [TestMethod]
    public void The_default_transport_bounds_are_a_hundred_seconds_and_sixteen_mebibytes()
    {
        ExecutionBudget budget = ExecutionBudget.Default;

        budget.StepTimeout.ShouldBe(TimeSpan.FromSeconds(100));
        budget.MaxResponseBytes.ShouldBe(16L * 1024 * 1024);
        ExecutionBudget.StepTimeoutCeiling.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [TestMethod]
    public void A_step_timeout_cannot_be_constructed_above_its_ceiling_nor_either_transport_bound_below_one()
    {
        // ADR 0068: the run-path clients carry a fixed backstop timeout above the ceiling, so no budget may exceed it.
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.StepTimeoutCeiling + TimeSpan.FromTicks(1), 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, TimeSpan.Zero, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, TimeSpan.FromSeconds(1), 0));
        Should.NotThrow(() => new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.StepTimeoutCeiling, 1));
    }

    [TestMethod]
    public void An_environment_override_tightens_the_transport_bounds_and_never_widens_them()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(60), 1_000_000);

        using ParsedJsonDocument<JsonElement> tighter = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":5,"maxResponseBytes":4096}"""));
        ExecutionBudget tightened = ExecutionBudget.Resolve(ceiling, tighter.RootElement);
        tightened.StepTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        tightened.MaxResponseBytes.ShouldBe(4096);
        tightened.MaxSteps.ShouldBe(200);

        // Wider than the ceiling, past the hard step-timeout ceiling, or malformed: a stored record can only tighten.
        using ParsedJsonDocument<JsonElement> wider = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":120,"maxResponseBytes":2000000}"""));
        ExecutionBudget.Resolve(ceiling, wider.RootElement).ShouldBe(ceiling);

        using ParsedJsonDocument<JsonElement> malformed = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":601,"maxResponseBytes":0}"""));
        ExecutionBudget.Resolve(ceiling, malformed.RootElement).ShouldBe(ceiling);
    }

    [TestMethod]
    public void An_authored_transport_bound_wider_than_the_ceiling_is_refused()
    {
        var ceiling = new ExecutionBudget(200, TimeSpan.FromHours(2), 4, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(60), 1_000_000);

        using ParsedJsonDocument<JsonElement> slower = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":61}"""));
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetOverride.ValidateAuthored(slower.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> larger = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"maxResponseBytes":1000001}"""));
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetOverride.ValidateAuthored(larger.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> zero = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":0}"""));
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetOverride.ValidateAuthored(zero.RootElement, ceiling));

        using ParsedJsonDocument<JsonElement> atTheCeiling = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"stepTimeoutSeconds":60,"maxResponseBytes":1000000}"""));
        Should.NotThrow(() => ExecutionBudgetOverride.ValidateAuthored(atTheCeiling.RootElement, ceiling));
    }

    [TestMethod]
    public void The_checkpoint_budget_round_trips_the_transport_bounds_and_requires_them()
    {
        var budget = new ExecutionBudget(7, TimeSpan.FromMinutes(30), 2, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), 65536);

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            budget.WriteTo(writer);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<JsonElement> written = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        written.RootElement.TryGetProperty("budget"u8, out JsonElement element).ShouldBeTrue();
        ExecutionBudget.TryRead(element, out ExecutionBudget read).ShouldBeTrue();
        read.ShouldBe(budget);

        // A budget without the transport bounds is not a budget: nothing has shipped that wrote one.
        using ParsedJsonDocument<JsonElement> older = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"maxSteps":7,"wallClockMs":1800000,"maxSubWorkflowDepth":2,"retryAfterCeilingMs":5000}"""));
        ExecutionBudget.TryRead(older.RootElement, out _).ShouldBeFalse();

        using ParsedJsonDocument<JsonElement> pastTheCeiling = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"maxSteps":7,"wallClockMs":1800000,"maxSubWorkflowDepth":2,"retryAfterCeilingMs":5000,"stepTimeoutMs":600001,"maxResponseBytes":1}"""));
        ExecutionBudget.TryRead(pastTheCeiling.RootElement, out _).ShouldBeFalse();
    }
}
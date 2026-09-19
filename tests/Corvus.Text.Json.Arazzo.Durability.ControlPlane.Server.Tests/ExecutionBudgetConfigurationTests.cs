// <copyright file="ExecutionBudgetConfigurationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// A deployment's execution-budget ceiling (ADR 0068) read from the host's configuration: the limits it names replace
/// the default's, and a limit it cannot have stops the host instead of starting it on a different one.
/// </summary>
[TestClass]
public sealed class ExecutionBudgetConfigurationTests
{
    [TestMethod]
    public void No_section_is_the_platform_default()
    {
        ExecutionBudgetConfiguration.ReadCeiling(Configuration([])).ShouldBe(ExecutionBudget.Default);
    }

    [TestMethod]
    public void A_limit_named_replaces_the_defaults_and_a_limit_left_out_keeps_it()
    {
        ExecutionBudget ceiling = ExecutionBudgetConfiguration.ReadCeiling(Configuration(new()
        {
            ["Arazzo:ExecutionBudgetCeiling:maxSteps"] = "250",
            ["Arazzo:ExecutionBudgetCeiling:wallClockSeconds"] = "172800",
            ["Arazzo:ExecutionBudgetCeiling:maxResponseBytes"] = "1048576",
        }));

        ceiling.MaxSteps.ShouldBe(250);

        // Wider than the default's day: the platform has no hard limit on the wall clock, so a ceiling may be.
        ceiling.WallClock.ShouldBe(TimeSpan.FromDays(2));
        ceiling.MaxResponseBytes.ShouldBe(1048576);
        ceiling.MaxSubWorkflowDepth.ShouldBe(ExecutionBudget.Default.MaxSubWorkflowDepth);
        ceiling.RetryAfterCeiling.ShouldBe(ExecutionBudget.Default.RetryAfterCeiling);
        ceiling.StepTimeout.ShouldBe(ExecutionBudget.Default.StepTimeout);
    }

    [TestMethod]
    [DataRow("maxSteps", "501")]
    [DataRow("maxSteps", "0")]
    [DataRow("stepTimeoutSeconds", "601")]
    [DataRow("wallClockSeconds", "0")]
    [DataRow("maxSubWorkflowDepth", "-1")]
    [DataRow("retryAfterCeilingSeconds", "-1")]
    [DataRow("maxResponseBytes", "0")]
    public void A_limit_the_platform_cannot_have_stops_the_host(string limit, string value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionBudgetConfiguration.ReadCeiling(Configuration(new() { ["Arazzo:ExecutionBudgetCeiling:" + limit] = value })));
    }

    [TestMethod]
    [DataRow("maxSteps", "lots")]
    [DataRow("wallClockSeconds", "1.5")]
    [DataRow("maxSteps", "99999999999")]
    public void A_limit_that_is_not_a_whole_number_stops_the_host_and_names_the_key(string limit, string value)
    {
        InvalidOperationException thrown = Should.Throw<InvalidOperationException>(() => ExecutionBudgetConfiguration.ReadCeiling(Configuration(new() { ["Arazzo:ExecutionBudgetCeiling:" + limit] = value })));
        thrown.Message.ShouldContain("Arazzo:ExecutionBudgetCeiling:" + limit);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
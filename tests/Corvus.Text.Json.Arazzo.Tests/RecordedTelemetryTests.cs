// <copyright file="RecordedTelemetryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class RecordedTelemetryTests
{
    [TestMethod]
    public void Records_completed_activities_from_the_arazzo_source()
    {
        using var recorded = new RecordedTelemetry();

        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.run"))
        {
            activity.ShouldNotBeNull();
            activity.SetTag("workflow.id", "adopt");
        }

        IReadOnlyList<Activity> runs = recorded.ActivitiesNamed("workflow.run");
        runs.Count.ShouldBe(1);
        runs[0].GetTagItem("workflow.id").ShouldBe("adopt");
        recorded.Activities.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Sums_counter_measurements()
    {
        using var recorded = new RecordedTelemetry();

        ArazzoTelemetry.StepsExecuted.Add(1);
        ArazzoTelemetry.StepsExecuted.Add(3);

        recorded.Sum("corvus.arazzo.steps.executed").ShouldBe(4);
    }

    [TestMethod]
    public void Sums_histogram_measurements_and_captures_tags()
    {
        using var recorded = new RecordedTelemetry();

        ArazzoTelemetry.StepDuration.Record(0.5, new KeyValuePair<string, object?>("step.id", "login"));
        ArazzoTelemetry.StepDuration.Record(1.5);

        recorded.Sum("corvus.arazzo.step.duration").ShouldBe(2.0);
        recorded.Measurements
            .First(m => m.InstrumentName == "corvus.arazzo.step.duration" && m.Value == 0.5)
            .Tags.ShouldContain(new KeyValuePair<string, object?>("step.id", "login"));
    }

    [TestMethod]
    public void Unknown_instrument_sums_to_zero()
    {
        using var recorded = new RecordedTelemetry();

        recorded.Sum("corvus.arazzo.nonexistent").ShouldBe(0);
    }

    [TestMethod]
    public void Disposed_recorder_stops_capturing()
    {
        var recorded = new RecordedTelemetry();
        recorded.Dispose();

        using (ArazzoTelemetry.ActivitySource.StartActivity("workflow.run"))
        {
        }

        ArazzoTelemetry.StepsExecuted.Add(1);

        recorded.Activities.Count.ShouldBe(0);
        recorded.Sum("corvus.arazzo.steps.executed").ShouldBe(0);
    }
}
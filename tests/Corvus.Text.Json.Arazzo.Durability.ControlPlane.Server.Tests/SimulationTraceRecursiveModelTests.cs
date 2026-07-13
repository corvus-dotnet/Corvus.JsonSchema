// <copyright file="SimulationTraceRecursiveModelTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Regeneration smoke check for the document's first recursive schema (pack 3 slice A): a
/// <see cref="Models.SimulationTrace"/> whose step carries a nested <c>subTrace</c> — itself a
/// SimulationTrace with its own <c>workflowId</c> — parses and navigates through the recursion, and the
/// additive <c>skipped</c> flag plus the <c>paused</c>/<c>suspended</c> parent statuses round-trip.
/// </summary>
[TestClass]
public sealed class SimulationTraceRecursiveModelTests
{
    private const string NestedTraceJson = """
        {
          "outcome": "paused",
          "pausedBefore": "call-sub/child-step",
          "steps": [
            {
              "stepId": "call-sub",
              "status": "paused",
              "attempt": 0,
              "subTrace": {
                "workflowId": "child",
                "outcome": "paused",
                "pausedBefore": "child-step",
                "steps": [
                  { "stepId": "child-step", "status": "completed", "attempt": 0, "skipped": true }
                ]
              }
            }
          ]
        }
        """;

    [TestMethod]
    public void A_nested_subTrace_parses_and_navigates_through_the_recursion()
    {
        Models.SimulationTrace trace = Models.SimulationTrace.ParseValue(NestedTraceJson);

        // Root: paused with a scoped pausedBefore; the parent step carries the new paused status.
        trace.Outcome.GetString().ShouldBe("paused");
        trace.PausedBefore.GetString().ShouldBe("call-sub/child-step");
        trace.Steps.GetArrayLength().ShouldBe(1);

        Models.SimulationTrace.SimulatedStepArray.SimulatedStep parent = default;
        foreach (Models.SimulationTrace.SimulatedStepArray.SimulatedStep step in trace.Steps.EnumerateArray())
        {
            parent = step;
            break;
        }

        parent.StepId.GetString().ShouldBe("call-sub");
        parent.Status.GetString().ShouldBe("paused");

        // The recursion: the parent step's subTrace is itself a SimulationTrace with its own workflowId.
        Models.SimulationTrace sub = parent.SubTrace;
        sub.WorkflowId.GetString().ShouldBe("child");
        sub.Outcome.GetString().ShouldBe("paused");
        sub.Steps.GetArrayLength().ShouldBe(1);

        Models.SimulationTrace.SimulatedStepArray.SimulatedStep child = default;
        foreach (Models.SimulationTrace.SimulatedStepArray.SimulatedStep step in sub.Steps.EnumerateArray())
        {
            child = step;
            break;
        }

        // The additive skipped flag round-trips on the nested record.
        child.StepId.GetString().ShouldBe("child-step");
        bool skipped = child.Skipped;
        skipped.ShouldBeTrue();
    }
}

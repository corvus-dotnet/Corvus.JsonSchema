// <copyright file="MetadataTraceAttributionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Linq;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// §18 R3: proves <see cref="MetadataTraceAssembler"/> attributes recorded exchanges to steps by the runner's
/// per-step boundaries (<see cref="RecordingApiTransport.StepBoundaries"/>) — so a retried step's several attempts
/// group under it — where the legacy positional one-per-step attribution leaks a retry onto the following step.
/// </summary>
[TestClass]
public sealed class MetadataTraceAttributionTests
{
    // A completed run: stepA (retried once → two attempts) then stepB (one call).
    private const string Checkpoint = """
        {
          "status": "Completed",
          "stepOutputs": { "stepA": { "x": 1 }, "stepB": { "y": 2 } },
          "retryCounters": { "stepA": 1 }
        }
        """;

    private static readonly RecordedApiExchange[] Exchanges =
    [
        new(OperationMethod.Get, "/a", 503),   // stepA attempt 1 (failed)
        new(OperationMethod.Get, "/a", 200),   // stepA attempt 2 (retry, succeeded)
        new(OperationMethod.Post, "/b", 200),  // stepB
    ];

    [TestMethod]
    public void Boundaries_group_a_retried_steps_attempts_under_that_step()
    {
        // stepA checkpointed after both its attempts (cumulative count 2); stepB after its call (count 3).
        List<int[]> stepRequests = AssembleStepRequestStatuses(stepBoundaries: [2, 3]);

        stepRequests.Count.ShouldBe(2);
        stepRequests[0].ShouldBe([503, 200]); // stepA: both attempts grouped under it
        stepRequests[1].ShouldBe([200]);      // stepB: its single call
    }

    [TestMethod]
    public void Without_boundaries_falls_back_to_the_legacy_positional_attribution()
    {
        // The legacy behaviour (unchanged): one exchange per step by position, trailing surplus on the last step —
        // which mis-attributes stepA's retry onto stepB. This is exactly the fragility R3's boundaries remove.
        List<int[]> stepRequests = AssembleStepRequestStatuses(stepBoundaries: null);

        stepRequests.Count.ShouldBe(2);
        stepRequests[0].ShouldBe([503]);      // stepA gets only exchange[0]
        stepRequests[1].ShouldBe([200, 200]); // stepB gets exchange[1] (stepA's retry) + trailing exchange[2]
    }

    // The per-step request status codes from the assembled trace. Parsing lives entirely in this method with var
    // locals so no System.Text.Json.JsonElement is named (the Corvus namespaces in scope also expose a JsonElement).
    private static List<int[]> AssembleStepRequestStatuses(IReadOnlyList<int>? stepBoundaries)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            MetadataTraceAssembler.WriteTrace(writer, Encoding.UTF8.GetBytes(Checkpoint), Exchanges, pausedBeforeStepId: null, stepBoundaries);
            writer.Flush();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        var stepRequests = new List<int[]>();
        foreach (var step in document.RootElement.GetProperty("steps").EnumerateArray())
        {
            stepRequests.Add(step.TryGetProperty("requests", out var requests)
                ? [.. requests.EnumerateArray().Select(static r => r.GetProperty("status").GetInt32())]
                : []);
        }

        return stepRequests;
    }
}

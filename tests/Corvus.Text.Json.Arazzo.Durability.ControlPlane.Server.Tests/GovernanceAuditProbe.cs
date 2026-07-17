// <copyright file="GovernanceAuditProbe.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Captures the governance-audit spans (design §850) a control-plane surface emits on the Arazzo
/// <see cref="ArazzoTelemetry.ActivitySource"/> for a test's duration, so a contract test can assert who did what to
/// which governed resource, and with what outcome. Filter by target id (a rule name, binding id, environment name,
/// request id, or <c>runnerId@environment</c>) — unique per test, so it isolates against parallel test classes.
/// </summary>
internal sealed class GovernanceAuditProbe : IDisposable
{
    private readonly List<Activity> spans = [];
    private readonly ActivityListener listener;

    private GovernanceAuditProbe()
    {
        this.listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ArazzoTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (this.spans)
                {
                    this.spans.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(this.listener);
    }

    public static GovernanceAuditProbe Capture() => new();

    /// <summary>The ordered outcomes recorded for the given target across its lifecycle.</summary>
    public IReadOnlyList<string> Outcomes(string targetId)
    {
        lock (this.spans)
        {
            return [.. this.spans.Where(s => (string?)s.GetTagItem(ArazzoTelemetry.TargetIdTag) == targetId).Select(s => (string)s.GetTagItem(ArazzoTelemetry.OutcomeTag)!)];
        }
    }

    /// <summary>The ordered (action, outcome) pairs recorded for the given target across its lifecycle.</summary>
    public IReadOnlyList<(string Action, string Outcome)> Events(string targetId)
    {
        lock (this.spans)
        {
            return [.. this.spans.Where(s => (string?)s.GetTagItem(ArazzoTelemetry.TargetIdTag) == targetId).Select(s => (s.OperationName, (string)s.GetTagItem(ArazzoTelemetry.OutcomeTag)!))];
        }
    }

    public void Dispose() => this.listener.Dispose();
}
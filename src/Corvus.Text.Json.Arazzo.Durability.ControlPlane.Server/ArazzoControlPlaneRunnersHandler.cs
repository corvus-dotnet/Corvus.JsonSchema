// <copyright file="ArazzoControlPlaneRunnersHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the control-plane <c>/runners</c> endpoint by reading the runner registry. Runners self-register
/// and heartbeat directly against the durability layer (<see cref="IRunnerRegistry"/>); this handler only reads
/// that registry for observability.
/// </summary>
public sealed class ArazzoControlPlaneRunnersHandler : IApiRunnersHandler
{
    private readonly IRunnerRegistry runners;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneRunnersHandler"/> class.</summary>
    /// <param name="runners">The runner registry the endpoint reads.</param>
    public ArazzoControlPlaneRunnersHandler(IRunnerRegistry runners)
    {
        ArgumentNullException.ThrowIfNull(runners);
        this.runners = runners;
    }

    /// <inheritdoc/>
    public async ValueTask<ListRunnersResult> HandleListRunnersAsync(ListRunnersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RunnerRegistration> registered = await this.runners.ListAsync(cancellationToken).ConfigureAwait(false);
        return ListRunnersResult.Ok(BuildPage(registered), workspace);
    }

    // The persisted RunnerRegistration and the API Runner share the same JSON shape, so each runner is a free
    // re-wrap of the stored document — no per-field projection.
    private static Models.RunnerPage.Source BuildPage(IReadOnlyList<RunnerRegistration> registered)
        => new((ref Models.RunnerPage.Builder b) =>
            b.Create(runners: new Models.RunnerPage.RunnerArray.Source(
                (ref Models.RunnerPage.RunnerArray.Builder ab) =>
                {
                    foreach (RunnerRegistration runner in registered)
                    {
                        ab.AddItem(Models.Runner.From(runner));
                    }
                })));
}
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

        // The persisted RunnerRegistration and the API Runner share the same JSON shape, so each runner is a free
        // whole-document re-wrap (Models.Runner.From) — no per-field projection. The page is built closure-free (the
        // registration list threaded as the context through the static BuildRunners) and consumed in place
        // (RunnerPage.Build is ref-scoped to its `in` argument, so it cannot be returned from a helper).
        Models.RunnerPage.Source<IReadOnlyList<RunnerRegistration>> body = Models.RunnerPage.Build(
            in registered,
            runners: Models.RunnerPage.RunnerArray.Build(in registered, BuildRunners));
        return ListRunnersResult.Ok(body, workspace);
    }

    private static void BuildRunners(in IReadOnlyList<RunnerRegistration> registered, ref Models.RunnerPage.RunnerArray.Builder array)
    {
        foreach (RunnerRegistration runner in registered)
        {
            array.AddItem(Models.Runner.From(runner));
        }
    }
}
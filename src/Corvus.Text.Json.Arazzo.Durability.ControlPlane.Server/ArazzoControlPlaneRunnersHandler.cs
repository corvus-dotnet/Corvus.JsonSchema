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
        // An absent limit passes 0 — the contract's "use the store's default page size" sentinel; the page token flows to
        // the store as its JSON value (From() rewraps parameters.PageToken — free, no managed string), decoded
        // bytes-native into one keyset page (bounded — never all runners).
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using RunnerRegistryPage page = await this.runners.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The persisted RunnerRegistration and the API Runner share the same JSON shape, so each runner is a free
        // whole-document re-wrap (Models.Runner.From) — no per-field projection. The registrations are detached (no pooled
        // buffer), so the body's From-wraps keep them GC-reachable through the synchronous Ok materialisation — no ownership
        // transfer; `using page` only returns the token buffer (the continuation token is copied into the response by Ok).
        IReadOnlyList<RunnerRegistration> registered = page.Runners;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.RunnerPage.Source<IReadOnlyList<RunnerRegistration>> body = Models.RunnerPage.Build(
            in registered,
            runners: Models.RunnerPage.RunnerArray.Build(in registered, BuildRunners),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
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